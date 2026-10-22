using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace PPBTool
{
    /// <summary>A valid teleport destination: a concrete level plus a grid cell.</summary>
    internal struct TeleportCell
    {
        public Level Level;
        public int X;
        public int Y;
        public bool ViaBox;

        public override string ToString()
        {
            return "(" + X + ", " + Y + ")" + (ViaBox ? " via box" : string.Empty);
        }
    }

    /// <summary>A concrete place in a parent level that references a Level.</summary>
    internal enum RefKind
    {
        RootOwner,
        RealBox,
        CloneBox,
        InfEnter,
        InfExit,
        Generated,
        Other,
    }

    internal sealed class LevelReference
    {
        public Block Block;
        public Level Parent;
        public Level Target;
        public int X;
        public int Y;
        public RefKind Kind;
        public bool IsGridResident;
        public bool IsHiddenOverlap;
        public bool IsPrimary;

        public bool CanBack
        {
            get
            {
                return Block != null
                    && Parent != null
                    && Target != null
                    && Block.SubLevel == Target
                    && World.levels != null
                    && World.levels.Contains(Parent);
            }
        }

        public string KindLabel
        {
            get
            {
                switch (Kind)
                {
                    case RefKind.RootOwner: return "root";
                    case RefKind.CloneBox: return "clone";
                    case RefKind.InfEnter: return "Infinite Enter";
                    case RefKind.InfExit: return "Infinite Exit";
                    case RefKind.Generated: return "generated";
                    case RefKind.RealBox: return "box";
                    default: return "ref";
                }
            }
        }

        public string Label
        {
            get
            {
                if (Parent == null)
                {
                    return KindLabel + " (no outer level)";
                }

                string label = LevelGraph.DescribeLevel(Parent) + " (" + X + "," + Y + ") · " + KindLabel;
                if (IsPrimary)
                {
                    label += " · primary";
                }

                if (IsHiddenOverlap)
                {
                    label += " · hidden overlap";
                }

                return label;
            }
        }

        public override string ToString()
        {
            return Label;
        }
    }

    /// <summary>One of a level's infinity-enter / infinity-exit links.</summary>
    internal sealed class InfLink
    {
        public bool Enter;
        public int Index;
        public Level Source;
        public Block Block;
        public Level Container;
        public Level Target;
        public int TargetX;
        public int TargetY;

        public string Direction
        {
            get { return Enter ? UiText.Get("inf_enter") : UiText.Get("inf_exit"); }
        }

        public string TargetLabel
        {
            get
            {
                string target = LevelGraph.DescribeLevel(Target);
                if (!Enter && Target != null)
                {
                    target += " @ (" + TargetX + "," + TargetY + ")";
                }

                return target;
            }
        }

        public string MenuLabel
        {
            get
            {
                return Direction + " #" + Index + " → " + TargetLabel;
            }
        }

        public override string ToString()
        {
            return MenuLabel;
        }
    }

    /// <summary>One level in the scene hierarchy (a hub area, a room, or a box interior).</summary>
    internal sealed class LevelNode
    {
        public Level Level;
        public Block ParentBlock;
        public LevelNode ParentNode;
        public string Title = string.Empty;
        public string Subtitle = string.Empty;
        public string Tags = string.Empty;
        public int Depth;
        public bool ContainsPlayer;
        public List<LevelNode> Children = new List<LevelNode>();
        public List<LevelReference> References = new List<LevelReference>();
        public int HiddenReferenceCount;

        /// <summary>Root → this node path, for the header label.</summary>
        public string Path
        {
            get
            {
                var parts = new List<string>();
                LevelNode current = this;
                int guard = 0;
                while (current != null && guard++ < 64)
                {
                    parts.Add(current.Title);
                    current = current.ParentNode;
                }

                parts.Reverse();
                return string.Join(" / ", parts.ToArray());
            }
        }
    }

    /// <summary>
    /// Read-only scan of the currently loaded scene. Rebuilt every time the map opens,
    /// so it automatically follows hub loads, CustomHubs reloads and box nesting.
    /// </summary>
    internal sealed class LevelGraph
    {
        private readonly List<LevelNode> roots = new List<LevelNode>();
        private readonly Dictionary<Level, LevelNode> nodesByLevel =
            new Dictionary<Level, LevelNode>();
        private readonly Dictionary<Level, int> infInCounts = new Dictionary<Level, int>();
        private readonly Dictionary<Level, int> infOutCounts = new Dictionary<Level, int>();
        private readonly Dictionary<Level, List<LevelReference>> referencesByLevel =
            new Dictionary<Level, List<LevelReference>>();

        public string SceneLabel = string.Empty;

        public LevelNode PlayerNode { get; private set; }

        public IReadOnlyList<LevelNode> Roots
        {
            get { return roots; }
        }

        public IEnumerable<LevelNode> AllNodes
        {
            get { return nodesByLevel.Values; }
        }

        public LevelNode FindNode(Level level)
        {
            if (level == null)
            {
                return null;
            }

            LevelNode node;
            return nodesByLevel.TryGetValue(level, out node) ? node : null;
        }

        /// <summary>Rebuild the hierarchy from World.levels. Never throws.</summary>
        public void Build()
        {
            roots.Clear();
            nodesByLevel.Clear();
            infInCounts.Clear();
            infOutCounts.Clear();
            referencesByLevel.Clear();
            PlayerNode = null;
            SceneLabel = GetSceneLabel();

            try
            {
                List<Level> levels = World.levels;
                if (levels == null || levels.Count == 0)
                {
                    return;
                }

                var levelSet = new HashSet<Level>(levels);
                var parentByLevel = new Dictionary<Level, Block>();
                var childrenByLevel = new Dictionary<Level, List<Level>>();

                BuildReferences(levels);

                foreach (Level level in levels)
                {
                    if (level == null)
                    {
                        continue;
                    }

                    Block parent = PreferredParent(level);
                    if (parent != null && !levelSet.Contains(parent.OuterLevel))
                    {
                        parent = null;
                    }

                    parentByLevel[level] = parent;
                    if (parent == null)
                    {
                        continue;
                    }

                    List<Level> children;
                    if (!childrenByLevel.TryGetValue(parent.OuterLevel, out children))
                    {
                        children = new List<Level>();
                        childrenByLevel[parent.OuterLevel] = children;
                    }

                    if (!children.Contains(level))
                    {
                        children.Add(level);
                    }
                }

                BuildInfinityCounts(levels);

                var rootLevels = new List<Level>();
                foreach (Level level in levels)
                {
                    if (level != null && parentByLevel[level] == null)
                    {
                        rootLevels.Add(level);
                    }
                }

                rootLevels.Sort((a, b) =>
                {
                    int va = a.InfZone ? 1 : 0;
                    int vb = b.InfZone ? 1 : 0;
                    return va != vb ? va.CompareTo(vb) : 0;
                });

                if (rootLevels.Count == 0)
                {
                    rootLevels.AddRange(levels.Where(l => l != null));
                }

                for (int i = 0; i < rootLevels.Count; i++)
                {
                    roots.Add(BuildNode(
                        rootLevels[i], parentByLevel[rootLevels[i]], null, 0,
                        childrenByLevel, parentByLevel, i, rootLevels.Count));
                }

                // Defensive: a cyclic/malformed parent chain can leave levels unreachable
                // from any root. Show them as extra roots instead of silently dropping them.
                foreach (Level level in levels)
                {
                    if (level != null && !nodesByLevel.ContainsKey(level))
                    {
                        roots.Add(BuildNode(
                            level, parentByLevel[level], null, 0,
                            childrenByLevel, parentByLevel, roots.Count, roots.Count + 1));
                    }
                }

                Block player = GameState.Player;
                if (player != null)
                {
                    LevelNode playerNode;
                    if (nodesByLevel.TryGetValue(player.OuterLevel, out playerNode))
                    {
                        PlayerNode = playerNode;
                        playerNode.ContainsPlayer = true;
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error("Failed to scan scene hierarchy", ex);
                roots.Clear();
                nodesByLevel.Clear();
                PlayerNode = null;
            }
        }

        private LevelNode BuildNode(
            Level level, Block parent, LevelNode parentNode, int depth,
            Dictionary<Level, List<Level>> childrenByLevel,
            Dictionary<Level, Block> parentByLevel,
            int rootIndex, int rootCount)
        {
            var node = new LevelNode
            {
                Level = level,
                ParentBlock = parent,
                ParentNode = parentNode,
                Depth = depth,
                Title = BuildTitle(level, parent, depth, rootIndex, rootCount),
                Tags = BuildTags(level, parent),
                References = new List<LevelReference>(GetReferences(level)),
            };
            foreach (LevelReference reference in node.References)
            {
                if (reference.IsHiddenOverlap)
                {
                    node.HiddenReferenceCount++;
                }
            }

            node.Subtitle = BuildSubtitle(level, node.Tags);
            if (parent != null)
            {
                string flags = BlockFlags(parent);
                if (!string.IsNullOrEmpty(flags))
                {
                    node.Subtitle += "  {" + flags + "}";
                }
            }

            nodesByLevel[level] = node;

            List<Level> childLevels;
            if (childrenByLevel.TryGetValue(level, out childLevels))
            {
                foreach (Level child in childLevels)
                {
                    if (nodesByLevel.ContainsKey(child))
                    {
                        continue;
                    }

                    Block childParent;
                    parentByLevel.TryGetValue(child, out childParent);
                    node.Children.Add(BuildNode(
                        child, childParent, node, depth + 1,
                        childrenByLevel, parentByLevel, 0, 1));
                }
            }

            return node;
        }

        private void BuildInfinityCounts(List<Level> levels)
        {
            foreach (Level level in levels)
            {
                if (level == null)
                {
                    continue;
                }

                if (level.infEnterBlocks != null)
                {
                    foreach (Block block in level.infEnterBlocks)
                    {
                        if (block != null && block.SubLevel != null)
                        {
                            Bump(infInCounts, block.SubLevel);
                        }
                    }
                }

                if (level.infExitBlocks != null)
                {
                    foreach (Block block in level.infExitBlocks)
                    {
                        if (block != null && block.OuterLevel != null)
                        {
                            Bump(infOutCounts, block.OuterLevel);
                        }
                    }
                }
            }
        }

        private static void Bump(Dictionary<Level, int> counts, Level level)
        {
            int value;
            counts.TryGetValue(level, out value);
            counts[level] = value + 1;
        }

        private void BuildReferences(List<Level> levels)
        {
            foreach (Level level in levels)
            {
                if (level == null)
                {
                    continue;
                }

                var list = new List<LevelReference>();
                if (level.BlocksWithThisAsTheirSubLevel != null)
                {
                    Block primary = SafeExitBlock(level);
                    foreach (Block block in level.BlocksWithThisAsTheirSubLevel)
                    {
                        if (block == null || block.SubLevel != level)
                        {
                            continue;
                        }

                        bool resident = false;
                        if (block.OuterLevel != null
                            && block.OuterLevel.blocks != null
                            && block.OuterLevel.InBounds(block.xpos, block.ypos))
                        {
                            resident = block.OuterLevel.blocks[block.xpos, block.ypos] == block;
                        }

                        RefKind kind = RefKind.Other;
                        if (block.OuterLevel == null)
                        {
                            kind = RefKind.RootOwner;
                        }
                        else if (block.IsSomeInfEnterBlock)
                        {
                            kind = RefKind.InfEnter;
                        }
                        else if (block.IsSomeInfExitBlock)
                        {
                            kind = RefKind.InfExit;
                        }
                        else if (block.GeneratedInf)
                        {
                            kind = RefKind.Generated;
                        }
                        else if (block != primary)
                        {
                            kind = RefKind.CloneBox;
                        }
                        else
                        {
                            kind = RefKind.RealBox;
                        }

                        list.Add(new LevelReference
                        {
                            Block = block,
                            Parent = block.OuterLevel,
                            Target = level,
                            X = block.xpos,
                            Y = block.ypos,
                            Kind = kind,
                            IsGridResident = resident,
                            IsHiddenOverlap = block.OuterLevel != null && !resident,
                            IsPrimary = block == primary,
                        });
                    }
                }

                referencesByLevel[level] = list;
            }
        }

        public IReadOnlyList<LevelReference> GetReferences(Level level)
        {
            List<LevelReference> list;
            if (level != null && referencesByLevel.TryGetValue(level, out list))
            {
                return list;
            }

            return new LevelReference[0];
        }

        public LevelReference FindReference(Block block, Level level)
        {
            if (block == null || level == null)
            {
                return null;
            }

            foreach (LevelReference reference in GetReferences(level))
            {
                if (reference.Block == block)
                {
                    return reference;
                }
            }

            return null;
        }

        /// <summary>
        /// A box can be referenced by both its real parent and a void/infinity block.
        /// Prefer the real (non-InfZone) parent so the tree mirrors how the player walks.
        /// </summary>
        public static Block PreferredParent(Level level)
        {
            if (level == null || level.BlocksWithThisAsTheirSubLevel == null)
            {
                return null;
            }

            bool hasRootOwner = false;
            Block nonVoidGrid = null;
            Block nonVoid = null;
            Block voidGrid = null;
            Block voidParent = null;
            foreach (Block block in level.BlocksWithThisAsTheirSubLevel)
            {
                if (block == null || block.SubLevel != level)
                {
                    continue;
                }

                if (block.OuterLevel == null)
                {
                    // The block that created a root level. Every level also gets an
                    // auto-generated infinite-exit block inside a void, so this owner must keep
                    // the level at the tree root instead of letting the void adopt it.
                    hasRootOwner = true;
                    continue;
                }

                bool resident = block.OuterLevel.InBounds(block.xpos, block.ypos)
                    && block.OuterLevel.blocks[block.xpos, block.ypos] == block;

                if (!block.OuterLevel.InfZone)
                {
                    if (resident && nonVoidGrid == null)
                    {
                        nonVoidGrid = block;
                    }

                    if (nonVoid == null)
                    {
                        nonVoid = block;
                    }
                }
                else
                {
                    if (resident && voidGrid == null)
                    {
                        voidGrid = block;
                    }

                    if (voidParent == null)
                    {
                        voidParent = block;
                    }
                }
            }

            if (nonVoidGrid != null)
            {
                return nonVoidGrid;
            }

            if (nonVoid != null)
            {
                return nonVoid;
            }

            if (hasRootOwner)
            {
                return null;
            }

            return voidGrid ?? voidParent;
        }

        private static string BuildTitle(
            Level level, Block parent, int depth, int rootIndex, int rootCount)
        {
            if (level.InfZone)
            {
                string voidTitle = "Void " + level.width + "x" + level.height;
                return rootCount > 1 ? voidTitle + " #" + (rootIndex + 1) : voidTitle;
            }

            if (parent != null && parent.IsSomeInfEnterBlock)
            {
                string area = CleanName(level.hubAreaName);
                return string.IsNullOrEmpty(area) ? "Infinite Enter" : area;
            }

            if (parent != null && parent.IsSomeInfExitBlock)
            {
                string area = CleanName(level.hubAreaName);
                return string.IsNullOrEmpty(area) ? "Infinite Exit" : area;
            }

            string title = CleanName(level.hubAreaName);
            if (!string.IsNullOrEmpty(title))
            {
                return title;
            }

            if (depth == 0)
            {
                string label = GetSceneLabel();
                return rootCount > 1 ? label + " #" + (rootIndex + 1) : label;
            }

            if (parent != null)
            {
                return "Box (" + parent.xpos + ", " + parent.ypos + ")";
            }

            return "Room " + level.width + "x" + level.height;
        }

        private string BuildTags(Level level, Block parent)
        {
            var tags = new List<string>();
            if (level.InfZone)
            {
                tags.Add("void");
            }

            IReadOnlyList<LevelReference> refs = GetReferences(level);
            tags.Add("refs ×" + refs.Count);
            int hidden = refs.Count(r => r.IsHiddenOverlap);
            if (hidden > 0)
            {
                tags.Add("hidden ×" + hidden);
            }

            int count;
            if (infInCounts.TryGetValue(level, out count) && count > 0)
            {
                tags.Add("Infinite Enter ×" + count);
            }

            if (infOutCounts.TryGetValue(level, out count) && count > 0)
            {
                tags.Add("Infinite Exit ×" + count);
            }

            var infTags = new HashSet<string>();
            if (World.blocks != null)
            {
                foreach (Block block in World.blocks)
                {
                    if (block == null)
                    {
                        continue;
                    }

                    if (block.IsSomeInfExitBlock
                        && block.OuterLevel == level
                        && block.SubLevel != null
                        && block.SubLevel.infExitBlocks != null)
                    {
                        int degree = block.SubLevel.infExitBlocks.IndexOf(block);
                        if (degree >= 0)
                        {
                            infTags.Add(
                                UiText.Get("inf_exit") + " #" + degree
                                + " of " + DescribeLevel(block.SubLevel));
                        }
                    }

                    if (block.IsSomeInfEnterBlock && block.SubLevel == level)
                    {
                        string owner = block.OuterLevel != null
                            ? DescribeLevel(block.OuterLevel)
                            : null;
                        if (!string.IsNullOrEmpty(owner))
                        {
                            infTags.Add(
                                UiText.Get("inf_enter") + " #" + block.SomeInfEnterNum
                                + " of " + owner);
                        }
                    }
                }
            }

            tags.AddRange(infTags);

            return string.Join(" ", tags.ToArray());
        }

        private static string BuildSubtitle(Level level, string tags)
        {
            int boxes = level.blockList == null
                ? 0
                : level.blockList.Count(b => b != null && b.SubLevel != null);
            int portals = level.floorList == null
                ? 0
                : level.floorList.Count(f => f != null && f.Type == Floor.FloorType.LevelPortal);
            string text = level.width + "x" + level.height;
            if (boxes > 0)
            {
                text += "  boxes:" + boxes;
            }

            if (portals > 0)
            {
                text += "  portals:" + portals;
            }

            if (!string.IsNullOrEmpty(tags))
            {
                text += "  [" + tags + "]";
            }

            return text;
        }

        private static string GetSceneLabel()
        {
            string name = World.currentLevelName;
            if (name == "custom_level")
            {
                try
                {
                    string file = Path.GetFileNameWithoutExtension(
                        World.lastLoadedCustomLevelPath ?? string.Empty);
                    if (!string.IsNullOrEmpty(file))
                    {
                        name = "custom: " + file;
                    }
                }
                catch
                {
                    // Path access is best-effort only.
                }
            }

            return CleanName(name) ?? "scene";
        }

        private static string CleanName(string raw)
        {
            if (string.IsNullOrEmpty(raw) || raw == "_")
            {
                return null;
            }

            string cleaned = raw.Replace('_', ' ').Trim();
            if (cleaned.Length == 0)
            {
                return null;
            }

            return cleaned.Equals("custom level", StringComparison.OrdinalIgnoreCase)
                ? "Custom level"
                : cleaned;
        }

        public static bool IsVoidLevel(Level level)
        {
            return level != null && level.InfZone;
        }

        public static bool IsVoidBox(Block block)
        {
            return block != null && block.OuterLevel != null && block.OuterLevel.InfZone;
        }

        private static Block SafeExitBlock(Level level)
        {
            if (level == null || level.BlocksWithThisAsTheirSubLevel == null
                || level.BlocksWithThisAsTheirSubLevel.Count == 0)
            {
                return null;
            }

            try
            {
                return level.GetExitBlock();
            }
            catch
            {
                return null;
            }
        }

        public static Block ExitBlockOf(Level level)
        {
            return SafeExitBlock(level);
        }

        /// <summary>Game clone condition: a box is light when it is not its level's exit block.</summary>
        public static bool IsCloneBox(Block block)
        {
            if (block == null || block.SubLevel == null)
            {
                return false;
            }

            Block exit = SafeExitBlock(block.SubLevel);
            return exit != null && block != exit;
        }

        public static int OwnerCount(Block block)
        {
            if (block == null || block.SubLevel == null
                || block.SubLevel.BlocksWithThisAsTheirSubLevel == null)
            {
                return 0;
            }

            return block.SubLevel.BlocksWithThisAsTheirSubLevel.Count;
        }

        public static string BlockFlags(Block block)
        {
            if (block == null)
            {
                return string.Empty;
            }

            var parts = new List<string>();
            if (block.SubLevel != null)
            {
                parts.Add("box");
            }

            if (block.FlipH)
            {
                parts.Add("flip");
            }

            if (block.IsSomeInfEnterBlock)
            {
                parts.Add("Infinite Enter");
            }

            if (block.IsSomeInfExitBlock)
            {
                parts.Add("Infinite Exit");
            }

            if (block.GeneratedInf)
            {
                parts.Add("Generated Infinite");
            }

            if (block.Possessable)
            {
                parts.Add("possess");
            }

            if (block.Locked)
            {
                parts.Add("locked");
            }

            if (block.Shield)
            {
                parts.Add("shield");
            }

            if (block.Weight)
            {
                parts.Add("weight");
            }

            if (block.Rooted)
            {
                parts.Add("rooted");
            }

            if (block.SpecialEffect != 0)
            {
                parts.Add("fx" + block.SpecialEffect);
            }

            if (IsCloneBox(block))
            {
                parts.Add("clone");
            }

            return string.Join(" ", parts.ToArray());
        }

        public static string DescribeLevel(Level level)
        {
            if (level == null)
            {
                return "?";
            }

            if (!string.IsNullOrEmpty(level.hubAreaName))
            {
                return level.hubAreaName.Replace('_', ' ');
            }

            if (level.InfZone)
            {
                return "Void " + level.width + "x" + level.height;
            }

            return level.width + "x" + level.height;
        }

        /// <summary>True when the player can legally stand on this cell (empty, no portal).</summary>
        public static bool IsWalkableCell(Level level, int x, int y)
        {
            if (level == null || level.blocks == null || level.floors == null || !level.InBounds(x, y))
            {
                return false;
            }

            if (level.blocks[x, y] != null)
            {
                return false;
            }

            Floor floor = level.floors[x, y];
            return floor == null || floor.Type != Floor.FloorType.LevelPortal;
        }

        public List<InfLink> GetInfLinks(Level level)
        {
            var links = new List<InfLink>();
            if (level == null)
            {
                return links;
            }

            if (level.infEnterBlocks != null)
            {
                for (int i = 0; i < level.infEnterBlocks.Count; i++)
                {
                    Block block = level.infEnterBlocks[i];
                    if (block != null)
                    {
                        links.Add(new InfLink
                        {
                            Enter = true,
                            Index = i,
                            Source = level,
                            Block = block,
                            Container = block.OuterLevel,
                            Target = block.SubLevel,
                            TargetX = -1,
                            TargetY = -1,
                        });
                    }
                }
            }

            if (level.infExitBlocks != null)
            {
                for (int i = 0; i < level.infExitBlocks.Count; i++)
                {
                    Block block = level.infExitBlocks[i];
                    if (block != null)
                    {
                        links.Add(new InfLink
                        {
                            Enter = false,
                            Index = i,
                            Source = level,
                            Block = block,
                            Container = block.OuterLevel,
                            Target = block.OuterLevel,
                            TargetX = block.xpos,
                            TargetY = block.ypos,
                        });
                    }
                }
            }

            return links;
        }

        /// <summary>First null index, otherwise the list length. Never overwrites an existing zone.</summary>
        public static int NextInfIndex(Level level, bool enter)
        {
            List<Block> list = enter ? level.infEnterBlocks : level.infExitBlocks;
            if (list == null)
            {
                return 0;
            }

            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] == null)
                {
                    return i;
                }
            }

            return list.Count;
        }

        /// <summary>
        /// Landing cell when entering a box: nearest free cell to the interior center.
        /// Level.GetExitBlock() is the box block in the parent level, so its coordinates
        /// must not be used inside the interior.
        /// </summary>
        public static bool TryResolveEnterable(Block box, out TeleportCell cell)
        {
            cell = default(TeleportCell);
            if (box == null || box.SubLevel == null)
            {
                return false;
            }

            Level inner = box.SubLevel;
            int x;
            int y;
            if (!TryFindNearestWalkable(inner, inner.width / 2, inner.height / 2, out x, out y)
                && !TryFindAnyWalkable(inner, out x, out y))
            {
                return false;
            }

            cell = new TeleportCell
            {
                Level = inner,
                X = x,
                Y = y,
                ViaBox = true,
            };
            return true;
        }

        /// <summary>
        /// XOR of the FlipH flags of every box on the path from the scene root to this level.
        /// Entering a flipped box mirrors the player; the game derives that from this chain.
        /// </summary>
        public static bool PathFlip(Level level)
        {
            bool flip = false;
            int guard = 0;
            Level current = level;
            while (current != null && guard++ < 64)
            {
                Block parent = PreferredParent(current);
                if (parent == null || parent.OuterLevel == null)
                {
                    break;
                }

                flip ^= parent.FlipH;
                current = parent.OuterLevel;
            }

            return flip;
        }

        private static bool TryFindAnyWalkable(Level level, out int x, out int y)
        {
            x = 0;
            y = 0;
            if (level == null || level.blocks == null)
            {
                return false;
            }

            for (int j = 0; j < level.height; j++)
            {
                for (int i = 0; i < level.width; i++)
                {
                    if (IsWalkableCell(level, i, j))
                    {
                        x = i;
                        y = j;
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>Breadth-first search for the closest empty, portal-free cell.</summary>
        public static bool TryFindNearestWalkable(
            Level level, int startX, int startY, out int x, out int y)
        {
            x = startX;
            y = startY;
            if (level == null)
            {
                return false;
            }

            if (level.InBounds(startX, startY) && IsWalkableCell(level, startX, startY))
            {
                return true;
            }

            var seen = new HashSet<int>();
            var queue = new Queue<long>();
            queue.Enqueue(Pack(startX, startY));
            seen.Add(CellKey(level, startX, startY));

            int[] dx = { 0, 0, 1, -1 };
            int[] dy = { 1, -1, 0, 0 };
            int visited = 0;

            while (queue.Count > 0 && visited < 20000)
            {
                long packed = queue.Dequeue();
                int cx = (int)(packed >> 32);
                int cy = (int)(packed & 0xFFFFFFFFL);
                visited++;

                for (int i = 0; i < 4; i++)
                {
                    int nx = cx + dx[i];
                    int ny = cy + dy[i];
                    if (!level.InBounds(nx, ny) || !seen.Add(CellKey(level, nx, ny)))
                    {
                        continue;
                    }

                    if (IsWalkableCell(level, nx, ny))
                    {
                        x = nx;
                        y = ny;
                        return true;
                    }

                    // Only expand through empty cells: walls/boxes are not passable here.
                    if (level.blocks[nx, ny] == null)
                    {
                        queue.Enqueue(Pack(nx, ny));
                    }
                }
            }

            return false;
        }

        private static long Pack(int x, int y)
        {
            return ((long)x << 32) | (uint)y;
        }

        private static int CellKey(Level level, int x, int y)
        {
            return y * level.width + x;
        }
    }
}
