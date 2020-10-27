using CustomSaber.Settings;
using CustomSaber.Settings.UI;
using CustomSaber.Utilities;
using IPA;
using IPA.Config;
using IPA.Loader;
using IPA.Utilities;
using System.IO;
using HarmonyLib;
using IPALogger = IPA.Logging.Logger;
using System;
using System.Reflection;

namespace CustomSaber
{
    [Plugin(RuntimeOptions.SingleStartInit)]
    public class Plugin
    {
        public static string PluginName => "Custom Sabers";
        public static SemVer.Version PluginVersion { get; private set; } = new SemVer.Version("0.0.0"); // Default.
        public static string PluginAssetPath => Path.Combine(UnityGame.InstallPath, "CustomSabers");

        public const string HarmonyId = "com.github.nalulululuna.CustomSaberPlugin";
        internal static Harmony harmony => new Harmony(HarmonyId);

        [Init]
        public void Init(IPALogger logger, Config config, PluginMetadata metadata)
        {
            Logger.log = logger;
            Configuration.Init(config);

            if (metadata != null)
            {
                PluginVersion = metadata.Version;
            }
        }

        [OnStart]
        public void OnApplicationStart() => Load();
        [OnExit]
        public void OnApplicationQuit() => Unload();

        private void OnGameSceneLoaded()
        {
            if (BS_Utils.Plugin.LevelData.Mode != BS_Utils.Gameplay.Mode.Multiplayer)
            {
                SaberScript.Load();
            }
        }

        private void Load()
        {
            ApplyHarmonyPatches();
            Configuration.Load();
            SaberAssetLoader.Load();
            SettingsUI.CreateMenu();
            AddEvents();
            Logger.log.Info($"{PluginName} v.{PluginVersion} has started.");
        }

        private void Unload()
        {
            Configuration.Save();
            SaberAssetLoader.Clear();
            RemoveEvents();
            RemoveHarmonyPatches();
        }

        private void AddEvents()
        {
            RemoveEvents();
            BS_Utils.Utilities.BSEvents.gameSceneLoaded += OnGameSceneLoaded;
        }

        private void RemoveEvents()
        {
            BS_Utils.Utilities.BSEvents.gameSceneLoaded -= OnGameSceneLoaded;
        }

        public static void ApplyHarmonyPatches()
        {
            try
            {
                Logger.log?.Debug("Applying Harmony patches.");
                harmony.PatchAll(Assembly.GetExecutingAssembly());
            }
            catch (Exception ex)
            {
                Logger.log?.Critical("Error applying Harmony patches: " + ex.Message);
                Logger.log?.Debug(ex);
            }
        }

        public static void RemoveHarmonyPatches()
        {
            try
            {
                harmony.UnpatchAll(HarmonyId);
            }
            catch (Exception ex)
            {
                Logger.log?.Critical("Error removing Harmony patches: " + ex.Message);
                Logger.log?.Debug(ex);
            }
        }
    }
}
