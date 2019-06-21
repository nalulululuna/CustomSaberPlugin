﻿using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections;
using System.Collections.Generic;
using IPA;
using IPA.Loader;
using IPALogger = IPA.Logging.Logger;
using LogLevel = IPA.Logging.Logger.Level;
using Harmony;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CustomSaber
{
    public class Plugin : IBeatSaberPlugin
    {
        public static string PluginVersion { get; private set; } = "Unknown version";
        public static string PluginName => "Custom Sabers";

        private static List<string> _saberPaths;
        private static AssetBundle _currentSaber;
        public static string _currentSaberPath;
        public static Saber LeftSaber;
        public static Saber RightSaber;

        private bool _init;
        public bool FirstFetch = true;

        public void Init(IPALogger logger)
        {
            Logger.log = logger;
            Logger.Log("Logger prepared", LogLevel.Debug);
        }

        public void OnApplicationStart()
        {
            if (_init)
            {
                return;
            }
            _init = true;

            Logger.Log($"Custom Sabers v{Plugin.PluginVersion} has started", LogLevel.Info);

            List<string> sabers = RetrieveCustomSabers();
            if (sabers.Count == 0)
            {
                Logger.Log("No custom sabers found.");
                return;
            }

            _currentSaberPath = PlayerPrefs.GetString("lastSaber", null);
            if (_currentSaberPath == null || !sabers.Contains(_currentSaberPath))
            {
                _currentSaberPath = sabers[0];
            }
        }

        public void OnApplicationQuit()
        {
        }

        public void OnActiveSceneChanged(Scene from, Scene to)
        {
            //if (scene.buildIndex > 0)
            //{
            //    if (FirstFetch)
            //    {
            //        //Logger.Log("Launching coroutine to grab original sabers!", LogLevel.Debug);
            //        //SharedCoroutineStarter.instance.StartCoroutine(PreloadDefaultSabers());
            //        //Logger.Log("Launched!", LogLevel.Debug);
            //    }
            //}

            if (to.name == "GameCore")
            {
                LoadNewSaber(_currentSaberPath);
                SaberScript.LoadAssets();
            }

            if (to.name == "MenuCore")
            {
                if (_currentSaber != null)
                {
                    _currentSaber.Unload(true);
                }
                CustomSaberUI.OnLoad();
            }
        }

        private IEnumerator PreloadDefaultSabers()
        {
            FirstFetch = false;

            Logger.Log("Preloading default sabers!", LogLevel.Debug);
            HarmonyInstance harmony = HarmonyInstance.Create("CustomSaberHarmonyInstance");
            harmony.PatchAll(Assembly.GetExecutingAssembly());

            Logger.Log("Loading GameCore scene", LogLevel.Debug);
            SceneManager.LoadSceneAsync("GameCore", LoadSceneMode.Additive);
            Logger.Log("Loaded!", LogLevel.Debug);

            yield return new WaitUntil(() => Resources.FindObjectsOfTypeAll<Saber>().Count() > 1);
            Logger.Log("Got sabers!", LogLevel.Debug);

            foreach (Saber s in Resources.FindObjectsOfTypeAll<Saber>())
            {
                Logger.Log($"Saber: {s.name}, GameObj: {s.gameObject.name}, {s.ToString()}", LogLevel.Debug);
                if (s.name == "LeftSaber")
                {
                    LeftSaber = Saber.Instantiate(s);
                }
                else if (s.name == "RightSaber")
                {
                    RightSaber = Saber.Instantiate(s);
                }
            }
            Logger.Log("Finished! Got default sabers! Setting active state", LogLevel.Debug);

            if (LeftSaber)
            {
                Object.DontDestroyOnLoad(LeftSaber.gameObject);
                LeftSaber.gameObject.SetActive(false);
                LeftSaber.name = "___OriginalSaberPreviewB";
            }

            if (RightSaber)
            {
                Object.DontDestroyOnLoad(RightSaber.gameObject);
                RightSaber.gameObject.SetActive(false);
                RightSaber.name = "___OriginalSaberPreviewA";
            }

            Logger.Log("Unloading GameCore", LogLevel.Debug);
            SceneManager.UnloadSceneAsync("GameCore");

            Logger.Log("Unloading harmony patches", LogLevel.Debug);
            harmony.UnpatchAll("CustomSaberHarmonyInstance");
        }

        public static List<string> RetrieveCustomSabers()
        {
            _saberPaths = (Directory.GetFiles(Path.Combine(Application.dataPath, "../CustomSabers/"), "*.saber", SearchOption.AllDirectories).ToList());
            Logger.Log($"Found {_saberPaths.Count} sabers");

            _saberPaths.Insert(0, "DefaultSabers");
            return _saberPaths;
        }

        public void OnUpdate()
        {
            if (_currentSaber == null)
            {
                return;
            }

            if (Input.GetKeyDown(KeyCode.Space))
            {
                RetrieveCustomSabers();
                if (_saberPaths.Count == 1)
                {
                    return;
                }

                int oldIndex = _saberPaths.IndexOf(_currentSaberPath);
                if (oldIndex >= _saberPaths.Count - 1)
                {
                    oldIndex = -1;
                }

                string newSaber = _saberPaths[oldIndex + 1];
                LoadNewSaber(newSaber);

                if (SceneManager.GetActiveScene().buildIndex != 4)
                {
                    return;
                }

                SaberScript.LoadAssets();
            }
            else if (Input.GetKeyDown(KeyCode.Space) && Input.GetKey(KeyCode.LeftAlt))
            {
                RetrieveCustomSabers();
                if (_saberPaths.Count == 1)
                {
                    return;
                }

                int oldIndex = _saberPaths.IndexOf(_currentSaberPath);
                if (oldIndex <= 0)
                {
                    oldIndex = _saberPaths.Count - 1;
                }

                string newSaber = _saberPaths[oldIndex - 1];
                LoadNewSaber(newSaber);

                if (SceneManager.GetActiveScene().buildIndex != 4)
                {
                    return;
                }

                SaberScript.LoadAssets();
            }
        }

        public static void LoadNewSaber(string path)
        {
            if (_currentSaber != null)
            {
                _currentSaber.Unload(true);
            }

            if (path != "DefaultSabers")
            {
                _currentSaberPath = path;

                _currentSaber = AssetBundle.LoadFromFile(_currentSaberPath);
                if (_currentSaber == null)
                {
                    Logger.Log("Something went wrong while getting the asset bundle", LogLevel.Warning);
                }
                else
                {
                    Logger.Log(_currentSaber.GetAllAssetNames()[0], LogLevel.Debug);
                    Logger.Log("Successfully obtained the asset bundle!");
                    SaberScript.CustomSaber = _currentSaber;
                }
            }

            PlayerPrefs.SetString("lastSaber", _currentSaberPath);
            Logger.Log($"Loaded saber {path}");
        }

        public void OnFixedUpdate()
        {
        }

        public void OnSceneLoaded(Scene scene, LoadSceneMode sceneMode)
        {
        }

        public void OnSceneUnloaded(Scene scene)
        {
        }

        /// <param name="pluginNameOrId">The name or id defined in the manifest.json</param>
        public string GetPluginVersion(string pluginNameOrId)
        {
            foreach (PluginLoader.PluginInfo p in PluginManager.AllPlugins)
            {
                if (p.Metadata.Id == pluginNameOrId || p.Metadata.Name == pluginNameOrId)
                {
                    return p.Metadata.Version.ToString();
                }
            }

            return "Plugin Not Found";
        }
    }
}
