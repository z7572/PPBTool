using System;
using BepInEx.Configuration;
using UnityEngine;

namespace PPBTool
{
    internal sealed class ModConfig
    {
        private readonly ConfigEntry<KeyboardShortcut> openMapKey;
        private readonly ConfigEntry<bool> closeAfterTeleport;
        private readonly ConfigEntry<bool> undoable;
        private readonly ConfigEntry<int> cellSize;
        private readonly ConfigEntry<bool> hideVoidBoxes;
        private readonly ConfigEntry<bool> levelThumbnails;
        private readonly ConfigEntry<bool> logSceneTree;
        private readonly ConfigEntry<bool> logInfSymbols;
        private readonly ConfigEntry<string> language;

        public ModConfig(ConfigFile config)
        {
            openMapKey = config.Bind(
                "Keys", "OpenMap", new KeyboardShortcut(KeyCode.F7),
                "Keyboard shortcut for opening/closing the map browser. " +
                "BepInEx KeyboardShortcut syntax, e.g. F7, Ctrl+F7.");

            closeAfterTeleport = config.Bind(
                "Teleport", "CloseAfterTeleport", true,
                "Close the map browser right after a teleport is performed.");

            undoable = config.Bind(
                "Teleport", "Undoable", true,
                "Register a teleport as a normal UndoManager turn so Undo/Redo work on it. " +
                "When false, the undo history is cleared instead.");

            cellSize = config.Bind(
                "Teleport", "CellSize", 24,
                "Initial map cell size in pixels (clamped to 8..64).");

            hideVoidBoxes = config.Bind(
                "Teleport", "HideVoidBoxes", false,
                "Hide void levels/boxes that are not game-visible. Voids the game is " +
                "currently drawing (player/box in infinity space) stay visible.");

            ConfigEntry<bool> legacyThumbnails = config.Bind(
                "Teleport", "PortalThumbnails", true,
                "Deprecated; use LevelThumbnails. Kept so existing configs migrate.");

            levelThumbnails = config.Bind(
                "Teleport", "LevelThumbnails", legacyThumbnails.Value,
                "Draw real level thumbnails on portal cells. On by default; off is a " +
                "performance/compatibility fallback.");

            logSceneTree = config.Bind(
                "Diagnostics", "LogSceneTree", false,
                "Log every scanned level node when the map opens. Useful when reporting " +
                "compatibility problems with custom hubs.");

            logInfSymbols = config.Bind(
                "Diagnostics", "LogInfSymbols", false,
                "Log infinity symbol decisions (block, parent, count, depth, DrawStyle). " +
                "Useful when the game and map disagree about Infinite Enter/Exit symbols.");

            language = config.Bind(
                "UI", "Language", "auto",
                "UI language: auto (follow the game), zh, or en. The toolbar switch " +
                "persists the chosen language.");
        }

        // Read live values so ConfigurationManager edits apply without a restart.
        public bool CloseAfterTeleport
        {
            get { return closeAfterTeleport.Value; }
        }

        public bool Undoable
        {
            get { return undoable.Value; }
        }

        public int CellSize
        {
            get { return Math.Max(8, Math.Min(64, cellSize.Value)); }
        }

        public bool HideVoidBoxes
        {
            get { return hideVoidBoxes.Value; }
            set { hideVoidBoxes.Value = value; }
        }

        public bool LevelThumbnails
        {
            get { return levelThumbnails.Value; }
            set { levelThumbnails.Value = value; }
        }

        public bool LogSceneTree
        {
            get { return logSceneTree.Value; }
        }

        public bool LogInfSymbols
        {
            get { return logInfSymbols.Value; }
        }

        public string Language
        {
            get { return language.Value; }
            set { language.Value = value; }
        }

        public bool OpenMapPressed()
        {
            return openMapKey.Value.IsDown();
        }
    }
}
