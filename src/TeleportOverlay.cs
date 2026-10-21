using System;
using System.Collections.Generic;
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
        private static readonly Color BackdropColor = new Color(0.04f, 0.05f, 0.07f, 0.96f);
        private static readonly Color PanelColor = new Color(0.1f, 0.11f, 0.14f, 1f);
        private static readonly Color HeaderColor = new Color(0.14f, 0.16f, 0.2f, 1f);
        private static readonly Color PlayerColor = new Color(0.95f, 0.85f, 0.25f, 1f);
        private static readonly Color WallColor = new Color(0.22f, 0.24f, 0.28f, 1f);
        private static readonly Color EmptyColor = new Color(0.16f, 0.18f, 0.22f, 1f);
        private static readonly Color BoxColor = new Color(0.93f, 0.58f, 0.25f, 1f);
        private static readonly Color PortalColor = new Color(0.72f, 0.45f, 0.95f, 1f);
        private static readonly Color TargetColor = new Color(0.35f, 0.78f, 0.42f, 1f);
        private static readonly Color CursorColor = new Color(1f, 1f, 1f, 0.95f);
        private static readonly Color MutedColor = new Color(0.62f, 0.66f, 0.72f, 1f);

        private readonly ModConfig config;
        private readonly TeleportService teleporter;
        private readonly LevelGraph graph = new LevelGraph();
        private readonly List<LevelNode> rows = new List<LevelNode>();

        private Texture2D white;
        private GUIStyle titleStyle;
        private GUIStyle labelStyle;
        private GUIStyle smallStyle;
        private GUIStyle rightSmallStyle;
        private GUIStyle buttonStyle;
        private GUIStyle statusStyle;

        private bool open;
        private int suppressFrames;
        private World.WS savedState;
        private LevelNode selectedNode;
        private int cursorX;
        private int cursorY;
        private float zoom = 1f;
        private Vector2 pan;
        private Vector2 treeScroll;
        private Rect lastMapCanvas;
        private Rect lastGrid;
        private Vector2 lastMousePos = new Vector2(-1f, -1f);
        private bool cursorWasVisible = true;
        private string message = string.Empty;
        private float messageUntil;
        private bool messageIsError;
        private string sceneAtBuild = string.Empty;
        private int rootsAtBuild = -1;
        private int levelsAtBuild = -1;

        // Box -> resolved landing cell. A box's interior never changes while the browser is
        // open (the game is frozen), so resolving each box once avoids per-frame BFS work.
        private readonly Dictionary<Block, TeleportCell> enterableCache =
            new Dictionary<Block, TeleportCell>();
        private readonly HashSet<Block> notEnterable = new HashSet<Block>();

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

            if (World.State != World.WS.Playing)
            {
                Notify(UiText.Get("win_blocked_state"), true);
                return;
            }

            if (!GameState.HasPlayer)
            {
                Notify(UiText.Get("no_players"), true);
                return;
            }

            if (white == null)
            {
                white = Texture2D.whiteTexture;
            }

            savedState = World.State;
            UiText.Refresh();
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
            titleStyle = null; // rebuild styles with the font of the current scene
 
            zoom = Mathf.Clamp(config.CellSize / 24f, 0.25f, 4f);
            pan = Vector2.zero;
            treeScroll = Vector2.zero;
            message = string.Empty;
            Mouse mouse = Mouse.current;
            if (mouse != null)
            {
                Vector2 p = mouse.position.ReadValue();
                lastMousePos = new Vector2(p.x, Screen.height - p.y);
            }
            else
            {
                lastMousePos = new Vector2(-1f, -1f);
            }

            ScrollTreeToSelection();
            Log.Info("Teleport map opened: " + rows.Count + " level nodes, scene '" + sceneAtBuild + "'");
            if (config.LogSceneTree)
            {
                LogTreeLayout();
            }
        }

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
                + (node.ContainsPlayer ? "  <player>" : string.Empty)
                + "  #" + node.Level.width + "x" + node.Level.height);
            foreach (LevelNode child in node.Children)
            {
                LogNode(child);
            }
        }

        private void ScrollTreeToSelection()
        {
            int index = rows.IndexOf(selectedNode);
            if (index >= 0)
            {
                treeScroll.y = Mathf.Max(0f, index * 26f - 26f * 4f);
            }
        }

        public void Close()
        {
            if (!open)
            {
                return;
            }

            open = false;
            // Swallow the input that closed the browser (Esc/Enter/click) so the game does
            // not immediately open the pause menu or trigger a floor button on resume.
            suppressFrames = 2;
            Cursor.visible = cursorWasVisible;
            World.State = savedState == World.WS.Initializing ? World.WS.Playing : savedState;
            Log.Info("Teleport map closed");
        }

        public void Notify(string text, bool isError)
        {
            message = text ?? string.Empty;
            messageIsError = isError;
            messageUntil = Time.unscaledTime + 2.5f;
        }

        private void ClearMessage()
        {
            message = string.Empty;
        }

        // ------------------------------------------------------------------ graph

        private void RebuildGraph(bool keepSelection)
        {
            enterableCache.Clear();
            notEnterable.Clear();
            graph.Build();
            sceneAtBuild = World.currentLevelName;
            rootsAtBuild = graph.Roots.Count;
            levelsAtBuild = World.levels == null ? 0 : World.levels.Count;

            LevelNode previous = keepSelection ? selectedNode : null;
            selectedNode = null;

            if (previous != null)
            {
                foreach (LevelNode node in graph.AllNodes)
                {
                    if (node.Level == previous.Level)
                    {
                        selectedNode = node;
                        break;
                    }
                }
            }

            if (selectedNode == null && graph.PlayerNode != null)
            {
                selectedNode = graph.PlayerNode;
            }

            if (selectedNode == null && graph.Roots.Count > 0)
            {
                selectedNode = graph.Roots[0];
            }

            if (selectedNode != null)
            {
                // Only reset the cursor when we switched to a different level.
                if (previous == null || previous.Level != selectedNode.Level)
                {
                    PlaceCursorAtDefault(selectedNode);
                }
            }

            FlattenTree();
        }

        private void FlattenTree()
        {
            rows.Clear();
            foreach (LevelNode root in graph.Roots)
            {
                AddRow(root);
            }
        }

        private void AddRow(LevelNode node)
        {
            rows.Add(node);
            foreach (LevelNode child in node.Children)
            {
                AddRow(child);
            }
        }

        private void SelectNode(LevelNode node)
        {
            if (node == null || node == selectedNode)
            {
                return;
            }

            selectedNode = node;
            zoom = 1f;
            pan = Vector2.zero;
            PlaceCursorAtDefault(node);
            ClearMessage();
        }

        private void SelectRow(int index)
        {
            if (index < 0 || index >= rows.Count)
            {
                return;
            }

            SelectNode(rows[index]);
            ClampTreeScrollToRow(index);
            ClearMessage();
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

            SetCursor(x, y);
        }

        // ------------------------------------------------------------------- input

        /// <summary>Called once per frame by the driver, before World.UpdateInner is skipped.</summary>
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

            // The scene can change underneath us (CustomHubs F5/portal reloads).
            if (World.currentLevelName != sceneAtBuild
                || graph.Roots.Count != rootsAtBuild
                || (World.levels != null ? World.levels.Count : 0) != levelsAtBuild)
            {
                RebuildGraph(true);
            }

            if (keyboard.escapeKey.wasPressedThisFrame)
            {
                Close();
                return;
            }

            if (config.TeleportPressed())
            {
                Close();
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

            if (Pressed(keyboard, Key.Enter) || Pressed(keyboard, Key.NumpadEnter) || Pressed(keyboard, Key.Space))
            {
                ConfirmCursor();
            }

            Mouse mouse = Mouse.current;
            if (mouse != null && selectedNode != null && selectedNode.Level != null)
            {
                // Input System mouse coordinates are bottom-left based; IMGUI is top-left.
                Vector2 screenMouse = mouse.position.ReadValue();
                var guiMouse = new Vector2(screenMouse.x, Screen.height - screenMouse.y);
                if (lastMapCanvas.Contains(guiMouse))
                {
                    float wheel = mouse.scroll.ReadValue().y;
                    if (Mathf.Abs(wheel) > 0.01f)
                    {
                        ZoomAt(guiMouse, wheel > 0f ? 0.9f : 1.1f);
                    }
                }
            }
        }

        private void ZoomAt(Vector2 mouse, float factor)
        {
            Level level = selectedNode?.Level;
            if (level == null)
            {
                return;
            }

            float before = lastGrid.width / Mathf.Max(1, level.width);
            zoom = Mathf.Clamp(zoom * factor, 0.25f, 4f);

            // Recompute the cell size exactly like DrawMap does, then keep the point
            // under the mouse approximately fixed.
            float fit = Mathf.Min(lastMapCanvas.width / level.width, lastMapCanvas.height / level.height);
            float after = Mathf.Clamp(fit * 0.92f * zoom, 3f, 96f);
            if (before > 0.5f && after > 0.5f)
            {
                float ratio = after / before;
                float centerX = lastGrid.center.x - pan.x;
                float centerY = lastGrid.center.y - pan.y;
                pan.x = pan.x * ratio + (mouse.x - centerX) * (1f - ratio);
                pan.y = pan.y * ratio + (mouse.y - centerY) * (1f - ratio);
            }
        }

        private static bool Pressed(Keyboard keyboard, Key key)
        {
            return keyboard[key].wasPressedThisFrame;
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
            if (selectedNode == null)
            {
                return;
            }

            TryTeleportTo(selectedNode, cursorX, cursorY);
        }

        private void TryTeleportTo(LevelNode node, int x, int y)
        {
            if (node == null || node.Level == null)
            {
                return;
            }

            if (x < 0 || y < 0 || x >= node.Level.width || y >= node.Level.height)
            {
                return;
            }

            Block block = node.Level.blocks[x, y];
            if (block == null)
            {
                if (!LevelGraph.IsWalkableCell(node.Level, x, y))
                {
                    Notify(UiText.Get("not_walkable"), true);
                    return;
                }

                TeleportCell direct = new TeleportCell
                {
                    Level = node.Level,
                    X = x,
                    Y = y,
                    ViaBox = false,
                };

                DoTeleport(direct);
                return;
            }

            TeleportCell entered;
            if (IsEnterable(block, out entered))
            {
                DoTeleport(entered);
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

        // -------------------------------------------------------------------- draw

        public void OnGUI()
        {
            EnsureStyles();
            GUI.depth = -100;

            if (!open)
            {
                DrawToast();
                return;
            }

            float w = Screen.width;
            float h = Screen.height;

            Fill(new Rect(0f, 0f, w, h), BackdropColor);

            float headerH = Mathf.Clamp(h * 0.075f, 52f, 76f);
            float footerH = Mathf.Clamp(h * 0.11f, 72f, 96f);
            float margin = 14f;
            float gap = 12f;
            float treeW = Mathf.Clamp(w * 0.28f, 300f, 420f);

            Rect headerRect = new Rect(0f, 0f, w, headerH);
            Fill(headerRect, HeaderColor);
            DrawHeader(headerRect);
            if (!open)
            {
                return;
            }

            float bodyTop = headerH + margin;
            float bodyH = Mathf.Max(80f, h - footerH - bodyTop - margin);
            Rect treeRect = new Rect(margin, bodyTop, treeW, bodyH);
            Rect mapRect = new Rect(treeRect.xMax + gap, bodyTop, w - treeW - margin * 2f - gap, bodyH);

            DrawTree(treeRect);
            if (open)
            {
                DrawMap(mapRect);
            }

            if (open)
            {
                DrawFooter(new Rect(margin, h - footerH, w - margin * 2f, footerH - margin));
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
            Fill(rect, new Color(0.07f, 0.08f, 0.11f, 0.94f));
            DrawBorder(
                rect,
                messageIsError
                    ? new Color(1f, 0.45f, 0.4f, 1f)
                    : new Color(0.5f, 0.9f, 0.6f, 1f),
                1f);
            GUI.Label(new Rect(rect.x + 14f, rect.y + 4f, rect.width - 28f, 28f), message, statusStyle);
        }

        private void DrawHeader(Rect rect)
        {
            string title = UiText.Get("title");
            GUI.Label(new Rect(rect.x + 18f, rect.y + 8f, 360f, 30f), title, titleStyle);

            string scene = UiText.Get("scene") + ": " + (sceneAtBuild == "custom_level" ? graph.SceneLabel : sceneAtBuild);
            GUI.Label(new Rect(rect.x + 18f, rect.y + 32f, 520f, 24f), scene + "    " + UiText.Get("opened") + ": " + rows.Count, smallStyle);

            if (selectedNode != null)
            {
                string path = selectedNode.Path;
                GUI.Label(new Rect(rect.x + 560f, rect.y + 10f, Mathf.Max(120f, rect.width - 760f), 24f),
                    UiText.Get("level") + ": " + path, labelStyle);
                GUI.Label(new Rect(rect.x + 560f, rect.y + 34f, Mathf.Max(120f, rect.width - 760f), 22f),
                    selectedNode.Subtitle, smallStyle);
            }

            if (GUI.Button(new Rect(rect.xMax - 180f, rect.y + 12f, 160f, 30f), UiText.Get("close"), buttonStyle))
            {
                Close();
            }
        }

        private void DrawTree(Rect rect)
        {
            Fill(rect, PanelColor);

            float rowH = 26f;
            float contentH = Mathf.Max(rect.height, rows.Count * rowH + 8f);
            Rect viewRect = new Rect(rect.x + 4f, rect.y + 6f, rect.width - 8f, rect.height - 12f);
            Rect contentRect = new Rect(0f, 0f, viewRect.width - 18f, contentH);

            treeScroll = GUI.BeginScrollView(viewRect, treeScroll, contentRect);
            int start = Mathf.Clamp(Mathf.FloorToInt(treeScroll.y / rowH), 0, Mathf.Max(0, rows.Count - 1));
            int visible = Mathf.CeilToInt(viewRect.height / rowH) + 2;

            for (int i = start; i < rows.Count && i < start + visible; i++)
            {
                LevelNode node = rows[i];
                Rect rowRect = new Rect(contentRect.x, i * rowH, contentRect.width, rowH - 2f);
                bool isSelected = node == selectedNode;

                if (isSelected)
                {
                    Fill(rowRect, new Color(0.2f, 0.34f, 0.44f, 1f));
                }
                else
                {
                    // rowRect is in scroll-content space; hover testing needs screen space.
                    var screenRow = new Rect(
                        rowRect.x + viewRect.x - treeScroll.x,
                        rowRect.y + viewRect.y - treeScroll.y,
                        rowRect.width,
                        rowRect.height);
                    if (MouseIn(screenRow, viewRect))
                    {
                        Fill(rowRect, new Color(0.16f, 0.18f, 0.23f, 1f));
                    }
                }

                if (GUI.Button(rowRect, GUIContent.none, GUIStyle.none))
                {
                    SelectNode(node);
                }

                float indent = node.Depth * 14f;
                string prefix = node.Children.Count > 0 ? "- " : "  ";
                string title = prefix + node.Title;
                Color old = GUI.color;
                if (node.ContainsPlayer)
                {
                    GUI.color = PlayerColor;
                }
                else if (!isSelected)
                {
                    GUI.color = new Color(0.86f, 0.9f, 0.95f, 1f);
                }

                GUI.Label(new Rect(rowRect.x + 6f + indent, rowRect.y + 2f, rowRect.width - indent - 6f, rowH), title, labelStyle);
                GUI.color = old;

                if (node.ContainsPlayer)
                {
                    GUI.Label(
                        new Rect(rowRect.xMax - 96f, rowRect.y + 2f, 88f, rowH),
                        "< " + UiText.Get("current"),
                        rightSmallStyle);
                }
            }

            GUI.EndScrollView();
        }

        private void DrawMap(Rect rect)
        {
            Fill(rect, PanelColor);
            if (selectedNode == null || selectedNode.Level == null)
            {
                GUI.Label(new Rect(rect.x + 20f, rect.y + 20f, rect.width - 40f, 30f), UiText.Get("no_levels"), labelStyle);
                return;
            }

            Level level = selectedNode.Level;
            float pad = 12f;
            float legendH = 28f;
            float detailH = 26f;
            Rect canvas = new Rect(
                rect.x + pad,
                rect.y + pad + detailH,
                rect.width - pad * 2f,
                rect.height - pad * 2f - legendH - detailH);
            lastMapCanvas = canvas;

            DrawLegend(new Rect(canvas.x, canvas.yMax + 4f, canvas.width, legendH));

            float fit = Mathf.Min(canvas.width / level.width, canvas.height / level.height);
            float cell = Mathf.Clamp(fit * 0.92f * zoom, 3f, 96f);
            float gridW = cell * level.width;
            float gridH = cell * level.height;
            float originX = canvas.x + (canvas.width - gridW) * 0.5f;
            float originY = canvas.y + (canvas.height - gridH) * 0.5f;

            // Keep some grid visible when panning.
            float maxPanX = Mathf.Max(0f, (gridW - canvas.width) * 0.5f + canvas.width * 0.25f);
            float maxPanY = Mathf.Max(0f, (gridH - canvas.height) * 0.5f + canvas.height * 0.25f);
            pan.x = Mathf.Clamp(pan.x, -maxPanX, maxPanX);
            pan.y = Mathf.Clamp(pan.y, -maxPanY, maxPanY);
            originX += pan.x;
            originY += pan.y;

            HandleMapInput(canvas, level, originX, originY, cell);
            if (!open)
            {
                return;
            }

            lastGrid = new Rect(originX, originY, gridW, gridH);

            Block player = GameState.Player;
            int px = player != null && player.OuterLevel == level ? player.xpos : int.MinValue;
            int py = player != null && player.OuterLevel == level ? player.ypos : int.MinValue;

            for (int y = 0; y < level.height; y++)
            {
                for (int x = 0; x < level.width; x++)
                {
                    float sx = originX + x * cell;
                    float sy = originY + (level.height - 1 - y) * cell;
                    var r = new Rect(sx, sy, cell, cell);
                    if (!RectInside(r, canvas))
                    {
                        // Skip cells that would paint over the header/legend/panels.
                        continue;
                    }

                    Block block = level.blocks[x, y];
                    Floor floor = level.floors[x, y];
                    Color color;
                    if (block == null)
                    {
                        if (floor != null && floor.Type == Floor.FloorType.LevelPortal)
                        {
                            color = new Color(PortalColor.r, PortalColor.g, PortalColor.b, floor.Unlocked ? 0.75f : 0.28f);
                        }
                        else
                        {
                            color = EmptyColor;
                        }
                    }
                    else if (block.IsPlayer)
                    {
                        color = PlayerColor;
                    }
                    else if (block.SubLevel != null)
                    {
                        color = BoxColor;
                    }
                    else
                    {
                        color = WallColor;
                    }

                    Fill(r, color);
                    if (cell >= 10f)
                    {
                        DrawBorder(r, new Color(0f, 0f, 0f, 0.25f), 1f);
                    }

                    if (x == px && y == py && cell >= 8f)
                    {
                        Fill(Centered(r, cell * 0.5f), new Color(0.05f, 0.05f, 0.08f, 1f));
                        Fill(Centered(r, cell * 0.26f), PlayerColor);
                    }

                    if (block != null && block.SubLevel != null && cell >= 14f)
                    {
                        DrawBorder(r, new Color(0.1f, 0.08f, 0.05f, 0.85f), 2f);
                        TeleportCell entered;
                        if (IsEnterable(block, out entered))
                        {
                            Fill(Centered(r, cell * 0.22f), TargetColor);
                        }
                    }

                    if (block == null && floor != null && floor.Type == Floor.FloorType.LevelPortal && cell >= 14f)
                    {
                        DrawDiamond(r, PortalColor);
                    }
                }
            }

            var gridRect = new Rect(originX - 1f, originY - 1f, gridW + 2f, gridH + 2f);
            if (RectInside(gridRect, canvas))
            {
                DrawBorder(gridRect, new Color(0.4f, 0.45f, 0.5f, 0.9f), 1f);
            }

            // Cursor.
            if (cursorX >= 0 && cursorY >= 0 && cursorX < level.width && cursorY < level.height)
            {
                Rect cr = new Rect(originX + cursorX * cell, originY + (level.height - 1 - cursorY) * cell, cell, cell);
                if (RectInside(cr, canvas))
                {
                    float pulse = 0.55f + 0.45f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 4f));
                    DrawBorder(cr, new Color(CursorColor.r, CursorColor.g, CursorColor.b, pulse), Mathf.Max(2f, cell * 0.08f));
                    if (cell >= 18f)
                    {
                        Fill(new Rect(cr.x + cr.width * 0.42f, cr.y + cr.height * 0.42f, cr.width * 0.16f, cr.height * 0.16f),
                            new Color(1f, 1f, 1f, 0.9f));
                    }
                }
            }

            // Context line above the map.
            string detail = BuildCellDetail(level, cursorX, cursorY);
            GUI.Label(new Rect(canvas.x, rect.y + pad, canvas.width, detailH), detail, labelStyle);
        }

        private string BuildCellDetail(Level level, int x, int y)
        {
            if (!level.InBounds(x, y))
            {
                return string.Empty;
            }

            string coord = "(" + x + ", " + y + ")";
            Block block = level.blocks[x, y];
            if (block == null)
            {
                Floor floor = level.floors[x, y];
                if (floor != null && floor.Type == Floor.FloorType.LevelPortal)
                {
                    return coord + "  " + UiText.Get("legend_portal") + " -> " + floor.SceneName;
                }

                return coord + "  " + UiText.Get("empty_cell");
            }

            TeleportCell entered;
            if (IsEnterable(block, out entered))
            {
                string name = entered.Level.hubAreaName;
                if (string.IsNullOrEmpty(name))
                {
                    name = entered.Level.width + "x" + entered.Level.height;
                }

                return coord + "  " + UiText.Get("will_enter") + " '" + name.Replace('_', ' ') + "' (" + entered.X + ", " + entered.Y + ")";
            }

            return coord + "  " + (block.IsPlayer ? UiText.Get("legend_player") : UiText.Get("blocked"));
        }

        private void HandleMapInput(Rect canvas, Level level, float originX, float originY, float cell)
        {
            Event e = Event.current;
            if (e == null)
            {
                return;
            }

            Vector2 mouse = e.mousePosition;

            // Cursor follows the mouse only when the mouse actually moved; otherwise arrow-key
            // movement would be snapped back to a resting mouse position every frame.
            if (e.type == EventType.Repaint && canvas.Contains(mouse)
                && (mouse - lastMousePos).sqrMagnitude > 0.01f)
            {
                lastMousePos = mouse;
                int hx = Mathf.FloorToInt((mouse.x - originX) / cell);
                int hy = level.height - 1 - Mathf.FloorToInt((mouse.y - originY) / cell);
                if (level.InBounds(hx, hy))
                {
                    cursorX = hx;
                    cursorY = hy;
                }
            }

            // Click = teleport to the hovered cell.
            if (e.type == EventType.MouseUp && e.button == 0 && canvas.Contains(mouse))
            {
                int hx = Mathf.FloorToInt((mouse.x - originX) / cell);
                int hy = level.height - 1 - Mathf.FloorToInt((mouse.y - originY) / cell);
                if (level.InBounds(hx, hy))
                {
                    cursorX = hx;
                    cursorY = hy;
                    TryTeleportTo(selectedNode, hx, hy);
                    e.Use();
                }
            }

            // Right / middle drag pans the map.
            if (e.type == EventType.MouseDrag && (e.button == 1 || e.button == 2))
            {
                pan += e.delta;
                e.Use();
            }
        }

        private void DrawLegend(Rect rect)
        {
            float x = rect.x;
            float y = rect.y + 4f;
            x = LegendItem(x, y, WallColor, UiText.Get("legend_wall"));
            x = LegendItem(x, y, BoxColor, UiText.Get("legend_box"));
            x = LegendItem(x, y, PortalColor, UiText.Get("legend_portal"));
            x = LegendItem(x, y, PlayerColor, UiText.Get("legend_player"));
            x = LegendItem(x, y, TargetColor, UiText.Get("legend_target"));
            LegendItem(x, y, CursorColor, UiText.Get("legend_cursor"));
        }

        private float LegendItem(float x, float y, Color color, string text)
        {
            Fill(new Rect(x, y + 4f, 14f, 14f), color);
            float labelWidth = smallStyle.CalcSize(new GUIContent(text)).x;
            GUI.Label(new Rect(x + 18f, y, labelWidth + 4f, 22f), text, smallStyle);
            return x + 18f + labelWidth + 22f;
        }

        private void DrawFooter(Rect rect)
        {
            Fill(rect, PanelColor);
            GUI.Label(new Rect(rect.x + 14f, rect.y + 8f, rect.width - 28f, 24f), UiText.Get("hint"), smallStyle);

            if (!string.IsNullOrEmpty(message) && Time.unscaledTime < messageUntil)
            {
                Color old = GUI.color;
                GUI.color = messageIsError ? new Color(1f, 0.5f, 0.45f, 1f) : new Color(0.55f, 0.95f, 0.65f, 1f);
                GUI.Label(new Rect(rect.x + 14f, rect.y + 32f, rect.width - 28f, 26f), message, statusStyle);
                GUI.color = old;
            }
            else
            {
                string status = string.Empty;
                if (selectedNode != null)
                {
                    status = UiText.Get("level") + ": " + selectedNode.Title + "   " + selectedNode.Subtitle;
                }

                GUI.Label(new Rect(rect.x + 14f, rect.y + 32f, rect.width - 28f, 26f), status, smallStyle);
            }
        }

        // ------------------------------------------------------------------ styles

        private void EnsureStyles()
        {
            if (titleStyle != null)
            {
                return;
            }

            Font font = null;
            try
            {
                // MainFont is private in Draw; reuse it so the map matches the game's look.
                if (Draw.instance != null)
                {
                    FieldInfo field = typeof(Draw).GetField(
                        "MainFont", BindingFlags.Instance | BindingFlags.NonPublic);
                    if (field != null)
                    {
                        font = field.GetValue(Draw.instance) as Font;
                    }
                }
            }
            catch
            {
                font = null;
            }

            titleStyle = new GUIStyle(GUI.skin.label)
            {
                font = font,
                fontSize = 22,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
            };
            titleStyle.normal.textColor = Color.white;

            labelStyle = new GUIStyle(GUI.skin.label)
            {
                font = font,
                fontSize = 16,
                alignment = TextAnchor.MiddleLeft,
            };
            labelStyle.normal.textColor = new Color(0.9f, 0.93f, 0.97f, 1f);

            smallStyle = new GUIStyle(labelStyle) { fontSize = 14 };
            smallStyle.normal.textColor = MutedColor;

            rightSmallStyle = new GUIStyle(smallStyle) { alignment = TextAnchor.MiddleRight };
            rightSmallStyle.normal.textColor = PlayerColor;

            statusStyle = new GUIStyle(labelStyle) { fontSize = 16, fontStyle = FontStyle.Bold };

            buttonStyle = new GUIStyle(GUI.skin.button)
            {
                font = font,
                fontSize = 15,
                alignment = TextAnchor.MiddleCenter,
            };

            white = Texture2D.whiteTexture;
        }

        // --------------------------------------------------------------- primitives

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

        private void DrawBorder(Rect rect, Color color, float thickness)
        {
            Fill(new Rect(rect.x, rect.y, rect.width, thickness), color);
            Fill(new Rect(rect.x, rect.yMax - thickness, rect.width, thickness), color);
            Fill(new Rect(rect.x, rect.y, thickness, rect.height), color);
            Fill(new Rect(rect.xMax - thickness, rect.y, thickness, rect.height), color);
        }

        private void DrawDiamond(Rect rect, Color color)
        {
            float cx = rect.center.x;
            float cy = rect.center.y;
            float size = Mathf.Min(rect.width, rect.height) * 0.18f;
            Fill(new Rect(cx - size, cy - size, size * 2f, size * 2f), color);
        }

        private static Rect Centered(Rect rect, float size)
        {
            size = Mathf.Max(1f, size);
            return new Rect(rect.center.x - size * 0.5f, rect.center.y - size * 0.5f, size, size);
        }

        private static bool RectInside(Rect rect, Rect canvas)
        {
            return rect.x >= canvas.x && rect.y >= canvas.y
                && rect.xMax <= canvas.xMax && rect.yMax <= canvas.yMax;
        }

        private static bool MouseIn(Rect rowOnScreen, Rect view)
        {
            Event e = Event.current;
            return e != null && view.Contains(e.mousePosition) && rowOnScreen.Contains(e.mousePosition);
        }

        private bool IsEnterable(Block block, out TeleportCell cell)
        {
            if (enterableCache.TryGetValue(block, out cell))
            {
                return true;
            }

            if (notEnterable.Contains(block))
            {
                return false;
            }

            if (LevelGraph.TryResolveEnterable(block, out cell))
            {
                enterableCache[block] = cell;
                return true;
            }

            notEnterable.Add(block);
            return false;
        }

        private void ClampTreeScrollToRow(int index)
        {
            if (index < 0)
            {
                return;
            }

            float rowH = 26f;
            treeScroll.y = Mathf.Max(0f, index * rowH - rowH * 3f);
        }
    }
}
