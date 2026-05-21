using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using Newtonsoft.Json;
using Python.Runtime;

namespace PythonMod
{
    public sealed class PythonModRegistry
    {
        private readonly PythonRuntimeHost _runtime;
        private readonly PythonHostBridge _bridge;
        private readonly HarmonyBridge _harmonyBridge;
        private Dictionary<string, bool> _enabledState = new Dictionary<string, bool>();

        public PythonModRegistry(PythonRuntimeHost runtime, PythonHostBridge bridge, HarmonyBridge harmonyBridge)
        {
            _runtime = runtime;
            _bridge = bridge;
            _harmonyBridge = harmonyBridge;
            Events = new PythonEventHub();
            _bridge.Attach(this, harmonyBridge);
        }

        public List<PythonChildMod> Mods { get; } = new List<PythonChildMod>();
        public PythonEventHub Events { get; }

        private string EnabledPath => Path.Combine(_runtime.ConfigDir, "enabled.json");

        public PythonChildMod Get(string id)
        {
            return Mods.FirstOrDefault(x => x.Id == id);
        }

        public void Scan()
        {
            _runtime.EnsureLayout();
            _enabledState = JsonFile.Read(EnabledPath, new Dictionary<string, bool>());
            Mods.Clear();

            foreach (var dir in Directory.GetDirectories(_runtime.ModsDir))
            {
                var manifestPath = Path.Combine(dir, "pythonmod.json");
                if (!File.Exists(manifestPath))
                {
                    continue;
                }

                try
                {
                    var manifest = JsonConvert.DeserializeObject<PythonModManifest>(File.ReadAllText(manifestPath));
                    ValidateManifest(manifest, dir);
                    var mod = new PythonChildMod
                    {
                        Manifest = manifest,
                        DirectoryPath = dir,
                        EntryPath = Path.Combine(dir, manifest.Entry),
                        Enabled = _enabledState.TryGetValue(manifest.Id, out var enabled) && enabled,
                        State = _enabledState.TryGetValue(manifest.Id, out enabled) && enabled
                            ? PythonChildModState.Discovered
                            : PythonChildModState.Disabled,
                        ModuleName = "pythonmod_user_" + SanitizeModuleName(manifest.Id)
                    };
                    LoadSettings(mod);
                    Mods.Add(mod);
                }
                catch (Exception ex)
                {
                    Mods.Add(new PythonChildMod
                    {
                        Manifest = new PythonModManifest { Id = Path.GetFileName(dir), Name = Path.GetFileName(dir), Entry = "" },
                        DirectoryPath = dir,
                        State = PythonChildModState.Error,
                        LastError = ex.Message
                    });
                }
            }
        }

        public void LoadEnabledMods()
        {
            foreach (var mod in Mods.Where(x => x.Enabled))
            {
                LoadMod(mod);
            }
        }

        public void Enable(PythonChildMod mod)
        {
            mod.Enabled = true;
            mod.State = PythonChildModState.Discovered;
            SaveEnabledState();
            LoadMod(mod);
        }

        public void Disable(PythonChildMod mod)
        {
            mod.Enabled = false;
            UnloadMod(mod);
            mod.State = PythonChildModState.Disabled;
            SaveEnabledState();
        }

        public void Reload(PythonChildMod mod)
        {
            UnloadMod(mod);
            LoadMod(mod);
        }

        public void LoadMod(PythonChildMod mod)
        {
            if (!_runtime.IsInitialized)
            {
                mod.State = PythonChildModState.Error;
                mod.LastError = _runtime.LastError ?? "Python 运行时尚未初始化。";
                return;
            }

            try
            {
                LoadModWithPython(mod);
            }
            catch (Exception ex)
            {
                mod.State = PythonChildModState.Error;
                mod.LastError = ex.ToString();
                AppendLog(mod, "ERROR: " + ex);
                Main.Mod.Logger.LogException(ex);
            }
            finally
            {
                _bridge.ActiveModId = null;
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private void LoadModWithPython(PythonChildMod mod)
        {
            try
            {
                mod.State = PythonChildModState.Loading;
                mod.LastError = null;
                LoadSettings(mod);

                using (Py.GIL())
                {
                    _bridge.ActiveModId = mod.Id;
                    dynamic sys = Py.Import("sys");
                    sys.path.insert(0, mod.DirectoryPath);
                    var packagesPath = Path.Combine(mod.DirectoryPath, "packages");
                    if (Directory.Exists(packagesPath))
                    {
                        sys.path.insert(0, packagesPath);
                    }

                    using (var scope = Py.CreateScope())
                    {
                        scope.Set("module_name", mod.ModuleName.ToPython());
                        scope.Set("entry_path", mod.EntryPath.ToPython());
                        scope.Exec(@"
import importlib.util
import sys
spec = importlib.util.spec_from_file_location(module_name, entry_path)
module = importlib.util.module_from_spec(spec)
sys.modules[module_name] = module
spec.loader.exec_module(module)
");
                        mod.Module = scope.Get("module");
                    }

                    var module = (PyObject)mod.Module;
                    if (module.HasAttr("load"))
                    {
                        module.GetAttr("load").Invoke(CreateContext(mod));
                    }
                }

                SaveSettings(mod);
                mod.State = PythonChildModState.Loaded;
                AppendLog(mod, "Mod loaded.");
            }
            finally
            {
                _bridge.ActiveModId = null;
            }
        }

        public void UnloadMod(PythonChildMod mod)
        {
            try
            {
                if (_runtime.IsInitialized && mod.Module != null)
                {
                    UnloadModWithPython(mod);
                }
            }
            catch (Exception ex)
            {
                Main.Mod.Logger.LogException(ex);
                AppendLog(mod, "ERROR during unload: " + ex);
            }
            finally
            {
                Events.RemoveMod(mod.Id);
                _harmonyBridge.RemoveMod(mod.Id);
                if (mod.Module is IDisposable disposable)
                {
                    disposable.Dispose();
                }
                mod.Module = null;
                if (mod.Enabled)
                {
                    mod.State = PythonChildModState.Discovered;
                }
                _bridge.ActiveModId = null;
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private void UnloadModWithPython(PythonChildMod mod)
        {
            using (Py.GIL())
            {
                _bridge.ActiveModId = mod.Id;
                var module = (PyObject)mod.Module;
                if (module.HasAttr("unload"))
                {
                    module.GetAttr("unload").Invoke(CreateContext(mod));
                }
            }
        }

        public void UnloadAll()
        {
            foreach (var mod in Mods.ToArray())
            {
                UnloadMod(mod);
            }
        }

        public void SaveEnabledState()
        {
            var state = Mods.ToDictionary(x => x.Id, x => x.Enabled);
            JsonFile.Write(EnabledPath, state);
        }

        public void SaveAllSettings()
        {
            foreach (var mod in Mods)
            {
                SaveSettings(mod);
            }
        }

        public void LoadSettings(PythonChildMod mod)
        {
            var path = SettingsPath(mod);
            var saved = JsonFile.Read(path, new Dictionary<string, SettingDefinition>());
            foreach (var item in saved)
            {
                mod.Settings[item.Key] = item.Value;
            }
        }

        public void SaveSettings(PythonChildMod mod)
        {
            JsonFile.Write(SettingsPath(mod), mod.Settings);
        }

        public void AppendLog(PythonChildMod mod, string line)
        {
            Directory.CreateDirectory(_runtime.LogsDir);
            File.AppendAllText(Path.Combine(_runtime.LogsDir, mod.Id + ".log"), line + Environment.NewLine);
        }

        public string ReadLog(PythonChildMod mod)
        {
            var path = Path.Combine(_runtime.LogsDir, mod.Id + ".log");
            return File.Exists(path) ? File.ReadAllText(path) : "";
        }

        private PyObject CreateContext(PythonChildMod mod)
        {
            var dict = new PyDict();
            dict.SetItem("id".ToPython(), mod.Id.ToPython());
            dict.SetItem("name".ToPython(), mod.Name.ToPython());
            dict.SetItem("path".ToPython(), mod.DirectoryPath.ToPython());
            dict.SetItem("version".ToPython(), (mod.Manifest.Version ?? "").ToPython());
            return dict;
        }

        private string SettingsPath(PythonChildMod mod)
        {
            return Path.Combine(_runtime.ConfigDir, "settings", mod.Id + ".json");
        }

        private static void ValidateManifest(PythonModManifest manifest, string dir)
        {
            if (manifest == null)
            {
                throw new InvalidDataException("manifest 为空。");
            }

            if (string.IsNullOrWhiteSpace(manifest.Id))
            {
                throw new InvalidDataException("manifest 缺少 id。");
            }

            if (string.IsNullOrWhiteSpace(manifest.Entry))
            {
                throw new InvalidDataException("manifest 缺少 entry。");
            }

            var entryPath = Path.GetFullPath(Path.Combine(dir, manifest.Entry));
            if (!entryPath.StartsWith(Path.GetFullPath(dir), StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("entry 路径越界。");
            }

            if (!File.Exists(entryPath))
            {
                throw new FileNotFoundException("entry 文件不存在。", entryPath);
            }
        }

        private static string SanitizeModuleName(string id)
        {
            return new string(id.Select(ch => char.IsLetterOrDigit(ch) ? ch : '_').ToArray());
        }
    }
}
