using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Python.Runtime;

namespace PythonMod
{
    public sealed class HarmonyBridge
    {
        private static HarmonyBridge _active;

        private readonly Harmony _harmony;
        private readonly PythonHostBridge _bridge;
        private readonly List<PythonPatch> _patches = new List<PythonPatch>();

        public HarmonyBridge(Harmony harmony, PythonHostBridge bridge)
        {
            _harmony = harmony;
            _bridge = bridge;
            _active = this;
        }

        public void RegisterPatch(string modId, string kind, string target, PyObject callback, object signature)
        {
            if (string.IsNullOrEmpty(modId))
            {
                throw new InvalidOperationException("无法在没有活动 Mod 的上下文中注册 Harmony patch。");
            }

            var method = ResolveTarget(target, signature);
            var patch = new PythonPatch
            {
                ModId = modId,
                Kind = kind?.ToLowerInvariant() ?? "postfix",
                Target = target,
                Method = method,
                Callback = callback
            };

            _patches.Add(patch);
            Apply(method, patch.Kind);
            Main.Mod.Logger.Log($"[{modId}] 注册 Harmony {patch.Kind}: {target}");
        }

        public void RemoveMod(string modId)
        {
            _patches.RemoveAll(x => x.ModId == modId);
        }

        private void Apply(MethodInfo original, string kind)
        {
            var bridgeType = typeof(HarmonyBridge);
            if (kind == "prefix")
            {
                _harmony.Patch(original, prefix: new HarmonyMethod(bridgeType.GetMethod(nameof(UniversalPrefix), BindingFlags.Public | BindingFlags.Static)));
            }
            else if (kind == "finalizer")
            {
                _harmony.Patch(original, finalizer: new HarmonyMethod(bridgeType.GetMethod(nameof(UniversalFinalizer), BindingFlags.Public | BindingFlags.Static)));
            }
            else
            {
                _harmony.Patch(original, postfix: new HarmonyMethod(bridgeType.GetMethod(nameof(UniversalPostfix), BindingFlags.Public | BindingFlags.Static)));
            }
        }

        public static bool UniversalPrefix(MethodBase __originalMethod, object __instance, object[] __args, ref object __result)
        {
            return _active?.DispatchPrefix(__originalMethod, __instance, __args, ref __result) ?? true;
        }

        public static void UniversalPostfix(MethodBase __originalMethod, object __instance, object[] __args, object __result)
        {
            _active?.Dispatch("postfix", __originalMethod, __instance, __args, __result, null);
        }

        public static Exception UniversalFinalizer(MethodBase __originalMethod, object __instance, object[] __args, Exception __exception)
        {
            _active?.Dispatch("finalizer", __originalMethod, __instance, __args, null, __exception);
            return __exception;
        }

        private bool DispatchPrefix(MethodBase original, object instance, object[] args, ref object result)
        {
            var callbacks = _patches.Where(x => x.Kind == "prefix" && SameMethod(x.Method, original)).ToArray();
            if (callbacks.Length == 0)
            {
                return true;
            }

            var shouldRunOriginal = true;
            using (Py.GIL())
            {
                foreach (var patch in callbacks)
                {
                    try
                    {
                        _bridge.ActiveModId = patch.ModId;
                        var returned = patch.Callback.Invoke(CreatePatchContext(original, instance, args, result, null));
                        if (returned != null && returned.ToString().Contains("__pythonmod_skip__"))
                        {
                            shouldRunOriginal = false;
                            var pyDict = returned as PyDict;
                            if (pyDict != null && pyDict.HasKey("result".ToPython()))
                            {
                                result = pyDict.GetItem("result".ToPython()).AsManagedObject(typeof(object));
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Main.Mod.Logger.LogException(ex);
                    }
                    finally
                    {
                        _bridge.ActiveModId = null;
                    }
                }
            }

            return shouldRunOriginal;
        }

        private void Dispatch(string kind, MethodBase original, object instance, object[] args, object result, Exception exception)
        {
            var callbacks = _patches.Where(x => x.Kind == kind && SameMethod(x.Method, original)).ToArray();
            if (callbacks.Length == 0)
            {
                return;
            }

            using (Py.GIL())
            {
                foreach (var patch in callbacks)
                {
                    try
                    {
                        _bridge.ActiveModId = patch.ModId;
                        patch.Callback.Invoke(CreatePatchContext(original, instance, args, result, exception));
                    }
                    catch (Exception ex)
                    {
                        Main.Mod.Logger.LogException(ex);
                    }
                    finally
                    {
                        _bridge.ActiveModId = null;
                    }
                }
            }
        }

        private static PyObject CreatePatchContext(MethodBase original, object instance, object[] args, object result, Exception exception)
        {
            var dict = new PyDict();
            dict.SetItem("method".ToPython(), original.Name.ToPython());
            dict.SetItem("declaring_type".ToPython(), original.DeclaringType?.FullName.ToPython() ?? "".ToPython());
            dict.SetItem("instance".ToPython(), instance?.ToPython() ?? "".ToPython());
            dict.SetItem("args".ToPython(), (args ?? Array.Empty<object>()).ToPython());
            dict.SetItem("result".ToPython(), result?.ToPython() ?? "".ToPython());
            dict.SetItem("exception".ToPython(), exception?.ToString().ToPython() ?? "".ToPython());
            return dict;
        }

        private static bool SameMethod(MethodInfo left, MethodBase right)
        {
            return left != null && right != null && left.MetadataToken == right.MetadataToken && left.Module == right.Module;
        }

        private static MethodInfo ResolveTarget(string target, object signature)
        {
            var lastDot = target.LastIndexOf('.');
            if (lastDot <= 0 || lastDot >= target.Length - 1)
            {
                throw new ArgumentException("Patch target 必须形如 TypeName.MethodName。");
            }

            var typeName = target.Substring(0, lastDot);
            var methodName = target.Substring(lastDot + 1);
            var type = FindType(typeName);
            if (type == null)
            {
                throw new MissingMemberException($"找不到类型：{typeName}");
            }

            var methods = type.GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                .Where(x => x.Name == methodName)
                .ToArray();
            if (methods.Length == 0)
            {
                throw new MissingMethodException(type.FullName, methodName);
            }

            if (methods.Length == 1 || signature == null)
            {
                return methods[0];
            }

            var signatureText = signature.ToString();
            var match = methods.FirstOrDefault(x => string.Join(",", x.GetParameters().Select(p => p.ParameterType.Name)) == signatureText);
            return match ?? methods[0];
        }

        private static Type FindType(string typeName)
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                var type = assembly.GetType(typeName) ?? assembly.GetTypes().FirstOrDefault(x => x.Name == typeName || x.FullName == typeName);
                if (type != null)
                {
                    return type;
                }
            }

            return null;
        }

        private sealed class PythonPatch
        {
            public string ModId { get; set; }
            public string Kind { get; set; }
            public string Target { get; set; }
            public MethodInfo Method { get; set; }
            public PyObject Callback { get; set; }
        }
    }
}
