using System;
using BepInEx;
using HarmonyLib;
using UnityEngine;

namespace PPBTool
{
    [BepInPlugin(Guid, Name, Version)]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string Guid = "ppbtool";
        public const string Name = "PPBTool";
        public const string Version = "0.1.0";

        internal static Plugin Instance { get; private set; }

        internal ModConfig Settings { get; private set; }

        internal TeleportOverlay Overlay { get; private set; }

        private Harmony harmony;

        private void Awake()
        {
            Instance = this;
            Log.Init(Logger);

            try
            {
                Settings = new ModConfig(Config);
                var teleporter = new TeleportService(Settings);
                Overlay = new TeleportOverlay(Settings, teleporter);

                harmony = new Harmony(Guid);
                harmony.PatchAll(typeof(WorldUpdateInnerPatch));

                var driver = gameObject.AddComponent<OverlayDriver>();
                driver.Bind(Overlay, Settings);
                DontDestroyOnLoad(gameObject);

                Log.Info("loaded v" + Version);
            }
            catch (Exception ex)
            {
                Log.Error("failed to initialise", ex);
            }
        }

        private void OnDestroy()
        {
            try
            {
                harmony?.UnpatchSelf();
            }
            catch (Exception ex)
            {
                Log.Error("failed to unpatch", ex);
            }
        }
    }

    /// <summary>
    /// Forwards per-frame callbacks to the overlay on the BepInEx manager object.
    ///
    /// Hotkeys are polled here, not inside the World.UpdateInner prefix: the prefix runs
    /// before the game calls Controls.Update(), so Unity's "this frame" key state may still
    /// describe the previous frame. A poller executed at the same priority as World.Update
    /// sees consistent state on the frame the key was actually pressed.
    /// </summary>
    public sealed class OverlayDriver : MonoBehaviour
    {
        private TeleportOverlay overlay;
        private ModConfig settings;

        internal void Bind(TeleportOverlay target, ModConfig modConfig)
        {
            overlay = target;
            settings = modConfig;
        }

        private void Update()
        {
            try
            {
                if (overlay == null || settings == null)
                {
                    return;
                }

                // F8 must always be able to close/open the browser, even while a
                // reference/infinity modal is open. Handle it before the game gate.
                if (settings.TeleportPressed())
                {
                    overlay.Toggle();
                    return;
                }

                overlay.FrameUpdate();

                if (overlay.BlocksGameUpdate)
                {
                    return;
                }

                if (settings.WinPressed())
                {
                    string message;
                    bool won = WinService.TryWin(out message);
                    overlay.Notify(message, !won);
                }
            }
            catch (Exception ex)
            {
                Log.Error("hotkey handling failed", ex);
            }
        }

        private void OnGUI()
        {
            try
            {
                overlay?.OnGUI();
            }
            catch (Exception ex)
            {
                Log.Error("overlay draw failed", ex);
            }
        }
    }

    /// <summary>
    /// Single patch point: while the map browser is open, skip the game's own update
    /// entirely (movement, undo, pause menu, transitions and win animation are all frozen).
    /// Hotkeys live in OverlayDriver.Update instead.
    /// </summary>
    [HarmonyPatch(typeof(World), "UpdateInner")]
    internal static class WorldUpdateInnerPatch
    {
        private static bool Prefix()
        {
            try
            {
                TeleportOverlay overlay = Plugin.Instance?.Overlay;
                if (overlay != null && overlay.BlocksGameUpdate)
                {
                    return false;
                }
            }
            catch (Exception ex)
            {
                Log.Error("overlay gate failed", ex);
            }

            return true;
        }
    }
}
