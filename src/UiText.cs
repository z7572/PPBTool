using System.Collections.Generic;

namespace PPBTool
{
    /// <summary>Small bilingual string table; follows the game's language preference.</summary>
    internal static class UiText
    {
        private static readonly Dictionary<string, string[]> Table =
            new Dictionary<string, string[]>
            {
                { "title", new[] { "传送地图  Teleport Map", "Teleport Map" } },
                { "scene", new[] { "场景", "Scene" } },
                { "level", new[] { "层级", "Level" } },
                { "close", new[] { "关闭 (Esc)", "Close (Esc)" } },
                { "legend_wall", new[] { "墙", "Wall" } },
                { "legend_box", new[] { "箱子", "Box" } },
                { "legend_portal", new[] { "传送门", "Portal" } },
                { "legend_player", new[] { "玩家", "Player" } },
                { "legend_target", new[] { "可落点", "Target" } },
                { "legend_cursor", new[] { "光标", "Cursor" } },
                { "hint", new[] { "方向键/WASD 移动光标 · PgUp/PgDn 切层 · Enter/左键 传送 · 滚轮缩放 · 右键拖动 · Esc 关闭", "Arrows/WASD move cursor · PgUp/PgDn change level · Enter/click teleport · wheel zoom · right-drag pan · Esc close" } },
                { "no_players", new[] { "找不到玩家", "Player not found" } },
                { "no_levels", new[] { "当前场景没有可扫描的层", "No levels found in this scene" } },
                { "empty_cell", new[] { "空格", "empty" } },
                { "enter_box", new[] { "进入箱子", "enter box" } },
                { "blocked", new[] { "不可传送", "not teleportable" } },
                { "will_enter", new[] { "将进入箱子并落在", "will enter box and land at" } },
                { "not_walkable", new[] { "该格不是可落点", "that cell is not a valid landing spot" } },
                { "no_target", new[] { "当前层没有可落点", "no teleportable cells in this level" } },
                { "teleported", new[] { "已传送", "Teleported" } },
                { "undo_hint", new[] { "已登记为可撤销操作", "recorded as an undoable turn" } },
                { "opened", new[] { "已扫描场景层级", "scene hierarchy scanned" } },
                { "current", new[] { "当前", "current" } },
                { "win_blocked_hub", new[] { "大厅里不能通关，请进入一个关卡", "cannot complete a level while in the hub" } },
                { "win_blocked_state", new[] { "当前状态不能通关", "cannot complete a level in the current state" } },
                { "win_blocked_busy", new[] { "动画/过场中，稍后再试", "busy with an animation or transition" } },
                { "win_done", new[] { "已触发通关结算", "level completion triggered" } },
                { "win_no_level", new[] { "没有加载关卡", "no level loaded" } },
            };

        private static string language;

        /// <summary>Re-read the game language (called when the browser opens).</summary>
        public static void Refresh()
        {
            language = null;
        }

        private static bool IsChinese
        {
            get
            {
                if (language == null)
                {
                    try
                    {
                        language = Prefs.Language ?? string.Empty;
                    }
                    catch
                    {
                        language = string.Empty;
                    }
                }

                if (language.Length == 0)
                {
                    return true;
                }

                string lower = language.ToLowerInvariant();
                return lower.Contains("zh") || lower.Contains("cn") || lower.Contains("chinese");
            }
        }

        public static string Get(string key)
        {
            string[] pair;
            if (!Table.TryGetValue(key, out pair))
            {
                return key;
            }

            return IsChinese ? pair[0] : pair[1];
        }
    }
}
