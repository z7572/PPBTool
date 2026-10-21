namespace PPBTool
{
    /// <summary>
    /// "Complete the current level" via the game's own victory flow.
    ///
    /// The game itself does this in World.KeyboardShortcuts (debug key "I"), so this is
    /// the same code path: set Winning/WinCount and let World.UpdateInner run its normal
    /// 1.5s celebration, save, unlock and GoToNextLevel logic.
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

            // Mirror the game's debug shortcut. Everything after this point (achievements,
            // save file, next level/hub transition, CustomHubs compatibility) is vanilla.
            World.Winning = true;
            World.WinCount = 1.5f;
            World.WinAnimT = 0f;
            World.WinSoundPlayed = false;

            message = UiText.Get("win_done");
            Log.Info("Win triggered for level '" + World.currentLevelName + "'");
            return true;
        }
    }
}
