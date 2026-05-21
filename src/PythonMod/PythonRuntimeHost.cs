using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using Python.Runtime;

namespace PythonMod
{
    public sealed class PythonRuntimeHost
    {
        private readonly PythonHostBridge _bridge;

        public PythonRuntimeHost(PythonHostBridge bridge)
        {
            _bridge = bridge;
        }

        public bool IsInitialized { get; private set; }
        public string LastError { get; private set; }

        public string RuntimeDir => Path.Combine(Main.ModPath, "Runtime");
        public string PythonDllPath => Path.Combine(RuntimeDir, "python38.dll");
        public string LibDir => Path.Combine(Main.ModPath, "Lib");
        public string GeneratedApiDir => Path.Combine(LibDir, "pythonmod");
        public string ModsDir => Path.Combine(Main.ModPath, "Mods");
        public string ConfigDir => Path.Combine(Main.ModPath, "Config");
        public string LogsDir => Path.Combine(Main.ModPath, "Logs");
        public string StubsDir => Path.Combine(Main.ModPath, "Stubs");

        [DllImport("kernel32", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool SetDllDirectory(string lpPathName);

        [DllImport("kernel32", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern IntPtr LoadLibrary(string lpFileName);

        public void EnsureLayout()
        {
            Directory.CreateDirectory(RuntimeDir);
            Directory.CreateDirectory(LibDir);
            Directory.CreateDirectory(GeneratedApiDir);
            Directory.CreateDirectory(ModsDir);
            Directory.CreateDirectory(ConfigDir);
            Directory.CreateDirectory(Path.Combine(ConfigDir, "settings"));
            Directory.CreateDirectory(LogsDir);
            Directory.CreateDirectory(StubsDir);
        }

        public bool TryInitialize()
        {
            if (IsInitialized)
            {
                return true;
            }

            try
            {
                return InitializePythonNet();
            }
            catch (Exception ex)
            {
                LastError = ex.ToString();
                Main.Mod.Logger.Error("CPython/Python.NET 初始化失败：");
                LogExceptionChain(ex);
                return false;
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private bool InitializePythonNet()
        {
            if (!File.Exists(PythonDllPath))
            {
                LastError = $"缺少内置 CPython：{PythonDllPath}";
                Main.Mod.Logger.Warning(LastError);
                return false;
            }

            Environment.SetEnvironmentVariable("PATH", RuntimeDir + Path.PathSeparator + Environment.GetEnvironmentVariable("PATH"));
            SetDllDirectory(RuntimeDir);
            var pythonDll = LoadLibrary(PythonDllPath);
            if (pythonDll == IntPtr.Zero)
            {
                var error = Marshal.GetLastWin32Error();
                throw new DllNotFoundException($"无法加载 {PythonDllPath}，Win32Error={error}");
            }

            PythonEngine.PythonHome = RuntimeDir;
            PythonEngine.PythonPath = string.Join(Path.PathSeparator.ToString(), new[]
            {
                RuntimeDir,
                Path.Combine(RuntimeDir, "python38.zip"),
                Path.Combine(RuntimeDir, "Lib"),
                Path.Combine(RuntimeDir, "Lib", "site-packages"),
                LibDir
            });
            PythonEngine.Initialize();
            using (Py.GIL())
            {
                dynamic sys = Py.Import("sys");
                sys.path.insert(0, LibDir);
                dynamic builtins = Py.Import("builtins");
                builtins._pythonmod_host = _bridge.ToPython();
            }

            IsInitialized = true;
            LastError = null;
            Main.Mod.Logger.Log("CPython 运行时初始化成功。");
            return true;
        }

        public void Shutdown()
        {
            IsInitialized = false;
        }

        public void WritePythonApiFiles()
        {
            Directory.CreateDirectory(GeneratedApiDir);
            Directory.CreateDirectory(Path.Combine(GeneratedApiDir, "clr"));

            File.WriteAllText(Path.Combine(GeneratedApiDir, "__init__.py"), PythonApiSource.Init);
            File.WriteAllText(Path.Combine(GeneratedApiDir, "log.py"), PythonApiSource.Log);
            File.WriteAllText(Path.Combine(GeneratedApiDir, "events.py"), PythonApiSource.Events);
            File.WriteAllText(Path.Combine(GeneratedApiDir, "settings.py"), PythonApiSource.Settings);
            File.WriteAllText(Path.Combine(GeneratedApiDir, "storage.py"), PythonApiSource.Storage);
            File.WriteAllText(Path.Combine(GeneratedApiDir, "ui.py"), PythonApiSource.Ui);
            File.WriteAllText(Path.Combine(GeneratedApiDir, "game.py"), PythonApiSource.Game);
            File.WriteAllText(Path.Combine(GeneratedApiDir, "harmony.py"), PythonApiSource.Harmony);
            File.WriteAllText(Path.Combine(GeneratedApiDir, "clr", "__init__.py"), PythonApiSource.ClrInit);
            File.WriteAllText(Path.Combine(GeneratedApiDir, "clr", "unity.py"), PythonApiSource.ClrUnity);
            File.WriteAllText(Path.Combine(GeneratedApiDir, "clr", "adofai.py"), PythonApiSource.ClrAdofai);
            File.WriteAllText(Path.Combine(GeneratedApiDir, "clr", "tmpro.py"), PythonApiSource.ClrTmpro);
        }

        public void RegenerateStubs()
        {
            ApiStubGenerator.Generate(Main.ManagedPath, StubsDir);
        }

        private static void LogExceptionChain(Exception ex)
        {
            var current = ex;
            while (current != null)
            {
                Main.Mod.Logger.Error(current.GetType().FullName + ": " + current.Message);
                if (current is TypeLoadException typeLoadException)
                {
                    Main.Mod.Logger.Error("TypeName: " + typeLoadException.TypeName);
                }
                if (current is FileNotFoundException fileNotFoundException)
                {
                    Main.Mod.Logger.Error("FusionLog: " + fileNotFoundException.FusionLog);
                }
                if (current is FileLoadException fileLoadException)
                {
                    Main.Mod.Logger.Error("FusionLog: " + fileLoadException.FusionLog);
                }
                if (current is BadImageFormatException badImageFormatException)
                {
                    Main.Mod.Logger.Error("FusionLog: " + badImageFormatException.FusionLog);
                }
                if (current is COMException comException)
                {
                    Main.Mod.Logger.Error("HResult: 0x" + comException.HResult.ToString("X8"));
                }
                Main.Mod.Logger.Error(current.StackTrace ?? "");
                current = current.InnerException;
            }
        }
    }
}
