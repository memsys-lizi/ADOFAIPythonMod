using System;
using System.IO;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using UnityModManagerNet;

namespace PythonMod
{
    public static class Main
    {
        public static UnityModManager.ModEntry Mod { get; private set; }
        public static Harmony Harmony { get; private set; }
        public static PythonRuntimeHost Runtime { get; private set; }
        public static PythonModRegistry Registry { get; private set; }
        public static PythonHostBridge Bridge { get; private set; }
        public static HarmonyBridge HarmonyBridge { get; private set; }
        public static bool IsEnabled { get; private set; }

        private static PythonModGui _gui;

        public static string ModPath => Mod?.Path ?? Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        public static string GamePath => Path.GetFullPath(Path.Combine(ModPath, "..", ".."));
        public static string ManagedPath => Path.Combine(GamePath, "A Dance of Fire and Ice_Data", "Managed");

        public static bool Load(UnityModManager.ModEntry modEntry)
        {
            Mod = modEntry;
            Harmony = new Harmony(modEntry.Info.Id);

            Bridge = new PythonHostBridge();
            Runtime = new PythonRuntimeHost(Bridge);
            HarmonyBridge = new HarmonyBridge(Harmony, Bridge);
            Registry = new PythonModRegistry(Runtime, Bridge, HarmonyBridge);
            _gui = new PythonModGui(Registry, Runtime);

            modEntry.OnToggle = OnToggle;
            modEntry.OnGUI = _gui.OnGUI;
            modEntry.OnSaveGUI = OnSaveGUI;

            modEntry.Logger.Log("PythonMod 已加载。");
            return true;
        }

        private static bool OnToggle(UnityModManager.ModEntry modEntry, bool value)
        {
            IsEnabled = value;
            if (value)
            {
                Start();
            }
            else
            {
                Stop();
            }
            return true;
        }

        private static void Start()
        {
            try
            {
                MainThreadDispatcher.Ensure();
                Harmony.PatchAll(Assembly.GetExecutingAssembly());
                Runtime.EnsureLayout();
                Runtime.WritePythonApiFiles();
                Runtime.TryInitialize();
                Registry.Scan();
                Registry.LoadEnabledMods();
                Mod.Logger.Log("PythonMod 已启用。");
            }
            catch (Exception ex)
            {
                Mod.Logger.LogException(ex);
            }
        }

        private static void Stop()
        {
            try
            {
                Registry.UnloadAll();
                Harmony.UnpatchAll(Mod.Info.Id);
                Runtime.Shutdown();
                Mod.Logger.Log("PythonMod 已禁用。");
            }
            catch (Exception ex)
            {
                Mod.Logger.LogException(ex);
            }
        }

        private static void OnSaveGUI(UnityModManager.ModEntry modEntry)
        {
            Registry?.SaveEnabledState();
            Registry?.SaveAllSettings();
        }
    }
}
