using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PPBTool
{
    /// <summary>
    /// Full-screen IMGUI map browser. While open, the Harmony prefix on World.UpdateInner
    /// freezes the game, so all input here is consumed exclusively by the browser.
    /// </summary>
    internal sealed class TeleportOverlay
    {
        private const float HeaderH = 58f;
        private const float ToolbarH = 42f;
        private const float FooterH = 64f;
        private const float Margin = 12f;
        private const float Gap = 10f;
        private const float RowH = 24f;
        private const float MinZoom = 0.1f;
        private const float MaxZoom = 10f;

        // ------------------------------------------------------------------ colours

        private static readonly Color Backdrop = new Color(0.035f, 0.04f, 0.055f, 0.97f);
        private static readonly Color Panel = new Color(0.09f, 0.10f, 0.13f, 1f);
        private static readonly Color Header = new Color(0.13f, 0.15f, 0.19f, 1f);
        private static readonly Color Bar = new Color(0.11f, 0.13f, 0.16f, 1f);
        private static readonly Color Edge = new Color(0.24f, 0.28f, 0.34f, 1f);
        private static readonly Color Muted = new Color(0.62f, 0.67f, 0.74f, 1f);
        private static readonly Color PlayerCol = new Color(0.96f, 0.84f, 0.22f, 1f);
        private static readonly Color WallCol = new Color(0.20f, 0.22f, 0.27f, 1f);
        private static readonly Color EmptyCol = new Color(0.14f, 0.16f, 0.20f, 1f);
        private static readonly Color BoxCol = new Color(0.93f, 0.57f, 0.22f, 1f);
        private static readonly Color BoxBorder = new Color(0.12f, 0.08f, 0.04f, 1f);
        private static readonly Color PossessCol = new Color(0.62f, 0.42f, 0.90f, 1f);
        private static readonly Color PortalCol = new Color(0.68f, 0.42f, 0.95f, 1f);
        private static readonly Color TargetCol = new Color(0.35f, 0.82f, 0.45f, 1f);
        private static readonly Color CursorCol = new Color(1f, 1f, 1f, 1f);
        private static readonly Color VoidBg = new Color(0.02f, 0.02f, 0.04f, 1f);
        private static readonly Color VoidText = new Color(0.45f, 0.50f, 0.60f, 1f);

        private static readonly Color FloorButton = new Color(0.85f, 0.52f, 0.18f, 0.60f);
        private static readonly Color FloorFinish = new Color(0.20f, 0.72f, 0.78f, 0.60f);
        private static readonly Color FloorPortal = new Color(0.55f, 0.30f, 0.85f, 0.60f);
        private static readonly Color FloorInfo = new Color(0.38f, 0.52f, 0.78f, 0.50f);
        private static readonly Color FloorItem = new Color(0.95f, 0.80f, 0.25f, 0.60f);
        private static readonly Color FloorBreak = new Color(0.75f, 0.30f, 0.30f, 0.45f);
        private static readonly Color FloorFast = new Color(0.30f, 0.85f, 0.50f, 0.55f);
        private static readonly Color FloorGallery = new Color(0.80f, 0.40f, 0.70f, 0.45f);
        private static readonly Color FloorDemo = new Color(0.90f, 0.90f, 0.90f, 0.50f);
        private static readonly Color FloorSmile = new Color(0.95f, 0.90f, 0.40f, 0.55f);
        private static readonly Color FloorShow = new Color(0.40f, 0.60f, 0.95f, 0.55f);
        private static readonly Color FloorLabel = new Color(0.60f, 0.60f, 0.65f, 0.30f);

        // ------------------------------------------------------------------- state

        private readonly ModConfig config;
        private readonly TeleportService teleporter;
        private readonly LevelGraph graph = new LevelGraph();
        private readonly List<LevelNode> rows = new List<LevelNode>();
        private readonly Dictionary<Level, bool> expanded = new Dictionary<Level, bool>();
        private readonly Dictionary<string, Texture> portalCache =
            new Dictionary<string, Texture>();

        private Texture2D white;
        private Texture2D texButton;
        private Texture2D texButtonHover;
        private Texture2D texButtonActive;
        private Texture2D texRowHover;
        private Texture2D texRowSelected;
        private Texture2D texTrack;
        private Texture2D texThumb;
        private Texture2D texClear;
        private Texture texEyesLarge;
        private Texture texEyesSmall;
        private Texture texInfo;
        private Texture texSmile;
        private Texture texUpArrow;
        private Texture texDownArrow;

        private GUIStyle titleStyle;
        private GUIStyle sectionStyle;
        private GUIStyle labelStyle;
        private GUIStyle smallStyle;
        private GUIStyle tinyStyle;
        private GUIStyle tinyCenterStyle;
        private GUIStyle buttonStyle;
        private GUIStyle buttonOnStyle;
        private GUIStyle arrowStyle;
        private GUIStyle catchStyle;
        private GUIStyle statusStyle;
        private GUIStyle rightStyle;
        private GUIStyle bannerStyle;
        private GUIStyle centerLabelStyle;

        private bool open;
        private int suppressFrames;
        private World.WS savedState;
        private LevelNode selectedNode;
        private bool hideVoid;
        private bool usePalette;
        private bool portalArt;
        private int cursorX;
        private int cursorY;
        private float zoom = 1f;
        private Vector2 pan;
        private float treeScroll;
        private Rect lastTreeView;
        private bool draggingThumb;
        private float thumbGrab;
        private Rect lastCanvas;
        private Rect lastGrid;
        private Vector2 lastMouse = new Vector2(-1f, -1f);
        private bool cursorWasVisible = true;
        private bool mapPressed;
        private bool mapDragged;
        private Vector2 mapPressPos;
        private Vector2 mapPressPan;
        private string message = string.Empty;
        private float messageUntil;
        private bool messageIsError;
        private string sceneAtBuild = string.Empty;
        private int rootsAtBuild = -1;
        private int levelsAtBuild = -1;

        private bool showInfMenu;
        private bool showInfConfirm;
        private bool infMenuEnter;
        private int pendingIndex;
        private string infMenuTitle = string.Empty;
        private List<InfLink> infMenuLinks = new List<InfLink>();

        private Level landingLevel;
        private int landingX;
        private int landingY;
        private bool landingValid;

        public TeleportOverlay(ModConfig config, TeleportService teleporter)
        {
            this.config = config;
            this.teleporter = teleporter;
        }

        public bool IsOpen
        {
            get { return open; }
        }

        /// <summary>
        /// True while the game's own UpdateInner must be skipped: either the browser is open,
        /// or it was just closed and the key/click that closed it must not leak into gameplay.
        /// </summary>
        public bool BlocksGameUpdate
        {
            get { return open || suppressFrames > 0; }
        }

        /// <summary>Called every frame by the driver, before World.UpdateInner runs.</summary>
        public void FrameUpdate()
        {
            if (suppressFrames > 0)
            {
                suppressFrames--;
            }

            if (open)
            {
                Tick();
            }
        }

        public void Toggle()
        {
            if (open)
            {
                Close();
            }
            else
            {
                Show();
            }
        }

        public void Show()
        {
            if (open)
            {
                return;
            }

            if (World.State != World.WS.Playing && World.State != World.WS.Paused)
            {
                Notify(UiText.Get("map_blocked_state"), true);
                return;
            }

            if (!GameState.HasPlayer)
            {
                Notify(UiText.Get("no_players"), true);
                return;
            }

            UiText.Refresh();
            portalCache.Clear();
            savedState = World.State;
            hideVoid = config.HideVoidBoxes;
            usePalette = config.UseLevelPalette;
            portalArt = config.PortalThumbnails;
            showInfMenu = false;
            showInfConfirm = false;
            titleStyle = null;
            RebuildGraph(false);
            if (rows.Count == 0)
            {
                Notify(UiText.Get("no_levels"), true);
                return;
            }

            open = true;
            suppressFrames = 0;
            cursorWasVisible = Cursor.visible;
            Cursor.visible = true;
            zoom = Mathf.Clamp(config.CellSize / 24f, MinZoom, MaxZoom);
            pan = Vector2.zero;
            treeScroll = 0f;
            message = string.Empty;

            if (graph.PlayerNode != null)
            {
                ExpandAncestors(graph.PlayerNode);
                selectedNode = graph.PlayerNode;
                PlaceCursorAtDefault(selectedNode);
            }

            FlattenTree();
            ScrollToSelection();

            Mouse mouse = Mouse.current;
            if (mouse != null)
            {
                Vector2 p = mouse.position.ReadValue();
                lastMouse = new Vector2(p.x, Screen.height - p.y);
            }
            else
            {
                lastMouse = new Vector2(-1f, -1f);
            }

            Log.Info("Teleport map opened: " + rows.Count + " visible rows, scene '" + sceneAtBuild + "'");
            if (config.LogSceneTree)
            {
                LogTreeLayout();
            }
        }

        public void Close()
        {
            if (!open)
            {
                return;
            }

            open = false;
            showInfMenu = false;
            showInfConfirm = false;
            suppressFrames = 2;
            Cursor.visible = cursorWasVisible;
            World.State = savedState == World.WS.Initializing ? World.WS.Playing : savedState;
            Log.Info("Teleport map closed");
        }

        public void Notify(string text, bool isError)
        {
            message = text ?? string.Empty;
            messageIsError = isError;
            messageUntil = Time.unscaledTime + 3f;
        }

        private void ClearMessage()
        {
            message = string.Empty;
        }

        // ------------------------------------------------------------------ graph

        private void RebuildGraph(bool keepSelection)
        {
            bool sceneChanged = !string.Equals(sceneAtBuild, World.currentLevelName, StringComparison.Ordinal);
            if (sceneChanged)
            {
                expanded.Clear();
                portalCache.Clear();
            }

            landingLevel = null;

            graph.Build();
            sceneAtBuild = World.currentLevelName;
            rootsAtBuild = graph.Roots.Count;
            levelsAtBuild = World.levels == null ? 0 : World.levels.Count;

            LevelNode previous = keepSelection ? selectedNode : null;
            selectedNode = null;

            if (previous != null)
            {
                selectedNode = graph.FindNode(previous.Level);
            }

            if (selectedNode == null && graph.PlayerNode != null)
            {
                selectedNode = graph.PlayerNode;
            }

            if (selectedNode == null && graph.Roots.Count > 0)
            {
                selectedNode = graph.Roots[0];
            }

            if (selectedNode != null && (previous == null || previous.Level != selectedNode.Level))
            {
                PlaceCursorAtDefault(selectedNode);
            }

            FlattenTree();
        }

        private void FlattenTree()
        {
            rows.Clear();
            foreach (LevelNode root in graph.Roots)
            {
                AddRows(root);
            }
        }

        private void AddRows(LevelNode node)
        {
            if (hideVoid && IsHiddenByVoid(node))
            {
                return;
            }

            rows.Add(node);

            if (!IsExpanded(node))
            {
                return;
            }

            foreach (LevelNode child in node.Children)
            {
                AddRows(child);
            }
        }

        private bool IsExpanded(LevelNode node)
        {
            bool value;
            if (expanded.TryGetValue(node.Level, out value))
            {
                return value;
            }

            if (node.Level.InfZone)
            {
                return false;
            }

            if (node.Depth == 0)
            {
                return true;
            }

            return IsOnPlayerPath(node);
        }

        private bool IsOnPlayerPath(LevelNode node)
        {
            LevelNode current = graph.PlayerNode;
            int guard = 0;
            while (current != null && guard++ < 64)
            {
                if (current == node)
                {
                    return true;
                }

                current = current.ParentNode;
            }

            return false;
        }

        private void ToggleExpanded(LevelNode node)
        {
            if (node == null)
            {
                return;
            }

            expanded[node.Level] = !IsExpanded(node);
            FlattenTree();
            ClampTreeScroll();
        }

        private void ExpandAncestors(LevelNode node)
        {
            LevelNode current = node != null ? node.ParentNode : null;
            int guard = 0;
            while (current != null && guard++ < 64)
            {
                expanded[current.Level] = true;
                current = current.ParentNode;
            }
        }

        private void CollapseAll()
        {
            foreach (LevelNode node in graph.AllNodes)
            {
                if (node.Children.Count > 0)
                {
                    expanded[node.Level] = false;
                }
            }

            FlattenTree();
            treeScroll = 0f;
        }

        private void ExpandAll()
        {
            foreach (LevelNode node in graph.AllNodes)
            {
                if (node.Children.Count > 0)
                {
                    expanded[node.Level] = true;
                }
            }

            FlattenTree();
        }

        private bool IsHiddenByVoid(LevelNode node)
        {
            if (!hideVoid || node == null)
            {
                return false;
            }

            if (node.Level.InfZone)
            {
                return true;
            }

            return node.ParentBlock != null
                && node.ParentBlock.OuterLevel != null
                && node.ParentBlock.OuterLevel.InfZone;
        }

        private void NavigateToNode(LevelNode node)
        {
            if (node == null)
            {
                return;
            }

            ExpandAncestors(node);
            if (hideVoid && IsHiddenByVoid(node))
            {
                hideVoid = false;
                config.HideVoidBoxes = false;
            }

            FlattenTree();
            selectedNode = node;
            zoom = 1f;
            pan = Vector2.zero;
            PlaceCursorAtDefault(node);
            ScrollToSelection();
            showInfMenu = false;
            showInfConfirm = false;
            ClearMessage();
        }

        private void NavigateToLevel(Level level)
        {
            LevelNode node = graph.FindNode(level);
            if (node == null)
            {
                Notify(UiText.Get("level_missing"), true);
                return;
            }

            NavigateToNode(node);
        }

        private void GoParent()
        {
            if (selectedNode == null || selectedNode.ParentNode == null)
            {
                Notify(UiText.Get("parent_missing"), false);
                return;
            }

            NavigateToNode(selectedNode.ParentNode);
        }

        private void PlaceCursorAtDefault(LevelNode node)
        {
            if (node == null || node.Level == null)
            {
                return;
            }

            Level level = node.Level;
            int x = level.width / 2;
            int y = level.height / 2;

            Block player = GameState.Player;
            if (player != null && player.OuterLevel == level)
            {
                x = player.xpos;
                y = player.ypos;
            }
            else
            {
                int lx;
                int ly;
                if (LevelGraph.TryFindNearestWalkable(level, x, y, out lx, out ly))
                {
                    x = lx;
                    y = ly;
                }
            }

            SetCursor(x, y);
        }

        // ------------------------------------------------------------------ input

        public void Tick()
        {
            if (!open)
            {
                return;
            }

            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return;
            }

            Cursor.visible = true;

            if (World.currentLevelName != sceneAtBuild
                || graph.Roots.Count != rootsAtBuild
                || (World.levels != null ? World.levels.Count : 0) != levelsAtBuild)
            {
                RebuildGraph(true);
            }

            if (keyboard.escapeKey.wasPressedThisFrame)
            {
                if (showInfConfirm || showInfMenu)
                {
                    showInfConfirm = false;
                    showInfMenu = false;
                    return;
                }

                Close();
                return;
            }

            if (config.TeleportPressed())
            {
                Close();
                return;
            }

            if (showInfMenu)
            {
                return;
            }

            int dx = 0;
            int dy = 0;
            if (Pressed(keyboard, Key.LeftArrow) || Pressed(keyboard, Key.A)) dx -= 1;
            if (Pressed(keyboard, Key.RightArrow) || Pressed(keyboard, Key.D)) dx += 1;
            if (Pressed(keyboard, Key.UpArrow) || Pressed(keyboard, Key.W)) dy += 1;
            if (Pressed(keyboard, Key.DownArrow) || Pressed(keyboard, Key.S)) dy -= 1;
            if (dx != 0 || dy != 0)
            {
                MoveCursor(dx, dy);
            }

            if (Pressed(keyboard, Key.PageUp))
            {
                SelectRow(rows.IndexOf(selectedNode) - 1);
            }

            if (Pressed(keyboard, Key.PageDown))
            {
                SelectRow(rows.IndexOf(selectedNode) + 1);
            }

            if (Pressed(keyboard, Key.Backspace))
            {
                GoParent();
            }

            if (Pressed(keyboard, Key.F))
            {
                FitMap();
            }

            if (Pressed(keyboard, Key.H))
            {
                ToggleHideVoid();
            }

            if (Pressed(keyboard, Key.Enter) || Pressed(keyboard, Key.NumpadEnter) || Pressed(keyboard, Key.Space))
            {
                ConfirmCursor();
            }

            Mouse mouse = Mouse.current;
            if (mouse == null)
            {
                return;
            }

            Vector2 screenMouse = mouse.position.ReadValue();
            var guiMouse = new Vector2(screenMouse.x, Screen.height - screenMouse.y);
            float wheel = mouse.scroll.ReadValue().y;
            if (Mathf.Abs(wheel) > 0.01f)
            {
                float notches = Mathf.Clamp(wheel / 120f, -3f, 3f);
                if (lastTreeView.Contains(guiMouse))
                {
                    treeScroll -= notches * 60f;
                    ClampTreeScroll();
                }
                else if (lastCanvas.Contains(guiMouse))
                {
                    ZoomAt(guiMouse, notches > 0f ? 0.9f : 1.1f);
                }
            }
        }

        private static bool Pressed(Keyboard keyboard, Key key)
        {
            return keyboard[key].wasPressedThisFrame;
        }

        private void SelectRow(int index)
        {
            if (index < 0 || index >= rows.Count)
            {
                return;
            }

            NavigateToNode(rows[index]);
        }

        private void MoveCursor(int dx, int dy)
        {
            if (selectedNode == null || selectedNode.Level == null)
            {
                return;
            }

            SetCursor(cursorX + dx, cursorY + dy);
        }

        private void SetCursor(int x, int y)
        {
            if (selectedNode == null || selectedNode.Level == null)
            {
                cursorX = 0;
                cursorY = 0;
                return;
            }

            Level level = selectedNode.Level;
            cursorX = Mathf.Clamp(x, 0, Mathf.Max(0, level.width - 1));
            cursorY = Mathf.Clamp(y, 0, Mathf.Max(0, level.height - 1));
        }

        private void ConfirmCursor()
        {
            if (selectedNode == null || selectedNode.Level == null)
            {
                return;
            }

            HandleMapClick(selectedNode.Level, cursorX, cursorY);
        }

        private void HandleMapClick(Level level, int x, int y)
        {
            if (level == null || !level.InBounds(x, y))
            {
                return;
            }

            Block block = level.blocks[x, y];
            if (block != null && block.SubLevel != null)
            {
                NavigateToLevel(block.SubLevel);
                return;
            }

            if (block == null && LevelGraph.IsWalkableCell(level, x, y))
            {
                DoTeleport(new TeleportCell
                {
                    Level = level,
                    X = x,
                    Y = y,
                    ViaBox = false,
                });
                return;
            }

            Notify(UiText.Get("not_walkable"), true);
        }

        private void DoTeleport(TeleportCell cell)
        {
            string error;
            if (!teleporter.TryTeleport(cell, out error))
            {
                Notify(error, true);
                return;
            }

            string note = UiText.Get("teleported") + " " + cell;
            if (config.Undoable)
            {
                note += "  (" + UiText.Get("undo_hint") + ")";
            }

            Notify(note, false);
            if (config.CloseAfterTeleport)
            {
                Close();
            }
            else
            {
                RebuildGraph(true);
            }
        }

        // ------------------------------------------------------------ inf / filter

        private void ToggleHideVoid()
        {
            hideVoid = !hideVoid;
            config.HideVoidBoxes = hideVoid;
            FlattenTree();
            ClampTreeScroll();
        }

        private void TogglePalette()
        {
            usePalette = !usePalette;
            config.UseLevelPalette = usePalette;
        }

        private void TogglePortalArt()
        {
            portalArt = !portalArt;
            config.PortalThumbnails = portalArt;
        }

        private void ShowInfMenu(bool enter)
        {
            if (selectedNode == null || selectedNode.Level == null)
            {
                return;
            }

            infMenuEnter = enter;
            infMenuLinks = graph.GetInfLinks(selectedNode.Level)
                .Where(l => l.Enter == enter)
                .ToList();
            infMenuTitle = UiText.Get("enter_inf")
                + " (" + (enter ? UiText.Get("inf_enter") : UiText.Get("inf_exit")) + ")"
                + " · " + selectedNode.Title;
            mapPressed = false;
            mapDragged = false;
            showInfMenu = true;
            showInfConfirm = false;
        }

        private void GoInfLink(InfLink link)
        {
            showInfMenu = false;
            showInfConfirm = false;
            FocusInfLink(link);
        }

        private void FocusInfLink(InfLink link)
        {
            if (link == null || link.Target == null)
            {
                Notify(UiText.Get("level_missing"), true);
                return;
            }

            NavigateToLevel(link.Target);
            if (!link.Enter && selectedNode != null && selectedNode.Level == link.Target)
            {
                SetCursor(link.TargetX, link.TargetY);
            }
        }

        private void BeginGenerateInf(bool enter)
        {
            if (selectedNode == null || selectedNode.Level == null)
            {
                return;
            }

            pendingIndex = LevelGraph.NextInfIndex(selectedNode.Level, enter);
            infMenuEnter = enter;
            showInfConfirm = true;
        }

        private void GenerateInf(bool enter, int index)
        {
            showInfConfirm = false;
            showInfMenu = false;
            if (selectedNode == null || selectedNode.Level == null)
            {
                return;
            }

            Level level = selectedNode.Level;
            try
            {
                if (enter)
                {
                    LoadLevel.CreateInfEnterZone(level, index);
                }
                else
                {
                    LoadLevel.CreateInfExitZone(level, index);
                }

                World.ComputeBlockLists();
                RebuildGraph(true);

                InfLink link = graph.GetInfLinks(level)
                    .FirstOrDefault(l => l.Enter == enter && l.Index == index);
                if (link == null || link.Target == null)
                {
                    Notify(UiText.Get("gen_failed"), true);
                    return;
                }

                FocusInfLink(link);
            }
            catch (Exception ex)
            {
                Log.Error("Failed to generate infinity zone", ex);
                Notify(UiText.Get("gen_failed"), true);
            }
        }

        // ------------------------------------------------------------------- draw

        public void OnGUI()
        {
            EnsureTheme();
            GUI.depth = -100;

            if (!open)
            {
                DrawToast();
                return;
            }

            float w = Screen.width;
            float h = Screen.height;

            Fill(new Rect(0f, 0f, w, h), Backdrop);
            DrawHeader(new Rect(0f, 0f, w, HeaderH));
            DrawToolbar(new Rect(0f, HeaderH, w, ToolbarH));
            if (!open)
            {
                return;
            }

            float bodyTop = HeaderH + ToolbarH + Margin;
            float bodyH = Mathf.Max(100f, h - FooterH - bodyTop - Margin);
            float treeW = Mathf.Clamp(w * 0.30f, 320f, 470f);
            var treeRect = new Rect(Margin, bodyTop, treeW, bodyH);
            var mapRect = new Rect(treeRect.xMax + Gap, bodyTop, w - treeW - Margin * 2f - Gap, bodyH);

            DrawTree(treeRect);
            if (open)
            {
                DrawMap(mapRect);
            }

            if (open)
            {
                DrawFooter(new Rect(Margin, h - FooterH, w - Margin * 2f, FooterH - Margin));
            }

            if (open && (showInfMenu || showInfConfirm))
            {
                DrawInfMenu(w, h);
            }
        }

        private void DrawToast()
        {
            if (string.IsNullOrEmpty(message) || Time.unscaledTime >= messageUntil)
            {
                return;
            }

            float width = Mathf.Min(760f, Screen.width - 40f);
            var rect = new Rect((Screen.width - width) * 0.5f, 18f, width, 36f);
            Fill(rect, new Color(0.07f, 0.08f, 0.11f, 0.95f));
            Border(rect, messageIsError ? new Color(1f, 0.45f, 0.4f, 1f) : new Color(0.5f, 0.9f, 0.6f, 1f), 1f);
            GUI.Label(new Rect(rect.x + 14f, rect.y + 4f, rect.width - 28f, 28f), message, statusStyle);
        }

        private void DrawHeader(Rect rect)
        {
            Fill(rect, Header);
            Fill(new Rect(rect.x, rect.yMax - 1f, rect.width, 1f), Edge);

            GUI.Label(new Rect(rect.x + 16f, rect.y + 4f, 360f, 28f), UiText.Get("title"), titleStyle);

            string sceneName = sceneAtBuild == "custom_level" ? graph.SceneLabel : sceneAtBuild;
            string scene = UiText.Get("scene") + ": " + sceneName
                + "    " + UiText.Get("levels") + ": " + graph.AllNodes.Count()
                + " / " + rows.Count;
            GUI.Label(new Rect(rect.x + 16f, rect.y + 32f, 470f, 22f), scene, smallStyle);

            if (selectedNode != null)
            {
                float x = rect.x + 430f;
                float width = Mathf.Max(120f, rect.width - x - 130f);
                GUI.Label(new Rect(x, rect.y + 5f, width, 24f),
                    UiText.Get("level") + ": " + selectedNode.Path, labelStyle);
                string sub = selectedNode.Subtitle;
                if (!string.IsNullOrEmpty(selectedNode.Tags))
                {
                    sub = "[" + selectedNode.Tags + "]  " + sub;
                }

                GUI.Label(new Rect(x, rect.y + 31f, width, 20f), sub, smallStyle);
            }

            if (GUI.Button(new Rect(rect.xMax - 104f, rect.y + 13f, 90f, 30f), UiText.Get("close"), buttonStyle))
            {
                Close();
            }
        }

        private void DrawToolbar(Rect rect)
        {
            Fill(rect, Bar);
            Fill(new Rect(rect.x, rect.yMax - 1f, rect.width, 1f), Edge);

            float x = Margin;
            float y = rect.y + 6f;

            x = ToolButton(x, y, UiText.Get("back_parent"),
                selectedNode != null && selectedNode.ParentNode != null, false, GoParent) + 8f;
            Fill(new Rect(x - 4f, y + 2f, 1f, 24f), Edge);

            x = ToolButton(x, y, UiText.Get("inf_enter"), selectedNode != null, false,
                () => ShowInfMenu(true)) + 6f;
            x = ToolButton(x, y, UiText.Get("inf_exit"), selectedNode != null, false,
                () => ShowInfMenu(false)) + 8f;

            x = ToolButton(x, y, UiText.Get("fit"), true, false, FitMap) + 6f;
            x = ToolButton(x, y, "-", true, false, () => ZoomBy(1f / 1.25f)) + 4f;
            x = ToolButton(x, y, "+", true, false, () => ZoomBy(1.25f)) + 8f;

            x = ToolButton(x, y,
                hideVoid ? UiText.Get("show_void") : UiText.Get("hide_void"),
                true, hideVoid, ToggleHideVoid) + 8f;
            x = ToolButton(x, y, UiText.Get("palette"), true, usePalette, TogglePalette) + 6f;
            x = ToolButton(x, y, UiText.Get("portal_art"), true, portalArt, TogglePortalArt) + 8f;

            x = ToolButton(x, y, UiText.Get("collapse_all"), true, false, CollapseAll) + 6f;
            x = ToolButton(x, y, UiText.Get("expand_all"), true, false, ExpandAll) + 6f;
            ToolButton(x, y, UiText.Get("refresh"), true, false, () => RebuildGraph(true));
        }

        private float ToolButton(float x, float y, string text, bool enabled, bool on, Action action)
        {
            float width = buttonStyle.CalcSize(new GUIContent(text)).x + 16f;
            var rect = new Rect(x, y, width, 30f);
            bool old = GUI.enabled;
            GUI.enabled = enabled && !showInfMenu && !showInfConfirm;
            if (GUI.Button(rect, text, on ? buttonOnStyle : buttonStyle))
            {
                action();
            }

            GUI.enabled = old;
            return x + width;
        }

        // ------------------------------------------------------------------- tree

        private void DrawTree(Rect panel)
        {
            Fill(panel, Panel);
            Border(panel, Edge, 1f);

            GUI.Label(new Rect(panel.x + 10f, panel.y + 5f, panel.width - 20f, 20f),
                UiText.Get("levels") + "  (" + rows.Count + ")", sectionStyle);

            Rect view = new Rect(panel.x + 6f, panel.y + 28f, panel.width - 20f, panel.height - 34f);
            lastTreeView = view;
            if (rows.Count == 0)
            {
                GUI.Label(new Rect(view.x + 6f, view.y + 6f, view.width - 12f, 24f), UiText.Get("no_levels"), smallStyle);
                return;
            }

            float contentH = rows.Count * RowH;
            float maxScroll = Mathf.Max(0f, contentH - view.height);
            treeScroll = Mathf.Clamp(treeScroll, 0f, maxScroll);

            Event e = Event.current;
            Vector2 localMouse = e != null ? e.mousePosition - view.position : new Vector2(-1000f, -1000f);
            bool mouseInView = e != null && view.Contains(e.mousePosition);
            LevelNode deferredSelect = null;
            LevelNode deferredToggle = null;

            GUI.BeginGroup(view);
            for (int i = 0; i < rows.Count; i++)
            {
                float y = i * RowH - treeScroll;
                if (y + RowH < 0f)
                {
                    continue;
                }

                if (y > view.height)
                {
                    break;
                }

                LevelNode node = rows[i];
                var row = new Rect(0f, y, view.width, RowH - 1f);
                bool selected = node == selectedNode;
                bool hover = mouseInView && row.Contains(localMouse);

                if (selected)
                {
                    Fill(row, new Color(0.18f, 0.31f, 0.40f, 1f));
                }
                else if (hover)
                {
                    Fill(row, new Color(0.15f, 0.17f, 0.21f, 1f));
                }

                bool treeInput = !showInfMenu && !showInfConfirm;
                if (node.Children.Count > 0)
                {
                    var arrow = new Rect(4f + node.Depth * 13f, y + 4f, 14f, 14f);
                    if (treeInput && GUI.Button(arrow, IsExpanded(node) ? "-" : "+", arrowStyle))
                    {
                        deferredToggle = node;
                    }
                }

                var hitX = node.Children.Count > 0 ? 20f + node.Depth * 13f : 4f;
                var hit = new Rect(hitX, y, Mathf.Max(1f, view.width - hitX), RowH - 1f);
                if (treeInput && GUI.Button(hit, GUIContent.none, catchStyle))
                {
                    deferredSelect = node;
                }

                float textX = 24f + node.Depth * 13f;
                Color oldColor = GUI.color;
                if (node.ContainsPlayer)
                {
                    GUI.color = PlayerCol;
                }

                GUI.Label(new Rect(textX, y, Mathf.Max(1f, view.width - textX - 80f), RowH - 1f),
                    node.Title, labelStyle);
                GUI.color = oldColor;

                if (!string.IsNullOrEmpty(node.Tags))
                {
                    float tw = labelStyle.CalcSize(new GUIContent(node.Title)).x;
                    float tagsX = textX + tw + 6f;
                    if (tagsX < view.width - 90f)
                    {
                        GUI.Label(new Rect(tagsX, y, view.width - tagsX - 80f, RowH - 1f),
                            "[" + node.Tags + "]", smallStyle);
                    }
                }

                if (node.ContainsPlayer)
                {
                    GUI.Label(new Rect(view.width - 74f, y, 70f, RowH - 1f), "< " + UiText.Get("current"), rightStyle);
                }
            }

            GUI.EndGroup();

            if (maxScroll > 0f)
            {
                DrawTreeScrollbar(panel, view, contentH, maxScroll);
            }

            // Mutating rows while iterating it would corrupt the draw pass; do it after.
            if (deferredToggle != null)
            {
                ToggleExpanded(deferredToggle);
            }
            else if (deferredSelect != null)
            {
                NavigateToNode(deferredSelect);
            }
        }

        private void DrawTreeScrollbar(Rect panel, Rect view, float contentH, float maxScroll)
        {
            var track = new Rect(panel.xMax - 11f, view.y, 6f, view.height);
            DrawTextureTinted(track, texTrack, Color.white);

            float thumbH = Mathf.Max(28f, view.height * view.height / Mathf.Max(1f, contentH));
            float t = maxScroll > 0f ? treeScroll / maxScroll : 0f;
            float thumbY = view.y + (view.height - thumbH) * t;
            var thumb = new Rect(track.x, thumbY, track.width, thumbH);
            DrawTextureTinted(thumb, texThumb, Color.white);

            Event e = Event.current;
            if (e == null || showInfMenu || showInfConfirm)
            {
                return;
            }

            Vector2 mouse = e.mousePosition;
            if (e.type == EventType.MouseDown && e.button == 0)
            {
                if (thumb.Contains(mouse))
                {
                    draggingThumb = true;
                    thumbGrab = mouse.y - thumb.y;
                    e.Use();
                }
                else if (track.Contains(mouse))
                {
                    float target = (mouse.y - track.y - thumbH * 0.5f) / Mathf.Max(1f, track.height - thumbH);
                    treeScroll = Mathf.Clamp(target * maxScroll, 0f, maxScroll);
                    draggingThumb = true;
                    thumbGrab = thumbH * 0.5f;
                    e.Use();
                }
            }
            else if (e.type == EventType.MouseDrag && draggingThumb && e.button == 0)
            {
                float target = (mouse.y - track.y - thumbGrab) / Mathf.Max(1f, track.height - thumbH);
                treeScroll = Mathf.Clamp(target * maxScroll, 0f, maxScroll);
                e.Use();
            }
            else if (e.type == EventType.MouseUp && e.button == 0)
            {
                draggingThumb = false;
            }
        }

        private void ClampTreeScroll()
        {
            float viewH = lastTreeView.height > 0f ? lastTreeView.height : 400f;
            float maxScroll = Mathf.Max(0f, rows.Count * RowH - viewH);
            treeScroll = Mathf.Clamp(treeScroll, 0f, maxScroll);
        }

        private void ScrollToSelection()
        {
            int index = rows.IndexOf(selectedNode);
            if (index < 0)
            {
                return;
            }

            float viewH = lastTreeView.height > 0f ? lastTreeView.height : 400f;
            treeScroll = Mathf.Clamp(
                index * RowH - viewH * 0.5f + RowH,
                0f,
                Mathf.Max(0f, rows.Count * RowH - viewH));
        }

        // -------------------------------------------------------------------- map

        private void DrawMap(Rect panel)
        {
            Fill(panel, Panel);
            Border(panel, Edge, 1f);

            if (selectedNode == null || selectedNode.Level == null)
            {
                GUI.Label(new Rect(panel.x + 16f, panel.y + 14f, panel.width - 32f, 24f),
                    UiText.Get("no_levels"), labelStyle);
                return;
            }

            Level level = selectedNode.Level;
            EnsureLanding(level);

            var detailRect = new Rect(panel.x + 6f, panel.y + 5f, panel.width - 12f, 24f);
            Fill(detailRect, Bar);
            GUI.Label(new Rect(detailRect.x + 8f, detailRect.y + 2f, detailRect.width - 16f, 20f),
                BuildDetail(level), labelStyle);

            var canvas = new Rect(panel.x + 8f, panel.y + 32f, panel.width - 16f, panel.height - 32f - 38f);
            lastCanvas = canvas;

            Fill(canvas, MapBackground(level));
            if (level.InfZone)
            {
                Color old = GUI.color;
                GUI.color = new Color(VoidText.r, VoidText.g, VoidText.b, 0.12f);
                GUI.Label(canvas, "VOID", bannerStyle);
                GUI.color = old;
            }

            float fit = Mathf.Min(
                canvas.width / Mathf.Max(1, level.width),
                canvas.height / Mathf.Max(1, level.height));
            int cell = Mathf.Clamp(Mathf.FloorToInt(fit * 0.92f * zoom), 2, 400);
            float gridW = cell * level.width;
            float gridH = cell * level.height;

            float maxPanX = Mathf.Max(0f, (gridW + canvas.width) * 0.5f - 24f);
            float maxPanY = Mathf.Max(0f, (gridH + canvas.height) * 0.5f - 24f);
            pan.x = Mathf.Clamp(pan.x, -maxPanX, maxPanX);
            pan.y = Mathf.Clamp(pan.y, -maxPanY, maxPanY);

            float originX = Mathf.Round(canvas.center.x - gridW * 0.5f + pan.x);
            float originY = Mathf.Round(canvas.center.y - gridH * 0.5f + pan.y);
            lastGrid = new Rect(originX, originY, gridW, gridH);

            if (!showInfMenu && !showInfConfirm)
            {
                HandleMapInput(canvas, level, originX, originY, cell);
            }

            if (!open || selectedNode == null || selectedNode.Level != level)
            {
                return;
            }
            int firstCol = Mathf.Clamp(Mathf.FloorToInt((canvas.x - originX) / cell), 0, level.width - 1);
            int lastCol = Mathf.Clamp(Mathf.CeilToInt((canvas.xMax - originX) / cell), 0, level.width - 1);
            int firstRow = Mathf.Clamp(Mathf.FloorToInt((canvas.y - originY) / cell), 0, level.height - 1);
            int lastRow = Mathf.Clamp(Mathf.CeilToInt((canvas.yMax - originY) / cell), 0, level.height - 1);

            Block player = GameState.Player;
            int playerX = player != null && player.OuterLevel == level ? player.xpos : int.MinValue;
            int playerY = player != null && player.OuterLevel == level ? player.ypos : int.MinValue;

            for (int row = firstRow; row <= lastRow; row++)
            {
                int y = level.height - 1 - row;
                for (int x = firstCol; x <= lastCol; x++)
                {
                    var r = new Rect(originX + x * cell, originY + row * cell, cell, cell);
                    if (!Inside(r, canvas))
                    {
                        continue;
                    }

                    Floor floor = level.floors[x, y];
                    Block block = level.blocks[x, y];
                    if (floor != null)
                    {
                        DrawFloor(r, floor, block != null, cell);
                    }
                    else
                    {
                        Fill(r, EmptyCol);
                    }

                    if (block != null)
                    {
                        DrawBlock(r, block, cell);
                    }

                    if (cell >= 6f)
                    {
                        Border(r, new Color(0f, 0f, 0f, 0.22f), 1f);
                    }

                    if (x == playerX && y == playerY)
                    {
                        Ring(r, PlayerCol, Mathf.Max(2f, cell * 0.10f));
                        Fill(Centered(r, Mathf.Max(2f, cell * 0.20f)), PlayerCol);
                    }
                }
            }

            if (landingValid)
            {
                int row = level.height - 1 - landingY;
                var landing = new Rect(originX + landingX * cell, originY + row * cell, cell, cell);
                if (Inside(landing, canvas) && !(landingX == playerX && landingY == playerY))
                {
                    Ring(landing, TargetCol, Mathf.Max(2f, cell * 0.08f));
                    Cross(landing, TargetCol);
                }
            }

            var gridRect = new Rect(originX - 1f, originY - 1f, gridW + 2f, gridH + 2f);
            if (Inside(gridRect, canvas))
            {
                Border(gridRect, new Color(0.36f, 0.42f, 0.50f, 0.9f), 1f);
            }

            DrawCursor(level, originX, originY, cell, canvas);
            DrawLegend(panel, canvas);
        }

        private void DrawFloor(Rect r, Floor floor, bool hasBlock, int cell)
        {
            Color color = FloorBaseColor(floor.Type);
            if (usePalette)
            {
                color = PaletteFloorColor(floor, color);
            }

            if (hasBlock)
            {
                Fill(r, color);
                return;
            }

            switch (floor.Type)
            {
                case Floor.FloorType.Button:
                    DrawButtonBorder(r, color);
                    break;

                case Floor.FloorType.PlayerButton:
                    DrawButtonBorder(r, color);
                    DrawGameTexture(r, texEyesLarge, texEyesSmall,
                        Color.Lerp(color, Color.white, 0.45f), 1f);
                    break;

                case Floor.FloorType.Info:
                    DrawButtonBorder(r, color);
                    DrawGameTexture(r, texInfo, texInfo, Color.white, 0.9f);
                    break;

                case Floor.FloorType.LevelPortal:
                    DrawPortal(r, floor, cell);
                    break;

                case Floor.FloorType.Smile:
                    if (texSmile != null)
                    {
                        DrawGameTexture(r, texSmile, texSmile, Color.Lerp(color, Color.white, 0.6f), 0.9f);
                    }
                    else
                    {
                        Fill(Centered(r, Mathf.Min(r.width, r.height) * 0.6f), color);
                    }
                    break;

                case Floor.FloorType.FastTravel:
                    DrawFastTravel(r, color);
                    break;

                case Floor.FloorType.BreakFloor:
                    DrawBreakGrid(r, color);
                    break;

                case Floor.FloorType.GalleryFloor:
                    DrawGallery(r, color);
                    break;

                case Floor.FloorType.Show:
                    DrawBreakGrid(r, Color.Lerp(color, Color.white, 0.35f));
                    break;

                case Floor.FloorType.DemoEnd:
                    Fill(r, new Color(0.06f, 0.06f, 0.08f, 1f));
                    Border(r, color, 1f);
                    if (cell >= 22)
                    {
                        GUI.Label(r, "DEMO", tinyCenterStyle);
                    }
                    break;

                case Floor.FloorType.Item:
                    Fill(r, new Color(0.10f, 0.09f, 0.06f, 1f));
                    Fill(Centered(r, cell * 0.34f), color);
                    Border(Centered(r, cell * 0.34f), Color.white, 1f);
                    break;

                case Floor.FloorType.Label:
                    Fill(r, new Color(0.08f, 0.08f, 0.10f, 1f));
                    if (cell >= 16)
                    {
                        GUI.Label(r, "T", tinyCenterStyle);
                    }
                    break;

                default:
                    Fill(r, color);
                    break;
            }
        }

        private void DrawButtonBorder(Rect r, Color color)
        {
            float padX = r.width * 0.0664f;
            float padY = r.height * 0.0664f;
            float thickX = Mathf.Max(1f, Mathf.Round(r.width * 0.0725f));
            float thickY = Mathf.Max(1f, Mathf.Round(r.height * 0.0725f));

            float x = r.x + padX;
            float y = r.y + padY;
            float w = Mathf.Max(1f, r.width - padX * 2f);
            float h = Mathf.Max(1f, r.height - padY * 2f);

            Fill(new Rect(x, y, w, thickY), color);
            Fill(new Rect(x, y + h - thickY, w, thickY), color);
            Fill(new Rect(x, y + thickY, thickX, Mathf.Max(0f, h - thickY * 2f)), color);
            Fill(new Rect(x + w - thickX, y + thickY, thickX, Mathf.Max(0f, h - thickY * 2f)), color);
        }

        private void DrawPortal(Rect r, Floor floor, int cell)
        {
            if (portalArt)
            {
                Texture texture = GetPortalTexture(floor.SceneName);
                if (texture != null)
                {
                    if (floor.Hard != 0)
                    {
                        Fill(r, new Color(0.65f, 0.22f, 0.22f, 0.55f));
                    }

                    Color tint = !floor.Unlocked
                        ? new Color(0.45f, 0.45f, 0.45f, 1f)
                        : (floor.Won ? Color.white : new Color(0.70f, 0.82f, 1f, 1f));
                    var inner = floor.Hard != 0 ? Inset(r, 0.1f) : r;
                    DrawTextureTinted(inner, texture, tint);
                    if (floor.Unlocked && !floor.Won && cell >= 10)
                    {
                        Fill(Centered(r, cell * 0.18f), Color.white);
                    }

                    return;
                }
            }

            Fill(Centered(r, Mathf.Min(r.width, r.height) * 0.55f), FloorPortal);
            Border(Centered(r, Mathf.Min(r.width, r.height) * 0.55f),
                new Color(0.9f, 0.85f, 1f, 0.9f), 1f);
            if (floor.Unlocked && !floor.Won && cell >= 10)
            {
                Fill(Centered(r, cell * 0.16f), Color.white);
            }
        }

        private void DrawFastTravel(Rect r, Color color)
        {
            float w = r.width * 0.45f;
            float h = r.height * 0.45f;
            float cx = r.center.x;
            float cy = r.center.y - h * 0.5f;

            var up = new Rect(cx - w * 0.5f, cy - h * 0.5f, w, h);
            var down = new Rect(cx - w * 0.5f, cy + h * 0.5f, w, h);

            if (texUpArrow != null)
            {
                DrawTextureTinted(up, texUpArrow, color);
                DrawTextureTinted(down, texDownArrow != null ? texDownArrow : texUpArrow, color);
            }
            else
            {
                Fill(new Rect(cx - w * 0.25f, up.y + h * 0.2f, w * 0.5f, h * 0.6f), color);
                Fill(new Rect(cx - w * 0.25f, down.y + h * 0.2f, w * 0.5f, h * 0.6f), color);
            }
        }

        private void DrawBreakGrid(Rect r, Color color)
        {
            float w = r.width * 0.65f;
            float h = r.height * 0.65f;
            float gap = w * 0.13f;
            float cellW = (w - gap * 2f) / 3f;
            float cellH = (h - gap * 2f) / 3f;
            float x0 = r.center.x - w * 0.5f;
            float y0 = r.center.y - h * 0.5f;

            for (int i = 0; i < 3; i++)
            {
                for (int j = 0; j < 3; j++)
                {
                    Fill(new Rect(
                        x0 + i * (cellW + gap),
                        y0 + j * (cellH + gap),
                        cellW,
                        cellH), color);
                }
            }
        }

        private void DrawGallery(Rect r, Color color)
        {
            float w = r.width * 0.70f;
            float h = r.height * 0.70f;
            Fill(new Rect(r.center.x - w * 0.5f, r.center.y - h * 0.5f - h * 0.15f, w, h * 0.70f), color);
            Fill(new Rect(r.center.x - w * 0.60f, r.center.y + h * 0.35f, w * 1.20f, h * 0.15f), color);
        }

        private void DrawBlock(Rect r, Block block, int cell)
        {
            Color color = BlockColor(block);
            Fill(r, color);

            if (block.SubLevel != null)
            {
                Border(r, BoxBorder, 2f);
                if (cell >= 18f)
                {
                    string badge = BadgeText(block);
                    if (badge.Length > 0)
                    {
                        float badgeH = Mathf.Max(11f, cell * 0.36f);
                        Fill(new Rect(r.x, r.yMax - badgeH, r.width, badgeH), new Color(0f, 0f, 0f, 0.35f));
                        GUI.Label(new Rect(r.x, r.yMax - badgeH, r.width, badgeH), badge, tinyCenterStyle);
                    }

                    if (cell >= 30f)
                    {
                        GUI.Label(r, ">", centerLabelStyle);
                    }
                }
                else
                {
                    if (block.FlipH)
                    {
                        Fill(new Rect(r.xMax - 4f, r.y, 3f, 3f), new Color(1f, 0.95f, 0.4f, 1f));
                    }

                    if (block.IsSomeInfEnterBlock || block.IsSomeInfExitBlock || block.GeneratedInf)
                    {
                        Fill(new Rect(r.x, r.y, 3f, 3f), new Color(0.85f, 0.9f, 1f, 1f));
                    }
                }
            }
        }

        private Color BlockColor(Block block)
        {
            if (!usePalette)
            {
                if (block.IsPlayer)
                {
                    return PlayerCol;
                }

                if (block.SubLevel != null)
                {
                    Color boxColor = LevelGraph.IsCloneBox(block)
                        ? Color.Lerp(BoxCol, Color.white, 0.35f)
                        : BoxCol;
                    if (block.IsSomeInfExitBlock)
                    {
                        boxColor = Color.Lerp(boxColor, Color.black, 0.35f);
                    }

                    return boxColor;
                }

                return block.Possessable ? PossessCol : WallCol;
            }

            float sat = block.sat;
            float val = block.val;
            if (block.IsSomeInfExitBlock)
            {
                val *= 0.55f;
            }
            else if (LevelGraph.IsCloneBox(block))
            {
                val = Mathf.Clamp01(val + 0.42f);
                sat *= 0.76f;
            }

            Color color = Color.HSVToRGB(
                Mathf.Repeat(block.hue, 1f),
                Mathf.Clamp01(sat),
                Mathf.Clamp01(val));

            if (block.IsPlayer)
            {
                color = Color.Lerp(color, PlayerCol, 0.55f);
            }

            return color;
        }

        private static string BadgeText(Block block)
        {
            string text = string.Empty;
            if (block.FlipH)
            {
                text += "F";
            }

            if (LevelGraph.IsCloneBox(block))
            {
                text += "C";
            }

            if (block.IsSomeInfEnterBlock)
            {
                text += "I";
            }

            if (block.IsSomeInfExitBlock)
            {
                text += "O";
            }

            if (block.GeneratedInf)
            {
                text += "*";
            }

            if (block.Possessable)
            {
                text += "P";
            }

            if (block.Locked)
            {
                text += "L";
            }

            return text;
        }

        private void DrawCursor(Level level, float originX, float originY, int cell, Rect canvas)
        {
            if (!level.InBounds(cursorX, cursorY))
            {
                return;
            }

            int row = level.height - 1 - cursorY;
            var r = new Rect(originX + cursorX * cell, originY + row * cell, cell, cell);
            if (!Inside(r, canvas))
            {
                return;
            }

            Block block = level.blocks[cursorX, cursorY];
            Color color;
            string glyph;
            if (block != null && block.SubLevel != null)
            {
                color = new Color(0.35f, 0.85f, 1f, 1f);
                glyph = ">";
            }
            else if (block == null && LevelGraph.IsWalkableCell(level, cursorX, cursorY))
            {
                color = TargetCol;
                glyph = "+";
            }
            else if (block == null)
            {
                color = PortalCol;
                glyph = "P";
            }
            else
            {
                color = new Color(1f, 0.45f, 0.4f, 1f);
                glyph = "x";
            }

            float pulse = 0.6f + 0.4f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 4f));
            Border(r, new Color(color.r, color.g, color.b, pulse), Mathf.Max(2f, cell * 0.09f));
            if (cell >= 14f)
            {
                GUI.Label(r, glyph, centerLabelStyle);
            }
        }

        private void DrawLegend(Rect panel, Rect canvas)
        {
            var area = new Rect(panel.x + 8f, canvas.yMax + 3f, panel.width - 16f, 32f);
            Fill(area, Bar);

            float x = area.x + 8f;
            float y = area.y + 2f;
            float rowY = y;
            LegendItem(ref x, ref rowY, area, WallCol, UiText.Get("legend_wall"));
            LegendItem(ref x, ref rowY, area, BoxCol, UiText.Get("legend_box"));
            LegendItem(ref x, ref rowY, area, PlayerCol, UiText.Get("legend_player"));
            LegendItem(ref x, ref rowY, area, PortalCol, UiText.Get("legend_portal"));
            LegendItem(ref x, ref rowY, area, TargetCol, UiText.Get("legend_target"));
            LegendItem(ref x, ref rowY, area, VoidText, UiText.Get("legend_void"));
            LegendItem(ref x, ref rowY, area, FloorButton, UiText.Get("legend_button"));
            LegendItem(ref x, ref rowY, area, FloorFinish, UiText.Get("legend_finish"));
            LegendItem(ref x, ref rowY, area, FloorFast, UiText.Get("legend_fast"));
            LegendItem(ref x, ref rowY, area, FloorBreak, UiText.Get("legend_break"));
        }

        private void LegendItem(ref float x, ref float y, Rect area, Color color, string text)
        {
            float width = smallStyle.CalcSize(new GUIContent(text)).x + 30f;
            if (x + width > area.xMax - 8f)
            {
                x = area.x + 8f;
                y += 15f;
            }

            Fill(new Rect(x, y + 3f, 10f, 10f), color);
            GUI.Label(new Rect(x + 14f, y - 1f, width - 14f, 15f), text, smallStyle);
            x += width + 8f;
        }

        private string BuildDetail(Level level)
        {
            string coord = "(" + cursorX + ", " + cursorY + ")";
            Block block = level.InBounds(cursorX, cursorY) ? level.blocks[cursorX, cursorY] : null;
            Floor floor = level.InBounds(cursorX, cursorY) ? level.floors[cursorX, cursorY] : null;

            string info;
            if (block != null)
            {
                if (block.SubLevel != null)
                {
                    LevelNode target = graph.FindNode(block.SubLevel);
                    string title = target != null
                        ? target.Title
                        : LevelGraph.DescribeLevel(block.SubLevel);
                    info = UiText.Get("open_level") + ": " + title;
                }
                else
                {
                    info = LevelGraph.BlockFlags(block);
                }
            }
            else if (floor != null)
            {
                info = FloorName(floor.Type);
                if (floor.Type == Floor.FloorType.LevelPortal && !string.IsNullOrEmpty(floor.SceneName))
                {
                    info += " → " + floor.SceneName;
                }
            }
            else
            {
                info = UiText.Get("empty_cell");
            }

            if (block == null && LevelGraph.IsWalkableCell(level, cursorX, cursorY))
            {
                info += " · " + UiText.Get("legend_target");
            }

            if (landingValid)
            {
                info += " · " + UiText.Get("landing") + " (" + landingX + "," + landingY + ")";
            }

            return coord + "  " + info;
        }

        private void EnsureLanding(Level level)
        {
            if (landingLevel == level)
            {
                return;
            }

            landingLevel = level;
            landingValid = LevelGraph.TryFindNearestWalkable(
                level, level.width / 2, level.height / 2, out landingX, out landingY);
        }

        private static Color FloorBaseColor(Floor.FloorType type)
        {
            switch (type)
            {
                case Floor.FloorType.Button: return FloorButton;
                case Floor.FloorType.PlayerButton: return FloorFinish;
                case Floor.FloorType.LevelPortal: return FloorPortal;
                case Floor.FloorType.Info: return FloorInfo;
                case Floor.FloorType.Item: return FloorItem;
                case Floor.FloorType.Label: return FloorLabel;
                case Floor.FloorType.BreakFloor: return FloorBreak;
                case Floor.FloorType.FastTravel: return FloorFast;
                case Floor.FloorType.GalleryFloor: return FloorGallery;
                case Floor.FloorType.DemoEnd: return FloorDemo;
                case Floor.FloorType.Smile: return FloorSmile;
                case Floor.FloorType.Show: return FloorShow;
                default: return FloorLabel;
            }
        }

        private static string FloorName(Floor.FloorType type)
        {
            switch (type)
            {
                case Floor.FloorType.Button: return UiText.Get("legend_button");
                case Floor.FloorType.PlayerButton: return UiText.Get("legend_finish");
                case Floor.FloorType.LevelPortal: return UiText.Get("legend_portal");
                case Floor.FloorType.Info: return UiText.Get("legend_info");
                case Floor.FloorType.Item: return UiText.Get("legend_item");
                case Floor.FloorType.BreakFloor: return UiText.Get("legend_break");
                case Floor.FloorType.FastTravel: return UiText.Get("legend_fast");
                case Floor.FloorType.GalleryFloor: return UiText.Get("floor_gallery");
                case Floor.FloorType.DemoEnd: return UiText.Get("floor_demo");
                case Floor.FloorType.Smile: return UiText.Get("floor_smile");
                case Floor.FloorType.Show: return UiText.Get("floor_show");
                case Floor.FloorType.Label: return UiText.Get("floor_label");
                default: return type.ToString();
            }
        }

        private Color MapBackground(Level level)
        {
            if (level.InfZone)
            {
                return VoidBg;
            }

            if (!usePalette)
            {
                return new Color(0.06f, 0.07f, 0.09f, 1f);
            }

            Block exit = LevelGraph.ExitBlockOf(level);
            if (exit == null)
            {
                return new Color(0.06f, 0.07f, 0.09f, 1f);
            }

            return Color.HSVToRGB(
                Mathf.Repeat(exit.hue, 1f),
                Mathf.Clamp01(exit.sat * 0.45f),
                0.06f);
        }

        private Color PaletteFloorColor(Floor floor, Color fallback)
        {
            Block exit = LevelGraph.ExitBlockOf(floor.OuterLevel);
            if (exit == null)
            {
                return fallback;
            }

            Color color = Color.HSVToRGB(
                Mathf.Repeat(exit.hue, 1f),
                Mathf.Clamp01(exit.sat * 0.60f),
                0.90f);
            color.a = fallback.a;
            return color;
        }

        private void HandleMapInput(Rect canvas, Level level, float originX, float originY, int cell)
        {
            Event e = Event.current;
            if (e == null)
            {
                return;
            }

            Vector2 mouse = e.mousePosition;

            if (e.type == EventType.MouseDown && e.button == 0 && canvas.Contains(mouse))
            {
                mapPressed = true;
                mapDragged = false;
                mapPressPos = mouse;
                mapPressPan = pan;
            }

            if (e.type == EventType.MouseDrag && e.button == 0 && mapPressed)
            {
                if ((mouse - mapPressPos).sqrMagnitude > 16f)
                {
                    mapDragged = true;
                }

                if (mapDragged)
                {
                    pan = mapPressPan + (mouse - mapPressPos);
                    e.Use();
                }
            }

            if (e.type == EventType.MouseUp && e.button == 0)
            {
                if (mapPressed && !mapDragged && canvas.Contains(mouse))
                {
                    int x;
                    int y;
                    if (TryCell(level, originX, originY, cell, mouse, out x, out y))
                    {
                        cursorX = x;
                        cursorY = y;
                        HandleMapClick(level, x, y);
                        e.Use();
                    }
                }

                mapPressed = false;
                mapDragged = false;
            }

            if (e.type == EventType.MouseDrag && (e.button == 1 || e.button == 2))
            {
                pan += e.delta;
                e.Use();
            }

            if (e.type == EventType.Repaint && canvas.Contains(mouse)
                && (mouse - lastMouse).sqrMagnitude > 0.01f)
            {
                lastMouse = mouse;
                int x;
                int y;
                if (TryCell(level, originX, originY, cell, mouse, out x, out y))
                {
                    cursorX = x;
                    cursorY = y;
                }
            }
        }

        private static bool TryCell(
            Level level, float originX, float originY, int cell, Vector2 mouse, out int x, out int y)
        {
            x = 0;
            y = 0;
            if (level == null || cell <= 0)
            {
                return false;
            }

            x = Mathf.FloorToInt((mouse.x - originX) / cell);
            int row = Mathf.FloorToInt((mouse.y - originY) / cell);
            y = level.height - 1 - row;
            return level.InBounds(x, y);
        }

        private void DrawFooter(Rect rect)
        {
            Fill(rect, Panel);
            Border(rect, Edge, 1f);
            GUI.Label(new Rect(rect.x + 12f, rect.y + 6f, rect.width - 24f, 22f), UiText.Get("hint"), smallStyle);

            if (!string.IsNullOrEmpty(message) && Time.unscaledTime < messageUntil)
            {
                Color old = GUI.color;
                GUI.color = messageIsError ? new Color(1f, 0.5f, 0.45f, 1f) : new Color(0.55f, 0.95f, 0.65f, 1f);
                GUI.Label(new Rect(rect.x + 12f, rect.y + 30f, rect.width - 24f, 24f), message, statusStyle);
                GUI.color = old;
            }
            else if (selectedNode != null)
            {
                string status = UiText.Get("level") + ": " + selectedNode.Title
                    + "   " + selectedNode.Subtitle;
                GUI.Label(new Rect(rect.x + 12f, rect.y + 30f, rect.width - 24f, 24f), status, smallStyle);
            }
        }

        // -------------------------------------------------------------- inf popup

        private void DrawInfMenu(float w, float h)
        {
            bool confirm = showInfConfirm;
            int count = infMenuLinks.Count;
            float panelW = 620f;
            float panelH = confirm ? 190f : 90f + (count + 1) * 34f;
            var panel = new Rect((w - panelW) * 0.5f, (h - panelH) * 0.5f, panelW, panelH);

            var closeRect = new Rect(panel.xMax - 90f, panel.yMax - 38f, 74f, 28f);
            var yesRect = new Rect(panel.x + 16f, panel.yMax - 46f, 260f, 32f);
            var noRect = new Rect(panel.xMax - 156f, panel.yMax - 46f, 140f, 32f);
            var linkRects = new List<Rect>();
            float linkY = panel.y + 44f;
            foreach (InfLink link in infMenuLinks)
            {
                linkRects.Add(new Rect(panel.x + 16f, linkY, panel.width - 32f, 28f));
                linkY += 34f;
            }

            var genRect = new Rect(panel.x + 16f, linkY, panel.width - 32f, 28f);
            int next = selectedNode != null && selectedNode.Level != null
                ? LevelGraph.NextInfIndex(selectedNode.Level, infMenuEnter)
                : 0;

            Event e = Event.current;
            Vector2 mouse = e != null ? e.mousePosition : Vector2.zero;

            // One full-screen control swallows the click; panel buttons are drawn manually
            // so this modal cannot be bypassed by the tree/map drawn earlier.
            if (GUI.Button(new Rect(0f, 0f, w, h), GUIContent.none, catchStyle))
            {
                if (!panel.Contains(mouse))
                {
                    showInfMenu = false;
                    showInfConfirm = false;
                    return;
                }

                if (confirm)
                {
                    if (yesRect.Contains(mouse))
                    {
                        GenerateInf(infMenuEnter, pendingIndex);
                        return;
                    }

                    if (noRect.Contains(mouse))
                    {
                        showInfMenu = false;
                        showInfConfirm = false;
                        return;
                    }
                }
                else
                {
                    for (int i = 0; i < linkRects.Count; i++)
                    {
                        if (linkRects[i].Contains(mouse))
                        {
                            GoInfLink(infMenuLinks[i]);
                            return;
                        }
                    }

                    if (selectedNode != null && genRect.Contains(mouse))
                    {
                        BeginGenerateInf(infMenuEnter);
                        return;
                    }

                    if (closeRect.Contains(mouse))
                    {
                        showInfMenu = false;
                        showInfConfirm = false;
                        return;
                    }
                }

                return;
            }

            Fill(new Rect(0f, 0f, w, h), new Color(0f, 0f, 0f, 0.45f));
            Fill(panel, Header);
            Border(panel, Edge, 1f);

            if (confirm)
            {
                GUI.Label(new Rect(panel.x + 16f, panel.y + 10f, panel.width - 32f, 26f),
                    UiText.Get("confirm_title"), titleStyle);
                GUI.Label(new Rect(panel.x + 16f, panel.y + 44f, panel.width - 32f, 60f),
                    UiText.Get("confirm_body") + pendingIndex, labelStyle);
                DrawMenuButton(yesRect, UiText.Get("confirm_yes"), yesRect.Contains(mouse));
                DrawMenuButton(noRect, UiText.Get("cancel"), noRect.Contains(mouse));
                return;
            }

            GUI.Label(new Rect(panel.x + 16f, panel.y + 10f, panel.width - 32f, 26f), infMenuTitle, titleStyle);
            for (int i = 0; i < linkRects.Count; i++)
            {
                DrawMenuButton(linkRects[i], infMenuLinks[i].MenuLabel, linkRects[i].Contains(mouse));
            }

            if (selectedNode != null)
            {
                string label = UiText.Get("enter_inf") + " #" + next + UiText.Get("not_generated");
                DrawMenuButton(genRect, label, genRect.Contains(mouse));
            }

            DrawMenuButton(closeRect, UiText.Get("close"), closeRect.Contains(mouse));
        }

        private void DrawMenuButton(Rect rect, string text, bool hovered)
        {
            GUI.Label(rect, text, hovered ? buttonOnStyle : buttonStyle);
        }

        // ------------------------------------------------------------------ theme

        private void EnsureTheme()
        {
            if (titleStyle != null)
            {
                return;
            }

            if (white == null)
            {
                white = Texture2D.whiteTexture;
                texButton = MakeTex(new Color(0.17f, 0.19f, 0.24f, 1f));
                texButtonHover = MakeTex(new Color(0.23f, 0.27f, 0.33f, 1f));
                texButtonActive = MakeTex(new Color(0.16f, 0.36f, 0.48f, 1f));
                texRowHover = MakeTex(new Color(0.15f, 0.17f, 0.21f, 1f));
                texRowSelected = MakeTex(new Color(0.18f, 0.31f, 0.40f, 1f));
                texTrack = MakeTex(new Color(0.08f, 0.09f, 0.12f, 1f));
                texThumb = MakeTex(new Color(0.32f, 0.37f, 0.45f, 1f));
                texClear = MakeTex(new Color(0f, 0f, 0f, 0f));
            }

            if (texEyesLarge == null)
            {
                texEyesLarge = GetPrivateTexture("PlayerButtonOnlyEyesTex");
                texEyesSmall = GetPrivateTexture("PlayerButtonOnlyEyesSmallTex");
                texInfo = GetPrivateTexture("InfoFloorTex");
                texSmile = GetPrivateTexture("SmileFloorTex");
                texUpArrow = GetPrivateTexture("UpArrow");
                texDownArrow = GetPrivateTexture("DownArrow");
            }

            Font font = GetGameFont();

            titleStyle = new GUIStyle(GUI.skin.label)
            {
                font = font,
                fontSize = 22,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
            };
            titleStyle.normal.textColor = Color.white;

            sectionStyle = new GUIStyle(GUI.skin.label)
            {
                font = font,
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
            };
            sectionStyle.normal.textColor = new Color(0.80f, 0.85f, 0.92f, 1f);

            labelStyle = new GUIStyle(GUI.skin.label)
            {
                font = font,
                fontSize = 15,
                alignment = TextAnchor.MiddleLeft,
            };
            labelStyle.normal.textColor = new Color(0.90f, 0.93f, 0.97f, 1f);

            smallStyle = new GUIStyle(labelStyle) { fontSize = 12 };
            smallStyle.normal.textColor = Muted;

            tinyStyle = new GUIStyle(labelStyle) { fontSize = 10 };
            tinyStyle.normal.textColor = new Color(0.95f, 0.95f, 0.95f, 1f);

            tinyCenterStyle = new GUIStyle(tinyStyle) { alignment = TextAnchor.MiddleCenter };
            centerLabelStyle = new GUIStyle(labelStyle)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold,
            };
            centerLabelStyle.normal.textColor = new Color(1f, 1f, 1f, 0.95f);

            statusStyle = new GUIStyle(labelStyle) { fontSize = 14, fontStyle = FontStyle.Bold };
            rightStyle = new GUIStyle(smallStyle) { alignment = TextAnchor.MiddleRight };
            rightStyle.normal.textColor = PlayerCol;

            bannerStyle = new GUIStyle(GUI.skin.label)
            {
                font = font,
                fontSize = 72,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
            };
            bannerStyle.normal.textColor = Color.white;

            buttonStyle = new GUIStyle(GUI.skin.button)
            {
                font = font,
                fontSize = 13,
                alignment = TextAnchor.MiddleCenter,
                padding = new RectOffset(8, 8, 4, 4),
                border = new RectOffset(1, 1, 1, 1),
            };
            buttonStyle.normal.background = texButton;
            buttonStyle.hover.background = texButtonHover;
            buttonStyle.active.background = texButtonActive;
            buttonStyle.focused.background = texButtonHover;
            buttonStyle.normal.textColor = new Color(0.92f, 0.95f, 1f, 1f);
            buttonStyle.hover.textColor = Color.white;
            buttonStyle.active.textColor = Color.white;

            buttonOnStyle = new GUIStyle(buttonStyle);
            buttonOnStyle.normal.background = texButtonActive;
            buttonOnStyle.hover.background = texButtonActive;
            buttonOnStyle.active.background = texButtonActive;

            arrowStyle = new GUIStyle(GUI.skin.button)
            {
                font = font,
                fontSize = 12,
                alignment = TextAnchor.MiddleCenter,
                padding = new RectOffset(0, 0, 0, 0),
                border = new RectOffset(0, 0, 0, 0),
            };
            arrowStyle.normal.background = null;
            arrowStyle.hover.background = texRowHover;
            arrowStyle.active.background = texRowSelected;
            arrowStyle.normal.textColor = Muted;
            arrowStyle.hover.textColor = Color.white;

            catchStyle = new GUIStyle(GUIStyle.none);
            catchStyle.normal.background = texClear;
            catchStyle.hover.background = texClear;
            catchStyle.active.background = texClear;
        }

        private static Font GetGameFont()
        {
            try
            {
                if (Draw.instance != null)
                {
                    FieldInfo field = typeof(Draw).GetField(
                        "MainFont", BindingFlags.Instance | BindingFlags.NonPublic);
                    if (field != null)
                    {
                        return field.GetValue(Draw.instance) as Font;
                    }
                }
            }
            catch
            {
                // Fall back to Unity's default GUI font.
            }

            return null;
        }

        private static Texture GetPrivateTexture(string fieldName)
        {
            try
            {
                if (Draw.instance != null)
                {
                    FieldInfo field = typeof(Draw).GetField(
                        fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
                    if (field != null)
                    {
                        return field.GetValue(Draw.instance) as Texture;
                    }
                }
            }
            catch
            {
                // Optional cosmetic textures.
            }

            return null;
        }

        private Texture GetPortalTexture(string scene)
        {
            if (string.IsNullOrEmpty(scene))
            {
                return null;
            }

            Texture texture;
            if (portalCache.TryGetValue(scene, out texture))
            {
                return texture;
            }

            try
            {
                Hub.PuzzleData data;
                if (Hub.puzzleData != null && Hub.puzzleData.TryGetValue(scene, out data)
                    && data != null && data.thumbnail != null)
                {
                    texture = data.thumbnail;
                }
            }
            catch
            {
                texture = null;
            }

            if (texture == null)
            {
                try
                {
                    texture = Resources.Load("level_thumbnails/" + scene, typeof(Texture2D)) as Texture2D;
                }
                catch
                {
                    texture = null;
                }
            }

            portalCache[scene] = texture;
            return texture;
        }

        private static Texture2D MakeTex(Color color)
        {
            var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            texture.SetPixel(0, 0, color);
            texture.Apply();
            texture.hideFlags = HideFlags.DontSave;
            return texture;
        }

        // -------------------------------------------------------------- primitives

        private void Fill(Rect rect, Color color)
        {
            if (rect.width <= 0f || rect.height <= 0f)
            {
                return;
            }

            Color old = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, white);
            GUI.color = old;
        }

        private void Border(Rect rect, Color color, float thickness)
        {
            Fill(new Rect(rect.x, rect.y, rect.width, thickness), color);
            Fill(new Rect(rect.x, rect.yMax - thickness, rect.width, thickness), color);
            Fill(new Rect(rect.x, rect.y, thickness, rect.height), color);
            Fill(new Rect(rect.xMax - thickness, rect.y, thickness, rect.height), color);
        }

        private void Ring(Rect rect, Color color, float thickness)
        {
            Border(rect, color, thickness);
        }

        private void Cross(Rect rect, Color color)
        {
            float cx = rect.center.x;
            float cy = rect.center.y;
            float arm = Mathf.Min(rect.width, rect.height) * 0.22f;
            float thick = Mathf.Max(1f, Mathf.Min(rect.width, rect.height) * 0.07f);
            Fill(new Rect(cx - arm, cy - thick * 0.5f, arm * 2f, thick), color);
            Fill(new Rect(cx - thick * 0.5f, cy - arm, thick, arm * 2f), color);
        }

        private void DrawTextureTinted(Rect rect, Texture texture, Color color)
        {
            if (texture == null || rect.width <= 0f || rect.height <= 0f)
            {
                return;
            }

            Color old = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, texture, ScaleMode.StretchToFill, true);
            GUI.color = old;
        }

        private void DrawGameTexture(Rect cell, Texture large, Texture small, Color color, float scale)
        {
            Texture texture = cell.width >= 26f && large != null ? large : (small != null ? small : large);
            if (texture == null)
            {
                return;
            }

            Rect target = Centered(cell, Mathf.Min(cell.width, cell.height) * scale);
            DrawTextureTinted(target, texture, color);
        }

        private static Rect Centered(Rect rect, float size)
        {
            size = Mathf.Max(1f, size);
            return new Rect(rect.center.x - size * 0.5f, rect.center.y - size * 0.5f, size, size);
        }

        private static Rect Inset(Rect rect, float fraction)
        {
            float dx = rect.width * fraction;
            float dy = rect.height * fraction;
            return new Rect(rect.x + dx, rect.y + dy,
                Mathf.Max(1f, rect.width - dx * 2f), Mathf.Max(1f, rect.height - dy * 2f));
        }

        private static bool Inside(Rect rect, Rect canvas)
        {
            return rect.x >= canvas.x && rect.y >= canvas.y
                && rect.xMax <= canvas.xMax && rect.yMax <= canvas.yMax;
        }

        // ------------------------------------------------------------------- zoom

        private void FitMap()
        {
            zoom = 1f;
            pan = Vector2.zero;
        }

        private void ZoomBy(float factor)
        {
            Vector2 pivot = lastCanvas.width > 0f
                ? lastCanvas.center
                : new Vector2(Screen.width * 0.65f, Screen.height * 0.5f);
            ZoomAt(pivot, factor);
        }

        private void ZoomAt(Vector2 pivot, float factor)
        {
            Level level = selectedNode != null ? selectedNode.Level : null;
            if (level == null)
            {
                return;
            }

            float before = lastGrid.width / Mathf.Max(1, level.width);
            zoom = Mathf.Clamp(zoom * factor, MinZoom, MaxZoom);

            if (lastCanvas.width <= 0f)
            {
                return;
            }

            float fit = Mathf.Min(
                lastCanvas.width / Mathf.Max(1, level.width),
                lastCanvas.height / Mathf.Max(1, level.height));
            float after = Mathf.Clamp(Mathf.Floor(fit * 0.92f * zoom), 2f, 400f);
            if (before <= 0.5f || after <= 0.5f)
            {
                return;
            }

            float ratio = after / before;
            float centerX = lastGrid.center.x - pan.x;
            float centerY = lastGrid.center.y - pan.y;
            pan.x = pan.x * ratio + (pivot.x - centerX) * (1f - ratio);
            pan.y = pan.y * ratio + (pivot.y - centerY) * (1f - ratio);
        }

        // ------------------------------------------------------------------- log

        private void LogTreeLayout()
        {
            try
            {
                foreach (LevelNode root in graph.Roots)
                {
                    LogNode(root);
                }
            }
            catch (Exception ex)
            {
                Log.Error("failed to log tree layout", ex);
            }
        }

        private static void LogNode(LevelNode node)
        {
            Log.Info(new string(' ', node.Depth * 2) + node.Title + "  | " + node.Subtitle
                + (node.ContainsPlayer ? "  <player>" : string.Empty));
            foreach (LevelNode child in node.Children)
            {
                LogNode(child);
            }
        }
    }
}
