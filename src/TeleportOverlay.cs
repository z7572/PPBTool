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

        private Texture[] texWallTiles;
        private Texture[] texWallTilesFlip;
        private Texture[] texWallBig;
        private Texture[] texWallBigFlip;
        private Texture texGradient;
        private Texture texInf;
        private Texture texInfOutline;
        private Texture texEyeOpen;
        private Texture texEyeOpenSmall;
        private Texture texEyeClosed;
        private Texture texEyeClosedSmall;
        private Texture texPossessEye;
        private Texture texPossessEyeSmall;
        private Texture texMouthFrowny;
        private Texture texMouthFlat;
        private Texture texMouthOpen;
        private Texture texMouthV;
        private Texture texMouthMeh;
        private Texture texMouthSmile;
        private Texture texMouthSquare;

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
        private Block selectedColorOwner;
        private bool hideVoid;
        private bool portalArt;
        private int cursorX;
        private int cursorY;
        private float zoom = 1f;
        private Vector2 pan;
        private float treeScroll;
        private float treeScrollX;
        private float treeContentW;
        private float treeWidth;
        private bool draggingThumb;
        private bool draggingHThumb;
        private bool draggingSplitter;
        private float thumbGrab;
        private float hThumbGrab;
        private Rect lastTreeView;
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
            selectedColorOwner = null;
            lastCanvas = default(Rect);
            lastGrid = default(Rect);
            savedState = World.State;
            hideVoid = config.HideVoidBoxes;
            portalArt = config.LevelThumbnails;
            showInfMenu = false;
            showInfConfirm = false;
            titleStyle = null;
            RebuildGraph(false);
            if (rows.Count == 0 && hideVoid)
            {
                // Never leave the browser unopenable just because a persisted filter
                // hides every row. Turn it off for this session and retry.
                hideVoid = false;
                config.HideVoidBoxes = false;
                FlattenTree();
            }

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
            selectedColorOwner = null;
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
                    bool shift = keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed;
                    if (shift)
                    {
                        treeScrollX -= notches * 80f;
                        ClampTreeScrollX();
                    }
                    else
                    {
                        treeScroll -= notches * 60f;
                        ClampTreeScroll();
                    }
                }
                else if (lastCanvas.Contains(guiMouse))
                {
                    // Scroll up (positive) zooms in: larger cell size.
                    ZoomAt(guiMouse, notches > 0f ? 1.1f : 0.9f);
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
                Block colorOwner = block;
                NavigateToLevel(block.SubLevel);
                if (selectedNode != null && selectedNode.Level == block.SubLevel)
                {
                    selectedColorOwner = colorOwner;
                }

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

        private void TogglePortalArt()
        {
            portalArt = !portalArt;
            config.LevelThumbnails = portalArt;
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
            infMenuTitle = (enter ? UiText.Get("inf_enter") : UiText.Get("inf_exit"))
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
            if (treeWidth <= 0f)
            {
                treeWidth = Mathf.Clamp(w * 0.30f, 320f, 470f);
            }

            treeWidth = Mathf.Clamp(treeWidth, 240f, Mathf.Max(260f, w * 0.55f));
            var treeRect = new Rect(Margin, bodyTop, treeWidth, bodyH);
            var splitter = new Rect(treeRect.xMax, bodyTop, 6f, bodyH);
            HandleSplitter(splitter, w);

            var mapRect = new Rect(
                splitter.xMax + Gap,
                bodyTop,
                Mathf.Max(120f, w - treeWidth - Margin * 2f - 6f - Gap),
                bodyH);

            DrawSplitter(splitter);
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
            GUI.Label(new Rect(rect.x + 14f, rect.y + 4f, rect.width - 28f, 28f),
                Ellipsize(message, statusStyle, rect.width - 28f), statusStyle);
        }

        private void DrawHeader(Rect rect)
        {
            Fill(rect, Header);
            Fill(new Rect(rect.x, rect.yMax - 1f, rect.width, 1f), Edge);

            GUI.Label(new Rect(rect.x + 16f, rect.y + 4f, 360f, 26f), UiText.Get("title"), titleStyle);

            string sceneName = sceneAtBuild == "custom_level" ? graph.SceneLabel : sceneAtBuild;
            string scene = UiText.Get("scene") + ": " + sceneName
                + "    " + UiText.Get("levels") + ": " + graph.AllNodes.Count()
                + " / " + rows.Count;
            GUI.Label(new Rect(rect.x + 16f, rect.y + 30f, 400f, 20f),
                Ellipsize(scene, smallStyle, 390f), smallStyle);

            if (selectedNode != null)
            {
                float x = rect.x + 430f;
                float width = Mathf.Max(120f, rect.width - x - 130f);
                GUI.Label(new Rect(x, rect.y + 5f, width, 24f),
                    Ellipsize(UiText.Get("level") + ": " + selectedNode.Path, labelStyle, width),
                    labelStyle);
                GUI.Label(new Rect(x, rect.y + 31f, width, 20f),
                    Ellipsize(selectedNode.Subtitle, smallStyle, width), smallStyle);
            }

            if (GUI.Button(new Rect(rect.xMax - 104f, rect.y + 13f, 90f, 30f), UiText.Get("close"), buttonStyle))
            {
                Close();
            }
        }

        private static string Ellipsize(string text, GUIStyle style, float maxWidth)
        {
            if (string.IsNullOrEmpty(text) || maxWidth <= 4f || style == null)
            {
                return text ?? string.Empty;
            }

            if (style.CalcSize(new GUIContent(text)).x <= maxWidth)
            {
                return text;
            }

            const string suffix = "...";
            int low = 0;
            int high = text.Length;
            while (low < high)
            {
                int mid = (low + high + 1) / 2;
                string candidate = text.Substring(0, mid) + suffix;
                if (style.CalcSize(new GUIContent(candidate)).x <= maxWidth)
                {
                    low = mid;
                }
                else
                {
                    high = mid - 1;
                }
            }

            return low <= 0 ? suffix : text.Substring(0, low) + suffix;
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
            x = ToolButton(x, y, UiText.Get("level_thumbnails"), true, portalArt, TogglePortalArt) + 8f;

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

            Rect view = new Rect(panel.x + 6f, panel.y + 28f, panel.width - 20f, panel.height - 34f - 12f);
            lastTreeView = view;
            if (rows.Count == 0)
            {
                GUI.Label(new Rect(view.x + 6f, view.y + 6f, view.width - 12f, 24f), UiText.Get("no_levels"), smallStyle);
                return;
            }

            float contentH = rows.Count * RowH;
            float maxScroll = Mathf.Max(0f, contentH - view.height);
            treeScroll = Mathf.Clamp(treeScroll, 0f, maxScroll);

            float contentW = view.width;
            foreach (LevelNode rowNode in rows)
            {
                float titleW = labelStyle.CalcSize(new GUIContent(rowNode.Title)).x;
                float tagsW = string.IsNullOrEmpty(rowNode.Tags)
                    ? 0f
                    : smallStyle.CalcSize(new GUIContent("[" + rowNode.Tags + "]")).x + 8f;
                float need = 24f + rowNode.Depth * 13f + titleW + tagsW + 90f;
                if (need > contentW)
                {
                    contentW = need;
                }
            }

            treeContentW = contentW;
            float maxScrollX = Mathf.Max(0f, contentW - view.width);
            treeScrollX = Mathf.Clamp(treeScrollX, 0f, maxScrollX);

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
                    float arrowX = 4f + node.Depth * 13f - treeScrollX;
                    var arrow = new Rect(arrowX, y + 4f, 14f, 14f);
                    if (treeInput && arrow.xMax > 0f && arrow.x < view.width
                        && GUI.Button(arrow, IsExpanded(node) ? "-" : "+", arrowStyle))
                    {
                        deferredToggle = node;
                    }
                }

                float hitX = (node.Children.Count > 0 ? 20f : 4f) + node.Depth * 13f - treeScrollX;
                if (hitX < view.width)
                {
                    var hit = new Rect(hitX, y, Mathf.Max(1f, contentW - hitX), RowH - 1f);
                    if (treeInput && GUI.Button(hit, GUIContent.none, catchStyle))
                    {
                        deferredSelect = node;
                    }
                }

                float textX = 24f + node.Depth * 13f - treeScrollX;
                Color oldColor = GUI.color;
                if (node.ContainsPlayer)
                {
                    GUI.color = PlayerCol;
                }

                GUI.Label(new Rect(textX, y, Mathf.Max(1f, contentW - textX - 80f), RowH - 1f),
                    node.Title, labelStyle);
                GUI.color = oldColor;

                if (!string.IsNullOrEmpty(node.Tags))
                {
                    float tw = labelStyle.CalcSize(new GUIContent(node.Title)).x;
                    float tagsX = textX + tw + 6f;
                    if (tagsX < view.width - 10f)
                    {
                        GUI.Label(new Rect(tagsX, y, Mathf.Max(1f, contentW - tagsX - 80f), RowH - 1f),
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

            if (maxScrollX > 0f)
            {
                DrawTreeHScrollbar(panel, view, contentW, maxScrollX);
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

        private void DrawTreeHScrollbar(Rect panel, Rect view, float contentW, float maxScrollX)
        {
            var track = new Rect(view.x, view.yMax + 2f, view.width, 6f);
            DrawTextureTinted(track, texTrack, Color.white);

            float thumbW = Mathf.Max(28f, view.width * view.width / Mathf.Max(1f, contentW));
            float t = maxScrollX > 0f ? treeScrollX / maxScrollX : 0f;
            float thumbX = view.x + (view.width - thumbW) * t;
            var thumb = new Rect(thumbX, track.y, thumbW, track.height);
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
                    draggingHThumb = true;
                    hThumbGrab = mouse.x - thumb.x;
                    e.Use();
                }
                else if (track.Contains(mouse))
                {
                    float target = (mouse.x - track.x - thumbW * 0.5f) / Mathf.Max(1f, track.width - thumbW);
                    treeScrollX = Mathf.Clamp(target * maxScrollX, 0f, maxScrollX);
                    draggingHThumb = true;
                    hThumbGrab = thumbW * 0.5f;
                    e.Use();
                }
            }
            else if (e.type == EventType.MouseDrag && draggingHThumb && e.button == 0)
            {
                float target = (mouse.x - track.x - hThumbGrab) / Mathf.Max(1f, track.width - thumbW);
                treeScrollX = Mathf.Clamp(target * maxScrollX, 0f, maxScrollX);
                e.Use();
            }
            else if (e.type == EventType.MouseUp && e.button == 0)
            {
                draggingHThumb = false;
            }
        }

        private void HandleSplitter(Rect splitter, float screenWidth)
        {
            if (showInfMenu || showInfConfirm)
            {
                return;
            }

            Event e = Event.current;
            if (e == null)
            {
                return;
            }

            Vector2 mouse = e.mousePosition;
            if (e.type == EventType.MouseDown && e.button == 0 && splitter.Contains(mouse))
            {
                draggingSplitter = true;
                e.Use();
            }
            else if (e.type == EventType.MouseDrag && draggingSplitter && e.button == 0)
            {
                treeWidth = Mathf.Clamp(
                    mouse.x - Margin,
                    240f,
                    Mathf.Max(260f, screenWidth * 0.55f));
                e.Use();
            }
            else if (e.type == EventType.MouseUp && e.button == 0)
            {
                draggingSplitter = false;
            }
        }

        private void DrawSplitter(Rect splitter)
        {
            bool hover = draggingSplitter
                || (Event.current != null && splitter.Contains(Event.current.mousePosition));
            Color color = hover
                ? new Color(0.35f, 0.80f, 0.95f, 1f)
                : new Color(0.20f, 0.24f, 0.30f, 1f);
            Fill(new Rect(splitter.center.x - 1f, splitter.y + 4f, 2f, Mathf.Max(1f, splitter.height - 8f)), color);
        }

        private void ClampTreeScroll()
        {
            float viewH = lastTreeView.height > 0f ? lastTreeView.height : 400f;
            float maxScroll = Mathf.Max(0f, rows.Count * RowH - viewH);
            treeScroll = Mathf.Clamp(treeScroll, 0f, maxScroll);
        }

        private void ClampTreeScrollX()
        {
            float viewW = lastTreeView.width > 0f ? lastTreeView.width : 400f;
            float maxScroll = Mathf.Max(0f, treeContentW - viewW);
            treeScrollX = Mathf.Clamp(treeScrollX, 0f, maxScroll);
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
            treeScrollX = 0f;
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
            EnsureGameArtTextures();
            EnsureLanding(level);

            var detailRect = new Rect(panel.x + 6f, panel.y + 5f, panel.width - 12f, 24f);
            Fill(detailRect, Bar);
            GUI.Label(new Rect(detailRect.x + 8f, detailRect.y + 2f, detailRect.width - 16f, 20f),
                Ellipsize(BuildDetail(level), labelStyle, detailRect.width - 16f), labelStyle);

            var canvas = new Rect(panel.x + 8f, panel.y + 32f, panel.width - 16f, panel.height - 32f - 8f);
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

            // Draw inside a clipped group so edge cells show their visible portion instead
            // of disappearing entirely when zoomed/panned.
            float localX = originX - canvas.x;
            float localY = originY - canvas.y;
            var localCanvas = new Rect(0f, 0f, canvas.width, canvas.height);

            GUI.BeginGroup(canvas);
            Block levelOwner = LevelGraph.ExitBlockOf(level);
            Block visualOwner = selectedColorOwner != null && selectedColorOwner.SubLevel == level
                ? selectedColorOwner
                : levelOwner;
            if (visualOwner != null)
            {
                if (visualOwner.OuterLevel != null)
                {
                    Color levelBg = GameBlockColor(
                        visualOwner, Mathf.Max(24, Mathf.RoundToInt(Mathf.Min(gridW, gridH))));
                    Fill(new Rect(localX, localY, gridW, gridH), levelBg);
                    DrawLevelGrid(level, visualOwner, localX, localY, cell, gridW, gridH);
                }

                if (World.InHub())
                {
                    GUI.BeginGroup(new Rect(localX, localY, gridW, gridH));
                    DrawHubLines(level, visualOwner, cell, cell, false);
                    GUI.EndGroup();
                }
            }

            for (int row = firstRow; row <= lastRow; row++)
            {
                int y = level.height - 1 - row;
                for (int x = firstCol; x <= lastCol; x++)
                {
                    var r = new Rect(localX + x * cell, localY + row * cell, cell, cell);
                    if (!Overlaps(r, localCanvas))
                    {
                        continue;
                    }

                    DrawRealCell(level, x, y, r, cell, 0, visualOwner);
                }
            }

            Block exit = LevelGraph.ExitBlockOf(level);
            if (exit != null && exit.SubLevel == level
                && (exit.OuterLevel == null || !exit.OuterLevel.InfZone))
            {
                var boxRect = new Rect(localX, localY, gridW, gridH);
                int boxCell = Mathf.RoundToInt(Mathf.Min(gridW, gridH));
                if (exit.IsPlayer || exit.DrawIsPlayer)
                {
                    DrawFaceArt(boxRect, exit, boxCell);
                }

                if (exit.IsSomeInfEnterBlock || exit.IsSomeInfExitBlock)
                {
                    DrawInfSymbols(boxRect, exit, boxCell);
                }
            }

            if (landingValid)
            {
                int row = level.height - 1 - landingY;
                var landing = new Rect(localX + landingX * cell, localY + row * cell, cell, cell);
                if (Overlaps(landing, localCanvas) && !(landingX == playerX && landingY == playerY))
                {
                    Ring(landing, TargetCol, Mathf.Max(2f, cell * 0.08f));
                    Cross(landing, TargetCol);
                }
            }

            var gridRect = new Rect(localX - 1f, localY - 1f, gridW + 2f, gridH + 2f);
            if (Overlaps(gridRect, localCanvas))
            {
                Border(gridRect, new Color(0.36f, 0.42f, 0.50f, 0.9f), 1f);
            }

            DrawCursor(level, localX, localY, cell, localCanvas);
            GUI.EndGroup();
        }

        private void DrawFloor(Rect r, Floor floor, bool hasBlock, int cell)
        {
            Color color = GameFloorColor(floor, cell);

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
            Color hard = PortalHardColor(floor.Hard);
            if (hard.a > 0.01f)
            {
                Fill(r, hard);
            }

            if (portalArt)
            {
                Texture texture = GetPortalTexture(floor.SceneName);
                if (texture != null)
                {
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

            var marker = Centered(r, Mathf.Min(r.width, r.height) * 0.55f);
            Fill(marker, hard.a > 0.01f ? Color.Lerp(hard, Color.white, 0.18f) : FloorPortal);
            Border(marker, hard.a > 0.01f ? Color.white : new Color(0.9f, 0.85f, 1f, 0.9f), 1f);
            if (floor.Unlocked && !floor.Won && cell >= 10)
            {
                Fill(Centered(r, cell * 0.16f), Color.white);
            }
        }

        private static Color PortalHardColor(int hard)
        {
            switch (hard)
            {
                case 1:
                    // Particle.hardPortalColor: h=0, s=0.9, v=0.8, a=1
                    return Color.HSVToRGB(0f, 0.9f, 0.8f);
                case 2:
                    // Particle.visualPortalColor: h=0.53, s=1, v=1, a=1
                    return Color.HSVToRGB(0.53f, 1f, 1f);
                default:
                    return Color.clear;
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

        private void DrawRealCell(Level level, int x, int y, Rect r, int cell, int depth, Block parentBox)
        {
            Floor floor = level.floors[x, y];
            Block block = level.blocks[x, y];
            if (floor != null)
            {
                DrawFloor(r, floor, block != null, cell);
            }

            if (block == null || depth > 5)
            {
                return;
            }

            if (block.IsPlayer)
            {
                DrawPlayerArt(r, block, cell, depth);
            }
            else if (block.SubLevel != null)
            {
                DrawBoxArt(r, block, cell, depth);
            }
            else
            {
                DrawWallArt(r, block, cell, parentBox);
            }
        }

        private void DrawWallArt(Rect r, Block block, int cell, Block parentBox)
        {
            Block owner = parentBox ?? LevelGraph.ExitBlockOf(block.OuterLevel);
            Color color = GameWallColor(owner, cell);
            bool flip = block.FlipH;

            if (cell >= 256f && texWallBig != null && texWallBig.Length > 0)
            {
                int quadIndex = flip ? block.WallTileIndexFlipH : block.WallTileIndex;
                if (Autotile.quads != null && quadIndex >= 0 && quadIndex < Autotile.quads.Length)
                {
                    Autotile.Quad quad = Autotile.quads[quadIndex];
                    Texture[] big = flip && texWallBigFlip != null ? texWallBigFlip : texWallBig;
                    float hw = r.width * 0.5f;
                    float hh = r.height * 0.5f;
                    DrawWallQuad(big, quad.TL, new Rect(r.x, r.y, hw, hh), color);
                    DrawWallQuad(big, quad.TR, new Rect(r.x + hw, r.y, hw, hh), color);
                    DrawWallQuad(big, quad.BL, new Rect(r.x, r.y + hh, hw, hh), color);
                    DrawWallQuad(big, quad.BR, new Rect(r.x + hw, r.y + hh, hw, hh), color);
                    return;
                }
            }

            Texture[] tiles = flip && texWallTilesFlip != null ? texWallTilesFlip : texWallTiles;
            int index = flip ? block.WallTileIndexFlipH : block.WallTileIndex;
            Texture texture = PickTexture(tiles, index);
            if (texture != null)
            {
                DrawTextureTinted(r, texture, color);
            }
            else
            {
                Fill(r, color);
            }

            if (block.Locked)
            {
                Border(r, new Color(1f, 0.25f, 0.22f, 0.9f), 1f);
            }

            DrawFlipShineArt(r, block, cell);
        }

        private void DrawWallQuad(Texture[] textures, int index, Rect r, Color color)
        {
            Texture texture = PickTexture(textures, index);
            if (texture != null)
            {
                DrawTextureTinted(r, texture, color);
            }
            else
            {
                Fill(r, color);
            }
        }

        private void DrawBoxArt(Rect r, Block block, int cell, int depth)
        {
            Color color = GameBlockColor(block, cell);
            Level sub = block.SubLevel;
            if (sub != null && sub.InfZone)
            {
                color.a = 0f;
            }

            Fill(r, color);
            if (texGradient != null)
            {
                DrawTextureTinted(r, texGradient, new Color(0f, 0f, 0f, 0.08f));
            }

            if (sub != null && cell >= 14f && depth < 4)
            {
                if (World.InHub())
                {
                    GUI.BeginGroup(r);
                    DrawHubLines(sub, block, r.width / sub.width, r.height / sub.height, block.FlipH);
                    GUI.EndGroup();
                }

                DrawBoxContents(r, block, sub, cell, depth);
            }
            else if (sub != null)
            {
                DrawBoxGrid(r, block, sub);
            }

            DrawBoxBorder(r, block, sub, cell);
            DrawFlipShineArt(r, block, cell);
            bool parentVoid = block.OuterLevel != null && block.OuterLevel.InfZone;
            if (!parentVoid && sub != null && GameState.Player != null
                && GameState.Player.OuterLevel == sub
                && GameState.Player.SubLevel == null)
            {
                DrawFaceArt(r, GameState.Player, cell);
            }

            if (!parentVoid)
            {
                DrawInfSymbols(r, block, cell);
            }
        }

        private void DrawLevelGrid(
            Level level, Block owner, float originX, float originY, int cell, float gridW, float gridH)
        {
            float levelAvg = (level.width + level.height) * 0.5f;
            float scale = ((gridW + gridH) * 0.5f) / Mathf.Max(1f, levelAvg);
            float line = GameBorderThickness(scale * 0.6f, scale * 0.6f);
            float hue;
            float sat;
            float val;
            GameBlockHsv(owner, 24, out hue, out sat, out val);
            val = val * 0.65f - 0.075f;
            if (LevelGraph.IsCloneBox(owner))
            {
                val += 0.075f;
            }

            Color grid = Hsv(hue, sat, val);
            grid.a = 0.65f;
            for (int x = 1; x < level.width; x++)
            {
                Fill(new Rect(originX + x * cell, originY, line, gridH), grid);
            }

            for (int y = 1; y < level.height; y++)
            {
                Fill(new Rect(originX, originY + y * cell, gridW, line), grid);
            }
        }

        private void DrawHubLines(Level level, Block owner, float cw, float ch, bool flipH)
        {
            if (!World.InHub() || owner == null || level == null || level.floorList == null)
            {
                return;
            }

            float fraction = (cw + ch) * 0.5f / Mathf.Max(1f, Screen.height);
            Color active = HubLineColor(owner, true, fraction);
            Color inactive = HubLineColor(owner, false, fraction);

            foreach (Floor floor in level.floorList)
            {
                if (floor == null || floor.Type != Floor.FloorType.LevelPortal
                    || floor.UnlockLines == null || floor.UnlockLines.Length == 0)
                {
                    continue;
                }

                float childX = (floor.xpos + 0.5f) * cw;
                if (flipH)
                {
                    childX = (level.width - 1 - floor.xpos + 0.5f) * cw;
                }

                float childY = (level.height - 1 - floor.ypos + 0.5f) * ch;
                foreach (Floor.UnlockLine line in floor.UnlockLines)
                {
                    float sx = childX;
                    float sy = childY;
                    if (line.finishToReception)
                    {
                        sx += cw * 3f;
                        sy += cw * 2f;
                    }

                    float ex = sx + line.dx * cw;
                    float ey = sy - line.dy * ch;
                    if (line.lastPortal)
                    {
                        ex -= cw * 0.57f;
                    }

                    if (line.finishToReception)
                    {
                        sx += cw * 0.57f;
                    }

                    Color color = line.lit ? active : inactive;
                    int thickness = (int)(cw * (line.thick ? 0.32f : 0.12f));
                    if (thickness < 1)
                    {
                        thickness = 1;
                    }

                    int sm = thickness / 2;
                    int sp = thickness - sm;
                    switch (line.shape)
                    {
                        case Floor.LS.Straight:
                            DrawHubLine(sx, sy, ex, ey, sm, sp, color);
                            break;
                        case Floor.LS.HorizFirstL:
                            DrawHubLine(sx, sy, ex, sy, sm, sp, color);
                            DrawHubLine(ex, sy, ex, ey, sm, sp, color);
                            break;
                        case Floor.LS.VertFirstL:
                            DrawHubLine(sx, sy, sx, ey, sm, sp, color);
                            DrawHubLine(sx, ey, ex, ey, sm, sp, color);
                            break;
                        case Floor.LS.HorizFirstS:
                        {
                            float midX = sx + Mathf.Sign(line.dx) * cw;
                            DrawHubLine(sx, sy, midX, sy, sm, sp, color);
                            DrawHubLine(midX, ey, ex, ey, sm, sp, color);
                            if (line.dy != 0)
                            {
                                DrawHubLine(midX, sy, midX, ey, sm, sp, color);
                            }

                            break;
                        }
                        case Floor.LS.VertFirstS:
                        {
                            float midY = sy - Mathf.Sign(line.dy) * ch;
                            DrawHubLine(sx, sy, sx, midY, sm, sp, color);
                            DrawHubLine(ex, midY, ex, ey, sm, sp, color);
                            if (line.dx != 0)
                            {
                                DrawHubLine(sx, midY, ex, midY, sm, sp, color);
                            }

                            break;
                        }
                    }
                }
            }
        }

        private void DrawHubLine(
            float x1, float y1, float x2, float y2, int sm, int sp, Color color)
        {
            float x = Mathf.Min(x1, x2) - sm;
            float y = Mathf.Min(y1, y2) - sm;
            float w = Mathf.Abs(x2 - x1) + sm + sp;
            float h = Mathf.Abs(y2 - y1) + sm + sp;
            Fill(new Rect(x, y, Mathf.Max(1f, w), Mathf.Max(1f, h)), color);
        }

        private Color HubLineColor(Block block, bool active, float fraction)
        {
            float val = Mathf.Max(block.val, 0.4f);
            val *= -0.4f * Mathf.Log(Mathf.Max(fraction, 0.0001f), 20f) + 0.8f;
            float sat = block.sat;
            if (active)
            {
                sat *= 0.85f;
                val *= 0.9f;
            }
            else
            {
                val *= 0.25f;
            }

            return Hsv(block.hue, sat, val);
        }

        private void DrawBoxGrid(Rect r, Block block, Level sub)
        {            float boxAvg = (r.width + r.height) * 0.5f;
            float subAvg = (sub.width + sub.height) * 0.5f;
            float scale = boxAvg / Mathf.Max(1f, subAvg);
            float line = GameBorderThickness(scale * 0.6f, scale * 0.6f);
            int cell = Mathf.Max(1, Mathf.RoundToInt(boxAvg));
            float hue;
            float sat;
            float val;
            GameBlockHsv(block, cell, out hue, out sat, out val);
            val = val * 0.65f - 0.075f;
            if (LevelGraph.IsCloneBox(block))
            {
                val += 0.075f;
            }

            Color grid = Hsv(hue, sat, val);
            grid.a = 0.65f;
            for (int i = 1; i < sub.width; i++)
            {
                Fill(new Rect(r.x + r.width * i / sub.width, r.y, line, r.height), grid);
            }

            for (int i = 1; i < sub.height; i++)
            {
                Fill(new Rect(r.x, r.y + r.height * i / sub.height, r.width, line), grid);
            }
        }

        private void DrawBoxContents(Rect r, Block block, Level sub, int cell, int depth)
        {            // The game draws the sublevel across the whole box; the border is painted
            // afterwards on top and does not consume content space.
            Rect inner = r;
            if (inner.width < 4f || inner.height < 4f || sub.width <= 0 || sub.height <= 0)
            {
                return;
            }

            if (sub.width * sub.height > 1600)
            {
                return;
            }

            DrawBoxGrid(inner, block, sub);

            float cw = inner.width / sub.width;
            float ch = inner.height / sub.height;
            if (cw < 2f || ch < 2f)
            {
                return;
            }

            GUI.BeginGroup(inner);
            for (int row = 0; row < sub.height; row++)
            {
                int y = sub.height - 1 - row;
                for (int x = 0; x < sub.width; x++)
                {
                    int drawX = block.FlipH ? sub.width - 1 - x : x;
                    var cr = new Rect(drawX * cw, row * ch, cw, ch);
                    DrawRealCell(sub, x, y, cr, Mathf.RoundToInt(Mathf.Min(cw, ch)), depth + 1, block);
                }
            }

            GUI.EndGroup();
        }

        private void DrawBoxBorder(Rect r, Block block, Level sub, int cell)
        {
            float thickness = GameBorderThickness(r.width, r.height);
            Color border = block.borderColor;
            if (border.r <= 0.001f && border.g <= 0.001f && border.b <= 0.001f)
            {
                border = Color.white;
            }

            border.a = 0.12f;
            if (block.Locked)
            {
                border = Color.red;
                border.a = 0.9f;
            }

            if (sub == null)
            {
                Fill(new Rect(r.x, r.y, thickness, r.height), border);
                Fill(new Rect(r.xMax - thickness, r.y, thickness, r.height), border);
                Fill(new Rect(r.x, r.y, r.width, thickness), border);
                Fill(new Rect(r.x, r.yMax - thickness, r.width, thickness), border);
                return;
            }

            bool inf = block.OuterLevel != null && block.OuterLevel.InfZone
                && !sub.FilledWithWalls && block.SpecialEffect != 12;
            float cw = r.width / sub.width;
            float ch = r.height / sub.height;

            if (sub.leftWall || inf)
            {
                Fill(new Rect(r.x, r.y, thickness, r.height), border);
            }
            else
            {
                for (int y = 0; y < sub.height; y++)
                {
                    if (IsBorderWall(sub.blocks[0, y]))
                    {
                        Fill(new Rect(r.x, r.y + ch * (sub.height - 1 - y), thickness, ch), border);
                    }
                }
            }

            if (sub.rightWall || inf)
            {
                Fill(new Rect(r.xMax - thickness, r.y, thickness, r.height), border);
            }
            else
            {
                for (int y = 0; y < sub.height; y++)
                {
                    if (IsBorderWall(sub.blocks[sub.width - 1, y]))
                    {
                        Fill(new Rect(r.xMax - thickness, r.y + ch * (sub.height - 1 - y), thickness, ch), border);
                    }
                }
            }

            if (sub.upWall || inf)
            {
                Fill(new Rect(r.x, r.y, r.width, thickness), border);
            }
            else
            {
                for (int x = 0; x < sub.width; x++)
                {
                    if (IsBorderWall(sub.blocks[x, sub.height - 1]))
                    {
                        Fill(new Rect(r.x + cw * x, r.y, cw, thickness), border);
                    }
                }
            }

            if (sub.downWall || inf)
            {
                Fill(new Rect(r.x, r.yMax - thickness, r.width, thickness), border);
            }
            else
            {
                for (int x = 0; x < sub.width; x++)
                {
                    if (IsBorderWall(sub.blocks[x, 0]))
                    {
                        Fill(new Rect(r.x + cw * x, r.yMax - thickness, cw, thickness), border);
                    }
                }
            }
        }

        private static bool IsBorderWall(Block block)
        {
            return block != null && block.SubLevel == null;
        }

        private void DrawPlayerArt(Rect r, Block block, int cell, int depth)
        {
            bool parentVoid = block.OuterLevel != null && block.OuterLevel.InfZone;
            if (block.SubLevel != null)
            {
                DrawBoxArt(r, block, cell, depth);
                if (!parentVoid)
                {
                    DrawFaceArt(r, block, cell);
                }
                return;
            }

            Color color = GamePlayerColor(block, cell);
            Fill(r, color);
            if (!parentVoid)
            {
                DrawFaceArt(r, block, cell);
                DrawInfSymbols(r, block, cell);
            }
        }

        private void DrawFaceArt(Rect r, Block block, int cell)
        {
            float fraction = cell / Mathf.Max(1f, Screen.height);
            float alpha = -Mathf.Log10(fraction + 15f) + 1.5f;
            if (block.SubLevel != null && !block.SubLevel.FilledWithWalls)
            {
                alpha = -Mathf.Log10(fraction + 1.5f) + 0.5f;
            }

            alpha = Mathf.Clamp01(alpha);
            if (alpha <= 0.01f)
            {
                return;
            }

            bool possess = !block.DrawIsPlayer && block.Possessable;
            Texture eye = possess ? texPossessEye : texEyeOpen;
            Texture eyeSmall = possess ? texPossessEyeSmall : texEyeOpenSmall;
            if (block.FaceAnimT > 0f && Mathf.Floor(block.FaceAnimT * 7.12f) % 26f == 0f)
            {
                eye = texEyeClosed;
                eyeSmall = texEyeClosedSmall;
            }

            if (cell * 0.2f < 50f && eyeSmall != null)
            {
                eye = eyeSmall;
            }

            if (eye == null)
            {
                return;
            }

            float ew = r.width * 0.2f;
            float eh = r.height * 0.2f;
            float left = -0.25f;
            float right = 0.25f;
            float ey = -0.05f + block.eyeYOffset * 1.5f;
            left += block.eyeXOffset > 0f ? block.eyeXOffset : block.eyeXOffset * 1.5f;
            right += block.eyeXOffset < 0f ? block.eyeXOffset : block.eyeXOffset * 1.5f;
            if (block.FlipH)
            {
                float swap = left;
                left = -right;
                right = -swap;
            }

            Color ink = new Color(0f, 0f, 0f, alpha);
            DrawTextureTinted(new Rect(r.center.x + r.width * left - ew * 0.5f, r.center.y + r.height * ey - eh * 0.5f, ew, eh), eye, ink);
            DrawTextureTinted(new Rect(r.center.x + r.width * right - ew * 0.5f, r.center.y + r.height * ey - eh * 0.5f, ew, eh), eye, ink);

            Texture mouth = null;
            if (block.PlayerOrder == 1)
            {
                mouth = texMouthFrowny;
            }
            else if (block.PlayerOrder == 2)
            {
                mouth = null;
            }
            else if (block.PlayerOrder == 3)
            {
                mouth = texMouthV;
            }
            else if (block.PlayerOrder == 4)
            {
                mouth = texMouthMeh;
            }
            else if (block.PlayerOrder == 5)
            {
                mouth = texMouthOpen;
            }
            else if (block.PlayerOrder >= 6)
            {
                mouth = texMouthSquare;
            }
            else if (World.smile)
            {
                mouth = texMouthSmile;
            }

            if (mouth != null)
            {
                float mw = r.width * 0.27f;
                float mh = r.height * 0.27f;
                float mx = 1.5f * block.eyeXOffset;
                float my = 0.15f + 1.5f * block.eyeYOffset;
                if (block.FlipH)
                {
                    mx *= -1f;
                }

                DrawTextureTinted(new Rect(r.center.x + r.width * mx - mw * 0.5f, r.center.y + r.height * my - mh * 0.5f, mw, mh), mouth, ink);
            }
        }

        private void DrawFlipShineArt(Rect r, Block block, int cell)
        {
            if (!block.FlipH && block.SpecialEffect != 2 && block.SpecialEffect != 3)
            {
                return;
            }

            if (block.SubLevel != null && block.SubLevel.FilledWithWalls)
            {
                return;
            }

            float fraction = cell / Mathf.Max(1f, Screen.height);
            float alpha = Mathf.Min(-Mathf.Log10(fraction + 0.5f) + 0.18f, 0.22f);
            if (alpha <= 0.01f)
            {
                return;
            }

            float t = Mathf.Repeat(block.MiscAnimT * 0.5f + Time.unscaledTime * 0.35f, 1f);
            if (t >= 1f)
            {
                return;
            }

            if (block.FlipH || block.SpecialEffect == 3)
            {
                t = 1f - t;
            }

            float band = 0.4f * r.width * (t < 0.5f
                ? QuadIn(t * 2f)
                : QuadIn(1f - (t - 0.5f) * 2f));
            float center = Mathf.Lerp(r.x, r.xMax, QuadInOut(QuadInOut(t)));
            Fill(new Rect(center - band * 0.5f, r.y, band, r.height), new Color(1f, 1f, 1f, alpha));
        }

        private static float QuadIn(float value)
        {
            return value * value;
        }

        private static float QuadInOut(float value)
        {
            return value < 0.5f
                ? 2f * value * value
                : 1f - 2f * (1f - value) * (1f - value);
        }

        private void DrawInfSymbols(Rect r, Block block, int cell)
        {            Texture texture;
            int count;
            if (block.IsSomeInfEnterBlock)
            {
                texture = texInfOutline;
                count = Mathf.Max(1, block.SomeInfEnterNum + 1);
            }
            else if (block.IsSomeInfExitBlock && block.SubLevel != null)
            {
                texture = texInf;
                int index = block.SubLevel.infExitBlocks != null
                    ? block.SubLevel.infExitBlocks.IndexOf(block)
                    : -1;
                count = index >= 0 ? index + 1 : 1;
            }
            else
            {
                return;
            }

            if (count <= 0)
            {
                return;
            }

            if (texture == null)
            {
                string glyph = block.IsSomeInfEnterBlock ? "ε" : "∞";
                int shown = Mathf.Min(count, 4);
                for (int i = 0; i < shown; i++)
                {
                    GUI.Label(
                        new Rect(r.x, r.y + r.height * i / shown, r.width, r.height / shown),
                        glyph,
                        centerLabelStyle);
                }
                return;
            }

            float alpha = Mathf.Clamp01(
                -Mathf.Log10(((r.width + r.height) * 0.5f) / Mathf.Max(1f, Screen.height) + 0.5f) + 0.22f);
            Color tint = new Color(1f, 1f, 1f, alpha);
            if (count == 1)
            {
                float w = r.width * 0.9f;
                float h = r.height * 0.9f;
                DrawTextureTinted(new Rect(r.center.x - w * 0.5f, r.center.y - h * 0.5f, w, h), texture, tint);
                return;
            }

            float symbolW = r.width / (count + 1f);
            float symbolH = r.height / (count + 1f);
            float gap = (r.height - symbolH * count) / (count + 1f);
            float drawW = symbolW * 1.8f;
            float drawH = symbolH * 1.8f;
            float startY = r.center.y - drawH * 0.5f - (symbolH * 0.5f + gap * 0.5f) * (count - 1);
            for (int i = 0; i < count; i++)
            {
                DrawTextureTinted(
                    new Rect(r.center.x - drawW * 0.5f, startY + i * (symbolH + gap), drawW, drawH),
                    texture,
                    tint);
            }
        }

        private Color GameWallColor(Block owner, int cell)
        {
            if (owner == null)
            {
                return new Color(0.34f, 0.36f, 0.42f, 1f);
            }

            float fraction = cell / Mathf.Max(1f, Screen.height);
            float val = owner.val * WallBrightness(fraction) * 2f * 1.10f;
            float sat = owner.sat;
            bool clone = false;
            if (owner.IsSomeInfExitBlock)
            {
                val *= 0.55f;
            }
            else if (owner.SubLevel != null && LevelGraph.IsCloneBox(owner))
            {
                val += 0.4f;
                sat *= 0.82f;
                clone = true;
            }

            val = Mathf.Clamp(val, 0.22f, 1f);
            Color color = Hsv(owner.hue, sat, val);
            if (clone)
            {
                // Clone walls must read brighter even when val has already saturated
                // at 1 (small nested cells).
                color = Color.Lerp(color, Color.white, 0.22f);
            }

            return color;
        }

        private Color GameBlockColor(Block block, int cell)
        {
            float hue;
            float sat;
            float val;
            GameBlockHsv(block, cell, out hue, out sat, out val);
            return Hsv(hue, sat, val);
        }

        private static void GameBlockHsv(Block block, int cell, out float hue, out float sat, out float val)
        {
            Block owner = LevelGraph.ExitBlockOf(block.OuterLevel);
            hue = block.hue;
            sat = block.sat;
            val = block.val;
            if (val <= 0.0001f && owner != null)
            {
                hue = owner.hue;
                sat = owner.sat;
                val = owner.val;
            }

            float fraction = cell / Mathf.Max(1f, Screen.height);
            val = val * BlockBrightness(fraction) * 0.9f;
            if (block.IsSomeInfExitBlock)
            {
                val *= 0.55f;
            }
            else if (LevelGraph.IsCloneBox(block))
            {
                val += 0.42f;
                sat *= 0.76f;
            }

            hue = Mathf.Repeat(hue, 1f);
            sat = Mathf.Clamp01(sat);
            val = Mathf.Clamp(val, 0.12f, 1f);
        }

        private Color GamePlayerColor(Block block, int cell)
        {
            if (block.val > 0.0001f || block.sat > 0.0001f)
            {
                return GameBlockColor(block, cell);
            }

            Block owner = LevelGraph.ExitBlockOf(block.OuterLevel);
            if (owner != null)
            {
                Color baseColor = GameBlockColor(owner, cell);
                Color mixed = Color.Lerp(baseColor, PlayerCol, 0.45f);
                mixed.a = baseColor.a;
                return mixed;
            }

            Color player = Color.Lerp(PlayerCol, Color.white, 0.2f);
            player.a = 1f;
            return player;
        }

        private Color GameFloorColor(Floor floor, int cell)
        {
            if (floor.Type == Floor.FloorType.DemoEnd)
            {
                return new Color(0.05f, 0.05f, 0.06f, 1f);
            }

            Block owner = LevelGraph.ExitBlockOf(floor.OuterLevel);
            if (owner == null)
            {
                return FloorBaseColor(floor.Type);
            }

            Color baseColor = GameBlockColor(owner, cell);
            Color mixed = Color.Lerp(baseColor, Color.white, 0.10f);
            mixed.a = baseColor.a;
            return mixed;
        }

        private Color LevelBackgroundColor(Level level)
        {
            Block owner = LevelGraph.ExitBlockOf(level);
            if (owner == null)
            {
                return new Color(0.06f, 0.07f, 0.09f, 1f);
            }

            return GameBlockColor(owner, 24);
        }

        private static float GameBorderThickness(float width, float height)
        {
            float value = Mathf.Min(width, height) / 30f;
            float max = Mathf.Min(Screen.width, Screen.height) * 0.00508f;
            return Mathf.Max(Mathf.Round(Mathf.Clamp(value, 1f, max)), 1f);
        }

        private static float WallBrightness(float fraction)
        {
            return -0.25f * Mathf.Log(fraction + 0.005f, 20f) + 0.5f;
        }

        private static float BlockBrightness(float fraction)
        {
            return -0.25f * Mathf.Log(fraction + 0.005f, 20f) + 0.5f;
        }

        private static Color Hsv(float hue, float sat, float val)
        {
            // The game draws through a custom material; in IMGUI the material's
            // compensation is absent, so use the full RGB/alpha to match the
            // visible game brightness. The source HSVToRGBGUI halves both only for
            // that material path.
            return Color.HSVToRGB(Mathf.Repeat(hue, 1f), Mathf.Clamp01(sat), Mathf.Clamp01(val));
        }

        private static Texture PickTexture(Texture[] textures, int index)
        {
            if (textures == null || index < 0 || index >= textures.Length)
            {
                return null;
            }

            return textures[index];
        }

        private void DrawCursor(Level level, float originX, float originY, int cell, Rect canvas)
        {
            if (!level.InBounds(cursorX, cursorY))
            {
                return;
            }

            int row = level.height - 1 - cursorY;
            var r = new Rect(originX + cursorX * cell, originY + row * cell, cell, cell);
            if (!Overlaps(r, canvas))
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

            if (block != null && block.SubLevel != null)
            {
                string attributes = BlockAttributes(block);
                if (!string.IsNullOrEmpty(attributes))
                {
                    info += " · " + attributes;
                }
            }

            return coord + "  " + info;
        }

        private static string BlockAttributes(Block block)
        {
            string flags = LevelGraph.BlockFlags(block);
            if (block.SubLevel != null)
            {
                if (flags == "box")
                {
                    return string.Empty;
                }

                if (flags.StartsWith("box "))
                {
                    return flags.Substring(4);
                }
            }

            return flags;
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
            return Color.black;
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
            GUI.Label(new Rect(rect.x + 12f, rect.y + 6f, rect.width - 24f, 22f),
                Ellipsize(UiText.Get("hint"), smallStyle, rect.width - 24f), smallStyle);

            if (!string.IsNullOrEmpty(message) && Time.unscaledTime < messageUntil)
            {
                Color old = GUI.color;
                GUI.color = messageIsError ? new Color(1f, 0.5f, 0.45f, 1f) : new Color(0.55f, 0.95f, 0.65f, 1f);
                GUI.Label(new Rect(rect.x + 12f, rect.y + 30f, rect.width - 24f, 24f),
                    Ellipsize(message, statusStyle, rect.width - 24f), statusStyle);
                GUI.color = old;
            }
            else if (selectedNode != null)
            {
                string status = UiText.Get("level") + ": " + selectedNode.Title
                    + "   " + selectedNode.Subtitle;
                GUI.Label(new Rect(rect.x + 12f, rect.y + 30f, rect.width - 24f, 24f),
                    Ellipsize(status, smallStyle, rect.width - 24f), smallStyle);
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
                string label = (infMenuEnter ? UiText.Get("inf_enter") : UiText.Get("inf_exit"))
                    + " #" + next + UiText.Get("not_generated");
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
                texWallTiles = GetPrivateTextureArray("WallTiles");
                texWallTilesFlip = GetPrivateTextureArray("WallTilesFlip");
                texWallBig = GetPrivateTextureArray("WallBig");
                texWallBigFlip = GetPrivateTextureArray("WallBigFlip");
                texGradient = GetPrivateTexture("CenterGradientTex");
                texInf = GetPrivateTexture("InfParticleTex");
                texInfOutline = GetPrivateTexture("InfParticleOutlineTex");
                texEyeOpen = GetPrivateTexture("EyeOpen");
                texEyeOpenSmall = GetPrivateTexture("EyeOpenSmall");
                texEyeClosed = GetPrivateTexture("EyeClosed");
                texEyeClosedSmall = GetPrivateTexture("EyeClosedSmall");
                texPossessEye = GetPrivateTexture("PossessEye");
                texPossessEyeSmall = GetPrivateTexture("PossessEyeSmall");
                texMouthFrowny = GetPrivateTexture("FrownyMouth");
                texMouthFlat = GetPrivateTexture("FlatMouth");
                texMouthOpen = GetPrivateTexture("OpenMouth");
                texMouthV = GetPrivateTexture("VMouth");
                texMouthMeh = GetPrivateTexture("MehMouth");
                texMouthSmile = GetPrivateTexture("SmileMouth");
                texMouthSquare = GetPrivateTexture("SquareMouth");
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

            titleStyle.wordWrap = false;
            sectionStyle.wordWrap = false;
            labelStyle.wordWrap = false;
            smallStyle.wordWrap = false;
            tinyStyle.wordWrap = false;
            tinyCenterStyle.wordWrap = false;
            statusStyle.wordWrap = false;
            rightStyle.wordWrap = false;
            centerLabelStyle.wordWrap = false;
            bannerStyle.wordWrap = false;
            buttonStyle.wordWrap = false;
            arrowStyle.wordWrap = false;

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

        private static Texture[] GetPrivateTextureArray(string fieldName)
        {
            try
            {
                if (Draw.instance != null)
                {
                    FieldInfo field = typeof(Draw).GetField(
                        fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
                    if (field != null)
                    {
                        return field.GetValue(Draw.instance) as Texture[];
                    }
                }
            }
            catch
            {
                // Optional cosmetic textures.
            }

            return null;
        }

        private void EnsureGameArtTextures()
        {
            if (texWallTiles == null)
            {
                texWallTiles = GetPrivateTextureArray("WallTiles");
                texWallTilesFlip = GetPrivateTextureArray("WallTilesFlip");
                texWallBig = GetPrivateTextureArray("WallBig");
                texWallBigFlip = GetPrivateTextureArray("WallBigFlip");
            }

            if (texGradient == null)
            {
                texGradient = GetPrivateTexture("CenterGradientTex");
            }

            if (texInf == null)
            {
                texInf = GetPrivateTexture("InfParticleTex");
            }

            if (texInfOutline == null)
            {
                texInfOutline = GetPrivateTexture("InfParticleOutlineTex");
            }

            if (texEyeOpen == null)
            {
                texEyeOpen = GetPrivateTexture("EyeOpen");
                texEyeOpenSmall = GetPrivateTexture("EyeOpenSmall");
                texEyeClosed = GetPrivateTexture("EyeClosed");
                texEyeClosedSmall = GetPrivateTexture("EyeClosedSmall");
                texPossessEye = GetPrivateTexture("PossessEye");
                texPossessEyeSmall = GetPrivateTexture("PossessEyeSmall");
                texMouthFrowny = GetPrivateTexture("FrownyMouth");
                texMouthFlat = GetPrivateTexture("FlatMouth");
                texMouthOpen = GetPrivateTexture("OpenMouth");
                texMouthV = GetPrivateTexture("VMouth");
                texMouthMeh = GetPrivateTexture("MehMouth");
                texMouthSmile = GetPrivateTexture("SmileMouth");
                texMouthSquare = GetPrivateTexture("SquareMouth");
            }
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

        private static bool Overlaps(Rect rect, Rect canvas)
        {
            return rect.xMax > canvas.x && rect.x < canvas.xMax
                && rect.yMax > canvas.y && rect.y < canvas.yMax;
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
