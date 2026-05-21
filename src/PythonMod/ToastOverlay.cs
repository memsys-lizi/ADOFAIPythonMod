using System;
using System.Collections.Generic;
using UnityEngine;

namespace PythonMod
{
    public sealed class ToastOverlay : MonoBehaviour
    {
        private static ToastOverlay _instance;
        private readonly List<ToastItem> _items = new List<ToastItem>();

        public static void Ensure()
        {
            if (_instance != null)
            {
                return;
            }

            var go = new GameObject("PythonModToastOverlay");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<ToastOverlay>();
        }

        public static void Show(string message, double duration)
        {
            Ensure();
            _instance._items.Add(new ToastItem
            {
                Message = message,
                EndTime = Time.realtimeSinceStartup + Mathf.Max(0.5f, (float)duration)
            });
        }

        private void OnGUI()
        {
            var now = Time.realtimeSinceStartup;
            _items.RemoveAll(x => x.EndTime <= now);
            if (_items.Count == 0)
            {
                return;
            }

            var oldColor = GUI.color;
            var y = 28f;
            foreach (var item in _items)
            {
                var width = Math.Min(Screen.width - 40f, Math.Max(280f, item.Message.Length * 9f + 40f));
                var rect = new Rect((Screen.width - width) / 2f, y, width, 32f);
                GUI.color = new Color(0f, 0f, 0f, 0.72f);
                GUI.Box(rect, GUIContent.none);
                GUI.color = Color.white;
                GUI.Label(new Rect(rect.x + 16f, rect.y + 7f, rect.width - 32f, 22f), item.Message);
                y += 38f;
            }

            GUI.color = oldColor;
        }

        private sealed class ToastItem
        {
            public string Message { get; set; }
            public float EndTime { get; set; }
        }
    }
}
