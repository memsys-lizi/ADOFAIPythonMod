using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
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
        private static bool _started;

        public static string ModPath => Mod?.Path ?? Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        public static string GamePath => Path.GetFullPath(Path.Combine(ModPath, "..", ".."));
        public static string ManagedPath => Path.Combine(GamePath, "A Dance of Fire and Ice_Data", "Managed");

        public static bool Load(UnityModManager.ModEntry modEntry)
        {
            try
            {
                Mod = modEntry;
                modEntry.Logger.Log("PythonMod Main.Load 开始。");
                AppDomain.CurrentDomain.AssemblyResolve += ResolveDependency;

                Harmony = new Harmony(modEntry.Info.Id);
                Bridge = new PythonHostBridge();
                Runtime = new PythonRuntimeHost(Bridge);
                HarmonyBridge = new HarmonyBridge(Harmony, Bridge);
                Registry = new PythonModRegistry(Runtime, Bridge, HarmonyBridge);
                _gui = new PythonModGui(Registry, Runtime);
                PrepareWorkspace();

                modEntry.OnToggle = OnToggle;
                modEntry.OnGUI = _gui.OnGUI;
                modEntry.OnSaveGUI = OnSaveGUI;

                modEntry.Logger.Log("PythonMod 已加载。");
                return true;
            }
            catch (Exception ex)
            {
                modEntry.Logger.Error("PythonMod Main.Load 失败：");
                LogExceptionChain(modEntry, ex);
                return false;
            }
        }

        private static Assembly ResolveDependency(object sender, ResolveEventArgs args)
        {
            try
            {
                var assemblyName = new AssemblyName(args.Name).Name;
                var requested = assemblyName + ".dll";
                TraceDependency(assemblyName, "request");

                var localPath = Path.Combine(ModPath, requested);
                if (File.Exists(localPath))
                {
                    TraceDependency(assemblyName, localPath);
                    return Assembly.LoadFrom(localPath);
                }

                var runtimePath = Path.Combine(ModPath, "Runtime", requested);
                if (File.Exists(runtimePath))
                {
                    TraceDependency(assemblyName, runtimePath);
                    return Assembly.LoadFrom(runtimePath);
                }

                var facadesPath = Path.Combine(ModPath, "Facades", requested);
                if (File.Exists(facadesPath))
                {
                    TraceDependency(assemblyName, facadesPath);
                    return Assembly.LoadFrom(facadesPath);
                }

                var managedPath = Path.Combine(ManagedPath, requested);
                if (File.Exists(managedPath))
                {
                    TraceDependency(assemblyName, managedPath);
                    return Assembly.LoadFrom(managedPath);
                }

                var ummPath = Path.Combine(ManagedPath, "UnityModManager", requested);
                if (File.Exists(ummPath))
                {
                    TraceDependency(assemblyName, ummPath);
                    return Assembly.LoadFrom(ummPath);
                }

                TraceDependency(assemblyName, "not found");
            }
            catch (Exception ex)
            {
                Mod?.Logger?.Warning($"依赖解析失败：{args.Name} - {ex.GetType().Name}: {ex.Message}");
                return null;
            }

            return null;
        }

        private static void TraceDependency(string assemblyName, string result)
        {
            if (!ShouldTraceDependency(assemblyName))
            {
                return;
            }

            Mod?.Logger?.Log($"依赖解析：{assemblyName} -> {result}");
        }

        private static bool ShouldTraceDependency(string assemblyName)
        {
            return assemblyName == "Python.Runtime"
                || assemblyName == "netstandard"
                || assemblyName == "Microsoft.CSharp"
                || assemblyName == "System.Security.Permissions"
                || assemblyName.StartsWith("System.Reflection.Emit", StringComparison.Ordinal);
        }

        private static void LogExceptionChain(UnityModManager.ModEntry modEntry, Exception ex)
        {
            var current = ex;
            while (current != null)
            {
                modEntry.Logger.Error(current.GetType().FullName + ": " + current.Message);
                if (current is TypeLoadException typeLoadException)
                {
                    modEntry.Logger.Error("TypeName: " + typeLoadException.TypeName);
                }
                if (current is FileNotFoundException fileNotFoundException)
                {
                    modEntry.Logger.Error("FusionLog: " + fileNotFoundException.FusionLog);
                }
                if (current is FileLoadException fileLoadException)
                {
                    modEntry.Logger.Error("FusionLog: " + fileLoadException.FusionLog);
                }
                if (current is BadImageFormatException badImageFormatException)
                {
                    modEntry.Logger.Error("FusionLog: " + badImageFormatException.FusionLog);
                }
                if (current is COMException comException)
                {
                    modEntry.Logger.Error("HResult: 0x" + comException.HResult.ToString("X8"));
                }
                modEntry.Logger.Error(current.StackTrace ?? "");
                current = current.InnerException;
            }
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
            if (_started)
            {
                return;
            }

            try
            {
                MainThreadDispatcher.Ensure();
                ToastOverlay.Ensure();

                try
                {
                    Harmony.PatchAll(Assembly.GetExecutingAssembly());
                }
                catch (Exception ex)
                {
                    Mod.Logger.Warning("预置 Harmony 事件注册失败，PythonMod 将继续加载：");
                    LogExceptionChain(Mod, ex);
                }

                PrepareWorkspace();
                if (_runtimeStubsShouldGenerate())
                {
                    TryGenerateStubs();
                }

                if (!Runtime.TryInitialize())
                {
                    Registry.Scan();
                    Mod.Logger.Warning("Python 运行时初始化失败，Python 子 Mod 暂不会执行。");
                    _started = true;
                    return;
                }

                Registry.Scan();
                Registry.LoadEnabledMods();
                _started = true;
                Mod.Logger.Log("PythonMod 已启用。");
            }
            catch (Exception ex)
            {
                LogExceptionChain(Mod, ex);
            }
        }

        private static void Stop()
        {
            try
            {
                Registry.UnloadAll();
                Harmony.UnpatchAll(Mod.Info.Id);
                Runtime.Shutdown();
                _started = false;
                Mod.Logger.Log("PythonMod 已禁用。");
            }
            catch (Exception ex)
            {
                LogExceptionChain(Mod, ex);
            }
        }

        private static void OnSaveGUI(UnityModManager.ModEntry modEntry)
        {
            Registry?.SaveEnabledState();
            Registry?.SaveAllSettings();
        }

        private static void PrepareWorkspace()
        {
            Runtime.EnsureLayout();
            Runtime.WritePythonApiFiles();
            Registry.Scan();
        }

        private static bool _runtimeStubsShouldGenerate()
        {
            return !Directory.Exists(Runtime.StubsDir) || Directory.GetFiles(Runtime.StubsDir, "*.pyi", SearchOption.AllDirectories).Length == 0;
        }

        private static void TryGenerateStubs()
        {
            try
            {
                Runtime.RegenerateStubs();
            }
            catch (Exception ex)
            {
                Mod.Logger.Warning("API stubs 自动生成失败：");
                LogExceptionChain(Mod, ex);
            }
        }
    }
}
