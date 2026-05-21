using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

namespace PythonMod
{
    public static class ApiStubGenerator
    {
        private static readonly string[] AssemblyNames =
        {
            "UnityEngine.dll",
            "UnityEngine.CoreModule.dll",
            "UnityEngine.UI.dll",
            "Unity.TextMeshPro.dll",
            "Assembly-CSharp.dll",
            "Assembly-CSharp-firstpass.dll"
        };

        public static void Generate(string managedPath, string stubsDir)
        {
            Directory.CreateDirectory(stubsDir);
            var pythonmodDir = Path.Combine(stubsDir, "pythonmod");
            var clrDir = Path.Combine(pythonmodDir, "clr");
            Directory.CreateDirectory(clrDir);

            File.WriteAllText(Path.Combine(pythonmodDir, "__init__.pyi"), "from . import log, events, settings, storage, ui, game, harmony\n");
            File.WriteAllText(Path.Combine(pythonmodDir, "log.pyi"), "def debug(message: object) -> None: ...\ndef info(message: object) -> None: ...\ndef warn(message: object) -> None: ...\ndef error(message: object) -> None: ...\n");
            File.WriteAllText(Path.Combine(pythonmodDir, "events.pyi"), "from typing import Callable, Any\n\ndef on(name: str) -> Callable[[Callable[..., Any]], Callable[..., Any]]: ...\ndef off(name: str, callback: Callable[..., Any] | None = ...) -> None: ...\ndef emit(name: str, *args: Any) -> None: ...\n");
            File.WriteAllText(Path.Combine(pythonmodDir, "settings.pyi"), "from typing import Any, Sequence\n\ndef bool(key: str, label: str, default: bool = ...) -> bool: ...\ndef int(key: str, label: str, default: int = ..., min: int | None = ..., max: int | None = ...) -> int: ...\ndef float(key: str, label: str, default: float = ..., min: float | None = ..., max: float | None = ...) -> float: ...\ndef string(key: str, label: str, default: str = ...) -> str: ...\ndef choice(key: str, label: str, default: str, choices: Sequence[str]) -> str: ...\ndef button(key: str, label: str) -> bool: ...\ndef get(key: str, default: Any = ...) -> Any: ...\ndef set(key: str, value: Any) -> None: ...\n");
            File.WriteAllText(Path.Combine(pythonmodDir, "harmony.pyi"), "from typing import Callable, Any\n\ndef skip(result: Any = ...) -> dict: ...\ndef patch(target: str, kind: str = ..., signature: str | None = ...) -> Callable[[Callable[..., Any]], Callable[..., Any]]: ...\ndef prefix(target: str, signature: str | None = ...) -> Callable[[Callable[..., Any]], Callable[..., Any]]: ...\ndef postfix(target: str, signature: str | None = ...) -> Callable[[Callable[..., Any]], Callable[..., Any]]: ...\ndef finalizer(target: str, signature: str | None = ...) -> Callable[[Callable[..., Any]], Callable[..., Any]]: ...\n");
            File.WriteAllText(Path.Combine(pythonmodDir, "storage.pyi"), "from typing import Any\n\ndef read_json(name: str, default: Any = ...) -> Any: ...\ndef write_json(name: str, value: Any) -> None: ...\n");
            File.WriteAllText(Path.Combine(pythonmodDir, "ui.pyi"), "def toast(message: str, duration: float = ...) -> None: ...\ndef message_box(title: str, message: str) -> None: ...\n");
            File.WriteAllText(Path.Combine(pythonmodDir, "game.pyi"), "def active_scene() -> str: ...\ndef managed_path() -> str: ...\n");

            WriteModuleStub(Path.Combine(clrDir, "unity.pyi"), LoadTypes(managedPath, "UnityEngine", "UnityEngine.UI"));
            WriteModuleStub(Path.Combine(clrDir, "tmpro.pyi"), LoadTypes(managedPath, "TMPro"));
            WriteModuleStub(Path.Combine(clrDir, "adofai.pyi"), LoadTypes(managedPath, null));
            File.WriteAllText(Path.Combine(clrDir, "__init__.pyi"), "def raw(assembly_name: str) -> None: ...\n");
        }

        private static IEnumerable<Type> LoadTypes(string managedPath, string namespacePrefix, string alternatePrefix = null)
        {
            var result = new List<Type>();
            foreach (var name in AssemblyNames)
            {
                var path = Path.Combine(managedPath, name);
                if (!File.Exists(path))
                {
                    continue;
                }

                try
                {
                    var assembly = Assembly.LoadFrom(path);
                    foreach (var type in SafeTypes(assembly))
                    {
                        if (!type.IsPublic)
                        {
                            continue;
                        }

                        if (namespacePrefix == null)
                        {
                            if (type.Namespace == null)
                            {
                                result.Add(type);
                            }
                        }
                        else if ((type.Namespace ?? "").StartsWith(namespacePrefix) || (alternatePrefix != null && (type.Namespace ?? "").StartsWith(alternatePrefix)))
                        {
                            result.Add(type);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Main.Mod?.Logger.Warning($"生成 stub 时加载 {name} 失败：{ex.Message}");
                }
            }

            return result.OrderBy(x => x.Name).Take(1200);
        }

        private static IEnumerable<Type> SafeTypes(Assembly assembly)
        {
            try
            {
                return assembly.GetExportedTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                return ex.Types.Where(x => x != null);
            }
        }

        private static void WriteModuleStub(string path, IEnumerable<Type> types)
        {
            var sb = new StringBuilder();
            sb.AppendLine("from typing import Any");
            foreach (var type in types)
            {
                if (string.IsNullOrEmpty(type.Name) || type.Name.Contains("`") || type.Name.StartsWith("<"))
                {
                    continue;
                }

                sb.AppendLine();
                sb.Append("class ").Append(type.Name).AppendLine(":");
                var members = type.GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)
                    .Where(m => m.MemberType == MemberTypes.Method || m.MemberType == MemberTypes.Property || m.MemberType == MemberTypes.Field)
                    .Select(m => m.Name)
                    .Where(n => !n.Contains("`") && !n.StartsWith("get_") && !n.StartsWith("set_"))
                    .Distinct()
                    .Take(40)
                    .ToArray();

                if (members.Length == 0)
                {
                    sb.AppendLine("    ...");
                }
                else
                {
                    foreach (var member in members)
                    {
                        sb.Append("    ").Append(member).Append(": Any").AppendLine();
                    }
                }
            }

            File.WriteAllText(path, sb.ToString());
        }
    }
}
