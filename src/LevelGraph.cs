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

    /// <summary>One level in the scene hierarchy (a hub area, a room, or a box interior).</summary>
    internal sealed class LevelNode
    {
        public Level Level;
        public Block ParentBlock;
        public LevelNode ParentNode;
        public string Title = string.Empty;
        public string Subtitle = string.Empty;
        public int Depth;
        public bool ContainsPlayer;
        public List<LevelNode> Children = new List<LevelNode>();

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

        /// <summary>Rebuild the hierarchy from World.levels. Never throws.</summary>
        public void Build()
        {
            roots.Clear();
            nodesByLevel.Clear();
            PlayerNode = null;
            SceneLabel = GetSceneLabel();

            try
            {
                List<Level> levels = World.levels;
                if (levels == null || levels.Count == 0)
                {
                    return;
                }

                // Boxes own their interior level through Block.SubLevel; the block always
                // lives in the parent level, so that pair defines the tree edge.
                var parents = new Dictionary<Level, Block>();
                var childrenByLevel = new Dictionary<Level, List<Level>>();

                foreach (Level parent in levels)
                {
                    if (parent == null || parent.blockList == null)
                    {
                        continue;
                    }

                    foreach (Block block in parent.blockList)
                    {
                        if (block == null || block.SubLevel == null)
                        {
                            continue;
                        }

                        if (!parents.ContainsKey(block.SubLevel))
                        {
                            parents[block.SubLevel] = block;
                        }

                        List<Level> children;
                        if (!childrenByLevel.TryGetValue(parent, out children))
                        {
                            children = new List<Level>();
                            childrenByLevel[parent] = children;
                        }

                        if (!children.Contains(block.SubLevel))
                        {
                            children.Add(block.SubLevel);
                        }
                    }
                }

                var rootLevels = new List<Level>();
                foreach (Level level in levels)
                {
                    if (level != null && !parents.ContainsKey(level))
                    {
                        rootLevels.Add(level);
                    }
                }

                if (rootLevels.Count == 0)
                {
                    // Malformed/cyclic scene: at least show every level instead of nothing.
                    rootLevels.AddRange(levels.Where(l => l != null));
                }

                // Several root levels are normal for CustomHubs hubs (each top-level Block
                // is its own level), so number unnamed roots instead of showing "hub" twice.
                for (int i = 0; i < rootLevels.Count; i++)
                {
                    roots.Add(BuildNode(
                        rootLevels[i], null, null, 0, childrenByLevel, i, rootLevels.Count));
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
            Dictionary<Level, List<Level>> childrenByLevel, int rootIndex, int rootCount)
        {
            var node = new LevelNode
            {
                Level = level,
                ParentBlock = parent,
                ParentNode = parentNode,
                Depth = depth,
                Title = BuildTitle(level, parent, depth, rootIndex, rootCount),
                Subtitle = BuildSubtitle(level),
            };

            nodesByLevel[level] = node;

            List<Level> childLevels;
            if (childrenByLevel.TryGetValue(level, out childLevels))
            {
                foreach (Level child in childLevels)
                {
                    // A SubLevel can be referenced by several blocks. Build it exactly once,
                    // otherwise whole sub-trees are duplicated in the tree.
                    if (nodesByLevel.ContainsKey(child))
                    {
                        continue;
                    }

                    node.Children.Add(BuildNode(
                        child, FindParentBlock(child, level), node, depth + 1,
                        childrenByLevel, 0, 1));
                }
            }

            return node;
        }

        private static Block FindParentBlock(Level child, Level parentLevel)
        {
            if (parentLevel == null || parentLevel.blockList == null)
            {
                return null;
            }

            foreach (Block block in parentLevel.blockList)
            {
                if (block != null && block.SubLevel == child)
                {
                    return block;
                }
            }

            return null;
        }

        private static string BuildTitle(
            Level level, Block parent, int depth, int rootIndex, int rootCount)
        {
            string area = CleanName(level.hubAreaName);
            if (!string.IsNullOrEmpty(area))
            {
                return area;
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

        private static string BuildSubtitle(Level level)
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

        /// <summary>
        /// A cell holding a box means "go inside that box". The landing cell is chosen inside
        /// the box's own level: Level.GetExitBlock() is the box block in the *parent* level,
        /// so its coordinates cannot be used inside the interior.
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
                if (current.BlocksWithThisAsTheirSubLevel == null
                    || current.BlocksWithThisAsTheirSubLevel.Count == 0)
                {
                    break;
                }

                Block parent;
                try
                {
                    parent = current.GetExitBlock();
                }
                catch
                {
                    break;
                }

                if (parent == null || parent.SubLevel != current)
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
