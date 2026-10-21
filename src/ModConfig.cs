using System;
using System.Collections.Generic;
using System.Globalization;
using BepInEx.Configuration;
using UnityEngine.InputSystem;

namespace PPBTool
{
    internal sealed class ModConfig
    {
        private static readonly HashSet<string> WarnedKeys = new HashSet<string>();

        private readonly ConfigEntry<string> winLevelKey;
        private readonly ConfigEntry<string> teleportKey;
        private readonly ConfigEntry<bool> closeAfterTeleport;
        private readonly ConfigEntry<bool> undoable;
        private readonly ConfigEntry<int> cellSize;
        private readonly ConfigEntry<bool> hideVoidBoxes;
        private readonly ConfigEntry<bool> useLevelPalette;
        private readonly ConfigEntry<bool> portalThumbnails;
        private readonly ConfigEntry<bool> logSceneTree;

        public ModConfig(ConfigFile config)
        {
            winLevelKey = config.Bind(
                "Keys", "WinLevel", "F7",
                "Hotkey for instantly completing the current level (normal win flow). " +
                "Any UnityEngine.InputSystem.Key name, e.g. F7, F8, K, Numpad5, or None.");

            teleportKey = config.Bind(
                "Keys", "Teleport", "F8",
                "Hotkey for opening/closing the teleport map browser. " +
                "Any UnityEngine.InputSystem.Key name, e.g. F7, F8, K, Numpad5, or None.");

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
                "Hide boxes/levels that live inside an infinity void.");

            useLevelPalette = config.Bind(
                "Teleport", "UseLevelPalette", false,
                "Draw the map with each level's own block/floor colours instead of the " +
                "PPBTool schematic palette. Drawing only; the game palette is untouched.");

            portalThumbnails = config.Bind(
                "Teleport", "PortalThumbnails", false,
                "Draw level thumbnails on portal cells. Off keeps the current purple " +
                "portal marker style.");

            logSceneTree = config.Bind(
                "Diagnostics", "LogSceneTree", false,
                "Log every scanned level node when the map opens. Useful when reporting " +
                "compatibility problems with custom hubs.");
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

        public bool UseLevelPalette
        {
            get { return useLevelPalette.Value; }
            set { useLevelPalette.Value = value; }
        }

        public bool PortalThumbnails
        {
            get { return portalThumbnails.Value; }
            set { portalThumbnails.Value = value; }
        }

        public bool LogSceneTree
        {
            get { return logSceneTree.Value; }
        }

        public bool WinPressed()
        {
            return WasPressed(winLevelKey.Value);
        }

        public bool TeleportPressed()
        {
            return WasPressed(teleportKey.Value);
        }

        private static bool WasPressed(string raw)
        {
            if (!TryParseKey(raw, out Key key))
            {
                if (WarnedKeys.Add(raw ?? string.Empty))
                {
                    Log.Warn("Unknown hotkey '" + raw + "'; expected a UnityEngine.InputSystem.Key name.");
                }

                return false;
            }

            if (key == Key.None)
            {
                return false;
            }

            Keyboard keyboard = Keyboard.current;
            return keyboard != null && keyboard[key].wasPressedThisFrame;
        }

        public static bool TryParseKey(string raw, out Key key)
        {
            key = Key.None;
            if (string.IsNullOrWhiteSpace(raw))
            {
                return false;
            }

            string text = raw.Trim();
            if (text.Equals("none", StringComparison.OrdinalIgnoreCase)
                || text.Equals("off", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            // Accept "F7", "f7", "Numpad5" and the enum's own names.
            foreach (string candidate in new[] { text, Normalize(text) })
            {
                if (Enum.TryParse(candidate, true, out key) && key != Key.None)
                {
                    return true;
                }
            }

            // Accept single digit characters as top-row digits.
            if (text.Length == 1 && char.IsDigit(text[0]))
            {
                return Enum.TryParse("Digit" + text, true, out key);
            }

            // A few friendly aliases.
            switch (text.ToLowerInvariant())
            {
                case "return":
                    key = Key.Enter;
                    return true;
                case "esc":
                    key = Key.Escape;
                    return true;
                case "space":
                    key = Key.Space;
                    return true;
                case "pgup":
                    key = Key.PageUp;
                    return true;
                case "pgdn":
                    key = Key.PageDown;
                    return true;
            }

            return false;
        }

        private static string Normalize(string text)
        {
            if (text.Length == 1)
            {
                return text.ToUpper(CultureInfo.InvariantCulture);
            }

            if ((text[0] == 'f' || text[0] == 'F') && int.TryParse(text.Substring(1), out int fn)
                && fn >= 1 && fn <= 24)
            {
                return "F" + fn.ToString(CultureInfo.InvariantCulture);
            }

            return text;
        }
    }
}
