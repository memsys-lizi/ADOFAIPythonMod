using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using UnityEngine;

namespace PythonMod
{
    public sealed class PythonModGui
    {
        private readonly PythonModRegistry _registry;
        private readonly PythonRuntimeHost _runtime;
        private int _selected;
        private string _installZipPath = "";
        private string _status = "";

        public PythonModGui(PythonModRegistry registry, PythonRuntimeHost runtime)
        {
            _registry = registry;
            _runtime = runtime;
        }

        public void OnGUI(UnityModManagerNet.UnityModManager.ModEntry modEntry)
        {
            GUILayout.Label("PythonMod");
            GUILayout.Label("Game: " + Main.GamePath);
            GUILayout.Label("Runtime: " + (_runtime.IsInitialized ? "Initialized" : (_runtime.LastError ?? "Not initialized")));

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Reload Mods", GUILayout.Width(120)))
            {
                _registry.Scan();
                _status = "已重新扫描 Python Mods。";
            }
            if (GUILayout.Button("Open Mods Folder", GUILayout.Width(150)))
            {
                OpenFolder(_runtime.ModsDir);
            }
            if (GUILayout.Button("Regenerate API Stubs", GUILayout.Width(180)))
            {
                Try(() =>
                {
                    _runtime.RegenerateStubs();
                    _status = "API stubs 已生成。";
                });
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Install zip:", GUILayout.Width(75));
            _installZipPath = GUILayout.TextField(_installZipPath);
            if (GUILayout.Button("Install", GUILayout.Width(80)))
            {
                Try(() =>
                {
                    ZipModInstaller.Install(_installZipPath, _runtime.ModsDir);
                    _registry.Scan();
                    _status = "zip 安装完成。";
                });
            }
            GUILayout.EndHorizontal();

            if (!string.IsNullOrEmpty(_status))
            {
                GUILayout.Label(_status);
            }

            GUILayout.BeginHorizontal();
            DrawModList();
            DrawDetails();
            GUILayout.EndHorizontal();
        }

        private void DrawModList()
        {
            GUILayout.BeginVertical(GUILayout.Width(260));
            GUILayout.Label("Installed Python Mods");
            for (var i = 0; i < _registry.Mods.Count; i++)
            {
                var mod = _registry.Mods[i];
                var label = $"{(mod.Enabled ? "[x]" : "[ ]")} {mod.Name} - {mod.State}";
                if (GUILayout.Toggle(_selected == i, label, "Button"))
                {
                    _selected = i;
                }
            }
            GUILayout.EndVertical();
        }

        private void DrawDetails()
        {
            GUILayout.BeginVertical();

            if (_registry.Mods.Count == 0)
            {
                GUILayout.Label("没有发现 Python Mod。");
                GUILayout.EndVertical();
                return;
            }

            if (_selected >= _registry.Mods.Count)
            {
                _selected = 0;
            }

            var mod = _registry.Mods[_selected];
            GUILayout.Label(mod.Name);
            GUILayout.Label("ID: " + mod.Id);
            GUILayout.Label("Version: " + mod.Manifest.Version);
            GUILayout.Label("Authors: " + string.Join(", ", mod.Manifest.Authors ?? Enumerable.Empty<string>()));
            GUILayout.Label("State: " + mod.State);
            GUILayout.Label("Path: " + mod.DirectoryPath);
            if (!string.IsNullOrEmpty(mod.Manifest.Description))
            {
                GUILayout.Label(mod.Manifest.Description);
            }

            if (!string.IsNullOrEmpty(mod.LastError))
            {
                GUILayout.Label("Error:");
                GUILayout.TextArea(mod.LastError, GUILayout.Height(100));
            }

            GUILayout.BeginHorizontal();
            if (GUILayout.Button(mod.Enabled ? "Disable" : "Enable", GUILayout.Width(90)))
            {
                if (mod.Enabled)
                {
                    _registry.Disable(mod);
                }
                else
                {
                    _registry.Enable(mod);
                }
            }
            if (GUILayout.Button("Reload", GUILayout.Width(90)))
            {
                _registry.Reload(mod);
            }
            if (GUILayout.Button("Open Folder", GUILayout.Width(110)))
            {
                OpenFolder(mod.DirectoryPath);
            }
            GUILayout.EndHorizontal();

            DrawSettings(mod);

            GUILayout.Label("Log");
            GUILayout.TextArea(_registry.ReadLog(mod), GUILayout.Height(160));
            GUILayout.EndVertical();
        }

        private void DrawSettings(PythonChildMod mod)
        {
            GUILayout.Space(8);
            GUILayout.Label("Settings");
            foreach (var setting in mod.Settings.Values.ToArray())
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label(setting.Label ?? setting.Key, GUILayout.Width(160));
                var changed = false;
                if (setting.Type == "bool")
                {
                    var oldValue = Convert.ToBoolean(setting.Value);
                    var newValue = GUILayout.Toggle(oldValue, "");
                    changed = newValue != oldValue;
                    setting.Value = newValue;
                }
                else if (setting.Type == "int")
                {
                    changed = DrawTextValue(setting, true);
                }
                else if (setting.Type == "float")
                {
                    changed = DrawTextValue(setting, false);
                }
                else if (setting.Type == "choice")
                {
                    var choices = setting.Choices ?? new System.Collections.Generic.List<string>();
                    var current = Math.Max(0, choices.IndexOf(Convert.ToString(setting.Value)));
                    var next = GUILayout.SelectionGrid(current, choices.ToArray(), Math.Max(1, choices.Count));
                    if (next >= 0 && next < choices.Count && next != current)
                    {
                        setting.Value = choices[next];
                        changed = true;
                    }
                }
                else if (setting.Type == "button")
                {
                    if (GUILayout.Button(setting.Label ?? setting.Key))
                    {
                        setting.Value = true;
                        changed = true;
                    }
                }
                else
                {
                    var oldValue = Convert.ToString(setting.Value ?? "");
                    var newValue = GUILayout.TextField(oldValue);
                    changed = newValue != oldValue;
                    setting.Value = newValue;
                }
                GUILayout.EndHorizontal();

                if (changed)
                {
                    _registry.SaveSettings(mod);
                }
            }
        }

        private static bool DrawTextValue(SettingDefinition setting, bool integer)
        {
            var oldValue = Convert.ToString(setting.Value ?? setting.DefaultValue ?? "");
            var newValue = GUILayout.TextField(oldValue);
            if (newValue == oldValue)
            {
                return false;
            }

            if (integer && int.TryParse(newValue, out var intValue))
            {
                setting.Value = intValue;
                return true;
            }

            if (!integer && double.TryParse(newValue, out var doubleValue))
            {
                setting.Value = doubleValue;
                return true;
            }

            return false;
        }

        private static void OpenFolder(string path)
        {
            Directory.CreateDirectory(path);
            Process.Start("explorer.exe", path);
        }

        private void Try(Action action)
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                _status = ex.Message;
                Main.Mod.Logger.LogException(ex);
            }
        }
    }
}
