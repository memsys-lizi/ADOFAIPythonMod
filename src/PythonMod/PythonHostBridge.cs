using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Python.Runtime;
using UnityEngine.SceneManagement;

namespace PythonMod
{
    public sealed class PythonHostBridge
    {
        private PythonModRegistry _registry;
        private HarmonyBridge _harmonyBridge;

        public string ActiveModId { get; set; }

        public void Attach(PythonModRegistry registry, HarmonyBridge harmonyBridge)
        {
            _registry = registry;
            _harmonyBridge = harmonyBridge;
        }

        public void Log(string level, string message)
        {
            var mod = _registry?.Get(ActiveModId);
            var prefix = string.IsNullOrEmpty(ActiveModId) ? "[PythonMod]" : $"[{ActiveModId}]";
            var line = $"{prefix} {message}";

            if (string.Equals(level, "warn", StringComparison.OrdinalIgnoreCase))
            {
                Main.Mod.Logger.Warning(line);
            }
            else if (string.Equals(level, "error", StringComparison.OrdinalIgnoreCase))
            {
                Main.Mod.Logger.Error(line);
            }
            else
            {
                Main.Mod.Logger.Log(line);
            }

            if (mod != null)
            {
                _registry.AppendLog(mod, $"[{DateTime.Now:HH:mm:ss}] {level}: {message}");
            }
        }

        public void RegisterEvent(string name, PyObject callback)
        {
            _registry?.Events.Register(ActiveModId, name, callback);
        }

        public void UnregisterEvent(string name, PyObject callback)
        {
            _registry?.Events.Unregister(ActiveModId, name, callback);
        }

        public void Emit(string name, object args)
        {
            _registry?.Events.Trigger(name, args);
        }

        public object RegisterSetting(string key, string type, string label, object defaultValue, object min, object max, object choices)
        {
            var mod = _registry?.Get(ActiveModId);
            if (mod == null)
            {
                return defaultValue;
            }

            if (!mod.Settings.TryGetValue(key, out var setting))
            {
                setting = new SettingDefinition
                {
                    Key = key,
                    Type = type,
                    Label = label,
                    DefaultValue = ConvertPyObject(defaultValue),
                    Value = ConvertPyObject(defaultValue),
                    Min = ToNullableDouble(min),
                    Max = ToNullableDouble(max),
                    Choices = ToStringList(choices)
                };
                mod.Settings[key] = setting;
            }

            return setting.Value;
        }

        public object GetSetting(string key)
        {
            var mod = _registry?.Get(ActiveModId);
            return mod != null && mod.Settings.TryGetValue(key, out var setting) ? setting.Value : null;
        }

        public void SetSetting(string key, object value)
        {
            var mod = _registry?.Get(ActiveModId);
            if (mod == null || !mod.Settings.TryGetValue(key, out var setting))
            {
                return;
            }

            setting.Value = ConvertPyObject(value);
            _registry.SaveSettings(mod);
        }

        public object ReadStorageJson(string name, object defaultValue)
        {
            var mod = _registry?.Get(ActiveModId);
            if (mod == null)
            {
                return ConvertPyObject(defaultValue);
            }

            var path = GetStoragePath(mod, name);
            if (!File.Exists(path))
            {
                return ConvertPyObject(defaultValue);
            }

            return JsonConvert.DeserializeObject(File.ReadAllText(path));
        }

        public void WriteStorageJson(string name, object value)
        {
            var mod = _registry?.Get(ActiveModId);
            if (mod == null)
            {
                return;
            }

            var path = GetStoragePath(mod, name);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, JsonConvert.SerializeObject(ConvertPyObject(value), Formatting.Indented));
        }

        public void Toast(string message, double duration)
        {
            Log("info", $"Toast: {message}");
        }

        public void MessageBox(string title, string message)
        {
            Log("info", $"{title}: {message}");
        }

        public string GetActiveSceneName()
        {
            return SceneManager.GetActiveScene().name;
        }

        public string GetManagedPath()
        {
            return Main.ManagedPath;
        }

        public void RegisterPatch(string kind, string target, PyObject callback, object signature)
        {
            _harmonyBridge?.RegisterPatch(ActiveModId, kind, target, callback, signature);
        }

        private static string GetStoragePath(PythonChildMod mod, string name)
        {
            var safeName = string.Join("_", name.Split(Path.GetInvalidFileNameChars()));
            return Path.Combine(Main.Runtime.ConfigDir, "storage", mod.Id, safeName + ".json");
        }

        private static object ConvertPyObject(object value)
        {
            if (value is PyObject pyObject)
            {
                try
                {
                    dynamic json = Py.Import("json");
                    var dumped = json.dumps(pyObject);
                    return JsonConvert.DeserializeObject<string>(JsonConvert.SerializeObject(dumped.ToString()));
                }
                catch
                {
                    return pyObject.ToString();
                }
            }

            return value;
        }

        private static double? ToNullableDouble(object value)
        {
            if (value == null)
            {
                return null;
            }

            if (value is PyObject pyObject && (pyObject.IsNone() || pyObject.ToString() == "None"))
            {
                return null;
            }

            if (double.TryParse(Convert.ToString(ConvertPyObject(value)), out var parsed))
            {
                return parsed;
            }

            return null;
        }

        private static List<string> ToStringList(object value)
        {
            if (value == null)
            {
                return new List<string>();
            }

            if (value is PyObject pyObject)
            {
                try
                {
                    return pyObject.AsManagedObject(typeof(object[])) is object[] items
                        ? items.Select(x => Convert.ToString(x)).ToList()
                        : new List<string>();
                }
                catch
                {
                    return new List<string>();
                }
            }

            if (value is IEnumerable<object> enumerable)
            {
                return enumerable.Select(x => Convert.ToString(x)).ToList();
            }

            return new List<string>();
        }
    }
}
