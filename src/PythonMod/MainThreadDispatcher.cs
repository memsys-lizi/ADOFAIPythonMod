using System;
using System.Collections.Generic;
using UnityEngine;

namespace PythonMod
{
    public sealed class MainThreadDispatcher : MonoBehaviour
    {
        private static readonly Queue<Action> Queue = new Queue<Action>();
        private static MainThreadDispatcher _instance;

        public static void Ensure()
        {
            if (_instance != null)
            {
                return;
            }

            var go = new GameObject("PythonModDispatcher");
            UnityEngine.Object.DontDestroyOnLoad(go);
            _instance = go.AddComponent<MainThreadDispatcher>();
        }

        public static void Enqueue(Action action)
        {
            lock (Queue)
            {
                Queue.Enqueue(action);
            }
        }

        private void Update()
        {
            while (true)
            {
                Action action = null;
                lock (Queue)
                {
                    if (Queue.Count > 0)
                    {
                        action = Queue.Dequeue();
                    }
                }

                if (action == null)
                {
                    return;
                }

                try
                {
                    action();
                }
                catch (Exception ex)
                {
                    Main.Mod?.Logger.LogException(ex);
                }
            }
        }
    }
}
