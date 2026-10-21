using System.Collections.Generic;

namespace PPBTool
{
    /// <summary>
    /// Null-safe access to the game's global state.
    ///
    /// World.FindPlayerBlock() indexes playerBlocks[0] directly and throws while a scene is
    /// loading (or when a level has no player), so every call site goes through here instead.
    /// </summary>
    internal static class GameState
    {
        public static Block Player
        {
            get
            {
                List<Block> players = World.playerBlocks;
                return players != null && players.Count > 0 ? players[0] : null;
            }
        }

        public static bool HasPlayer
        {
            get { return Player != null; }
        }
    }
}
