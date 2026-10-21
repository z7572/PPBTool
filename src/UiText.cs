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
                { "levels", new[] { "层级", "levels" } },
                { "close", new[] { "关闭 X", "Close X" } },
                { "back_parent", new[] { "← 回到引用处", "← Go to Reference" } },
                { "no_outer_reference", new[] { "没有可返回的外层引用", "No outer reference" } },
                { "reference_chooser", new[] { "选择引用", "Choose reference" } },
                { "hidden_reference", new[] { "该引用与其它箱子重叠，地图上不可点击", "Hidden overlap reference; not clickable on the map" } },
                { "nav_menu", new[] { "导航", "Navigation" } },
                { "back_view", new[] { "← 后退", "← Back" } },
                { "forward_view", new[] { "→ 前进", "→ Forward" } },
                { "go_to_player", new[] { "回到玩家", "Go to Player" } },
                { "no_back_history", new[] { "没有可后退的视图", "No back history" } },
                { "no_forward_history", new[] { "没有可前进的视图", "No forward history" } },
                { "inf_enter", new[] { "无限进入", "Infinite Enter" } },
                { "inf_exit", new[] { "无限退出", "Infinite Exit" } },
                { "not_generated", new[] { "（未生成）", " (not generated)" } },
                { "fit", new[] { "适应", "Fit" } },
                { "zoom_in", new[] { "放大 +", "Zoom +" } },
                { "zoom_out", new[] { "缩小 -", "Zoom -" } },
                { "hide_void", new[] { "隐藏虚空", "Hide void" } },
                { "show_void", new[] { "显示虚空", "Show void" } },
                { "collapse_all", new[] { "折叠全部", "Collapse all" } },
                { "expand_all", new[] { "展开全部", "Expand all" } },
                { "refresh", new[] { "刷新", "Refresh" } },
                { "level_thumbnails", new[] { "关卡缩略图", "Level thumbnails" } },
                { "legend_wall", new[] { "墙", "Wall" } },
                { "legend_box", new[] { "箱子", "Box" } },
                { "legend_portal", new[] { "传送门", "Portal" } },
                { "legend_player", new[] { "玩家", "Player" } },
                { "legend_target", new[] { "可落点", "Target" } },
                { "legend_cursor", new[] { "光标", "Cursor" } },
                { "legend_void", new[] { "虚空", "Void" } },
                { "legend_button", new[] { "按钮", "Button" } },
                { "legend_finish", new[] { "终点按钮", "Finish" } },
                { "legend_fast", new[] { "快速移动", "Fast travel" } },
                { "legend_break", new[] { "碎裂地砖", "Break" } },
                { "legend_info", new[] { "信息", "Info" } },
                { "legend_item", new[] { "收集品", "Item" } },
                { "hint", new[] { "方向键/WASD 移动光标 · Enter/左键 进入箱子或传送 · 左键/右键拖动 平移 · 滚轮缩放 · Backspace 上一级 · F 适应 · H 隐藏虚空 · Esc 关闭", "Arrows/WASD move cursor · Enter/click open box or teleport · drag to pan · wheel zoom · Backspace parent · F fit · H hide void · Esc close" } },
                { "no_players", new[] { "找不到玩家", "Player not found" } },
                { "no_levels", new[] { "当前场景没有可扫描的层", "No levels found in this scene" } },
                { "empty_cell", new[] { "空格", "empty" } },
                { "enter_box", new[] { "进入箱子", "open box" } },
                { "blocked", new[] { "不可传送", "not teleportable" } },
                { "will_enter", new[] { "打开层级", "open level" } },
                { "not_walkable", new[] { "该格不是可落点", "that cell is not a valid landing spot" } },
                { "no_target", new[] { "当前层没有可落点", "no teleportable cells in this level" } },
                { "teleported", new[] { "已传送", "Teleported" } },
                { "undo_hint", new[] { "已登记为可撤销操作", "recorded as an undoable turn" } },
                { "opened", new[] { "已扫描场景层级", "scene hierarchy scanned" } },
                { "current", new[] { "当前", "current" } },
                { "landing", new[] { "箱子落点", "landing" } },
                { "open_level", new[] { "打开层级", "open level" } },
                { "parent_missing", new[] { "已经在最顶层", "already at the top level" } },
                { "level_missing", new[] { "层级不在当前场景", "level is not in the current scene" } },
                { "map_blocked_state", new[] { "当前界面不能打开传送地图", "cannot open the map in this screen" } },
                { "confirm_title", new[] { "确认生成并进入？", "Generate and enter?" } },
                { "confirm_body", new[] { "这会修改当前场景：新增无限进入/退出容器/房间/块，且不可撤销。目标索引 #", "This modifies the current scene: adds Infinite Enter/Exit containers/rooms/blocks and cannot be undone. Target index #" } },
                { "confirm_yes", new[] { "确认生成并进入", "Generate and enter" } },
                { "cancel", new[] { "取消", "Cancel" } },
                { "gen_failed", new[] { "生成无限区失败", "failed to generate infinite zone" } },
                { "win_blocked_hub", new[] { "大厅里不能通关，请进入一个关卡", "cannot complete a level while in the hub" } },
                { "win_blocked_state", new[] { "当前状态不能通关", "cannot complete a level in the current state" } },
                { "win_blocked_busy", new[] { "动画/过场中，稍后再试", "busy with an animation or transition" } },
                { "win_done", new[] { "已触发通关结算", "level completion triggered" } },
                { "win_no_level", new[] { "没有加载关卡", "no level loaded" } },
                { "flags", new[] { "属性", "flags" } },
                { "hover", new[] { "悬停", "Hover" } },
                { "floor_gallery", new[] { "画廊地砖", "Gallery" } },
                { "floor_demo", new[] { "Demo 结束", "Demo end" } },
                { "floor_smile", new[] { "笑脸地砖", "Smile" } },
                { "floor_show", new[] { "展示地砖", "Show" } },
                { "floor_label", new[] { "标签", "Label" } },
            };

        private static string language;
        private static string languageMode = "auto";

        /// <summary>auto / zh / en. Auto follows the game's Prefs.Language.</summary>
        public static void SetLanguage(string mode)
        {
            string value = (mode ?? string.Empty).Trim().ToLowerInvariant();
            languageMode = value == "zh" || value == "en" ? value : "auto";
            language = null;
        }

        /// <summary>Re-read the game language when in auto mode.</summary>
        public static void Refresh()
        {
            if (languageMode == "auto")
            {
                language = null;
            }
        }

        public static bool IsChinese
        {
            get
            {
                if (languageMode == "zh")
                {
                    return true;
                }

                if (languageMode == "en")
                {
                    return false;
                }

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

        public static string ToggleLabel
        {
            get { return IsChinese ? "EN" : "中文"; }
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
