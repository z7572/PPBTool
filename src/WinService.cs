namespace PPBTool
{
    /// <summary>
    /// "Complete the current level" via the game's own victory flow.
    ///
    /// Mimics UndoManager.CheckWinCondition: Winning=true and WinCount=0, so
    /// World.UpdateInner plays the normal ~1.5s gradual white/brighten animation before
    /// saving, unlocking and moving to the next level. (The game's debug key sets
    /// WinCount=1.5f to skip that animation, which we deliberately do not do.)
    /// </summary>
    internal static class WinService
    {
        public static bool TryWin(out string message)
        {
            message = null;

            if (World.levels == null || World.levels.Count == 0 || !GameState.HasPlayer)
            {
                message = UiText.Get("win_no_level");
                return false;
            }

            if (World.InHub())
            {
                message = UiText.Get("win_blocked_hub");
                return false;
            }

            if (World.State != World.WS.Playing)
            {
                message = UiText.Get("win_blocked_state");
                return false;
            }

            if (World.Winning || World.EndZoom || World.Transitioning)
            {
                message = UiText.Get("win_blocked_busy");
                return false;
            }

            // Same state CheckWinCondition would set after a real solve: WinCount stays 0
            // so the next UpdateInner frames run the gradual whitening animation.
            World.Winning = true;
            World.WinCount = 0f;
            World.WinAnimT = 0f;
            World.WinSoundPlayed = false;

            message = UiText.Get("win_done");
            Log.Info("Win triggered for level '" + World.currentLevelName + "'");
            return true;
        }
    }
}
