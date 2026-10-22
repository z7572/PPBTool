using System;
using System.Collections.Generic;

namespace PPBTool
{
    /// <summary>Read-only snapshot of the block traits the original game actually exposes.</summary>
    internal sealed class BlockTraits
    {
        public Block Block;
        public bool IsRoot;
        public bool IsExitBlock;
        public bool IsClone;
        public bool IsInfEnter;
        public bool IsInfExit;
        public bool IsPlayer;
        public bool IsPossessable;
        public bool IsFlip;
        public bool IsCounting;
        public int PlayerOrder;
        public int SpecialEffect;
        public int Solved;
        public int Required;
        public List<InfLink> InfAccess = new List<InfLink>();

        public static BlockTraits From(Block block)
        {
            var traits = new BlockTraits
            {
                Block = block,
                IsRoot = block != null && block.OuterLevel == null,
                IsInfEnter = block != null && block.IsSomeInfEnterBlock,
                IsInfExit = block != null && block.IsSomeInfExitBlock,
                IsPlayer = block != null && block.IsPlayer,
                IsPossessable = block != null && block.Possessable,
                IsFlip = block != null && block.FlipH,
                IsCounting = block != null && block.numRequired > 0,
                PlayerOrder = block != null ? block.PlayerOrder : 0,
                SpecialEffect = block != null ? block.SpecialEffect : 0,
                Solved = block != null ? block.numSolved : 0,
                Required = block != null ? block.numRequired : 0,
            };

            if (block != null && block.SubLevel != null)
            {
                List<Block> refs = block.SubLevel.BlocksWithThisAsTheirSubLevel;
                Block primary = refs != null && refs.Count > 0 ? refs[0] : null;
                traits.IsExitBlock = primary == block;
                traits.IsClone = primary != null && primary != block;
            }

            if (block != null && block.SubLevel != null && World.blocks != null)
            {
                foreach (Block other in World.blocks)
                {
                    if (other != null
                        && other.IsSomeInfEnterBlock
                        && other.SubLevel == block.SubLevel)
                    {
                        AddInfLink(traits, other, true);
                    }
                }
            }

            if (block != null && block.IsSomeInfExitBlock)
            {
                AddInfLink(traits, block, false);
            }

            return traits;
        }

        private static void AddInfLink(BlockTraits traits, Block block, bool enter)
        {
            Level owner;
            int degree;
            bool resolved = LevelGraph.TryGetInfOwner(block, out owner, out degree);
            if (!resolved)
            {
                owner = null;
                degree = -1;
            }

            foreach (InfLink existing in traits.InfAccess)
            {
                if (existing.Enter == enter
                    && existing.Source == owner
                    && existing.Index == degree)
                {
                    return;
                }
            }

            traits.InfAccess.Add(new InfLink
            {
                Enter = enter,
                Index = degree,
                Source = owner,
                Block = block,
                Container = block.OuterLevel,
                Target = block.SubLevel,
                TargetX = block.xpos,
                TargetY = block.ypos,
            });
        }

        public string Compact(Func<Level, string> levelLabel)
        {
            var parts = new List<string>();
            if (IsPlayer)
            {
                parts.Add(UiText.Get("player"));
                if (PlayerOrder > 0)
                {
                    parts.Add(UiText.Get("player_order") + " " + PlayerOrder);
                }
            }

            if (IsPossessable && !IsPlayer)
            {
                parts.Add(UiText.Get("possessable"));
            }

            if (IsExitBlock)
            {
                parts.Add(UiText.Get("primary_exit"));
            }
            else if (IsClone)
            {
                parts.Add(UiText.Get("clone"));
            }

            foreach (InfLink link in InfAccess)
            {
                parts.Add(InfLabel(link, levelLabel));
            }

            if (IsFlip)
            {
                parts.Add(UiText.Get("flip"));
            }

            if (IsCounting)
            {
                parts.Add(UiText.Get("counting") + " " + Solved + "/" + Required);
            }

            if (SpecialEffect > 0)
            {
                parts.Add(UiText.Get("se") + SpecialEffect);
            }

            if (IsRoot)
            {
                parts.Add(UiText.Get("no_outer_level"));
            }

            return string.Join(" · ", parts.ToArray());
        }

        private static string InfLabel(
            InfLink link, Func<Level, string> levelLabel)
        {
            string label = link.Enter ? "Infinite Enter" : "Infinite Exit";
            if (link.Index >= 0)
            {
                label += " #" + link.Index;
            }

            if (link.Source != null && levelLabel != null)
            {
                label += " of " + levelLabel(link.Source);
            }

            return label;
        }
    }

    internal static class SpecialEffectCatalog
    {
        private static readonly string[] Names =
        {
            "None",
            "Focus",
            "Right flip",
            "Left flip",
            "Unused",
            "Unused",
            "Hub Intro block",
            "Unused",
            "Mechanic glyphs",
            "Open void sides",
            "Hub Intro ref",
            "Hide in ASCII-grid",
            "Multiplayer camera",
            "Disable glyphs",
        };

        public static string Name(int id)
        {
            if (id >= 0 && id < Names.Length)
            {
                return UiText.Get("se_name_" + id);
            }

            return "Unknown";
        }

        public static bool IsKnown(int id)
        {
            return id >= 0 && id < Names.Length;
        }
    }
}
