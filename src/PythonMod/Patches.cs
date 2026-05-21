using System.Collections.Generic;
using HarmonyLib;
using UnityEngine.SceneManagement;

namespace PythonMod
{
    public static class Patches
    {
        [HarmonyPatch(typeof(SceneManager), nameof(SceneManager.LoadScene), new[] { typeof(string), typeof(LoadSceneMode) })]
        private static class SceneLoadPatch
        {
            private static void Postfix(string sceneName, LoadSceneMode mode)
            {
                Main.Registry?.Events.Trigger("scene_loaded", new Dictionary<string, object>
                {
                    ["name"] = sceneName,
                    ["mode"] = mode.ToString()
                });
            }
        }

        [HarmonyPatch(typeof(scrController), "Start")]
        private static class ControllerStartPatch
        {
            private static void Postfix()
            {
                Main.Registry?.Events.Trigger("level_started");
            }
        }

        [HarmonyPatch(typeof(scrController), "FailAction")]
        private static class ControllerFailPatch
        {
            private static void Prefix()
            {
                Main.Registry?.Events.Trigger("player_failed");
            }
        }

        [HarmonyPatch(typeof(scrController), "PortalTravelAction")]
        private static class ControllerPortalPatch
        {
            private static void Prefix()
            {
                Main.Registry?.Events.Trigger("level_completed");
            }
        }

        [HarmonyPatch(typeof(scrController), "CountValidKeysPressed")]
        private static class KeyPatch
        {
            private static void Postfix(int __result)
            {
                if (__result > 0)
                {
                    Main.Registry?.Events.Trigger("key_pressed", __result);
                }
            }
        }
    }
}
