using System;
using System.Collections.Generic;
using System.Linq;
using Python.Runtime;

namespace PythonMod
{
    public sealed class PythonEventHub
    {
        private readonly Dictionary<string, List<EventHandler>> _handlers = new Dictionary<string, List<EventHandler>>();

        public void Register(string modId, string name, PyObject callback)
        {
            if (string.IsNullOrEmpty(modId) || callback == null)
            {
                return;
            }

            if (!_handlers.TryGetValue(name, out var list))
            {
                list = new List<EventHandler>();
                _handlers[name] = list;
            }

            list.Add(new EventHandler { ModId = modId, Callback = callback });
        }

        public void Unregister(string modId, string name, PyObject callback)
        {
            if (!_handlers.TryGetValue(name, out var list))
            {
                return;
            }

            list.RemoveAll(x => x.ModId == modId && (callback == null || x.Callback == callback));
        }

        public void RemoveMod(string modId)
        {
            foreach (var key in _handlers.Keys.ToList())
            {
                _handlers[key].RemoveAll(x => x.ModId == modId);
            }
        }

        public void Trigger(string name, object payload = null)
        {
            if (!_handlers.TryGetValue(name, out var list) || list.Count == 0 || !Main.Runtime.IsInitialized)
            {
                return;
            }

            var snapshot = list.ToArray();
            MainThreadDispatcher.Enqueue(() =>
            {
                using (Py.GIL())
                {
                    foreach (var handler in snapshot)
                    {
                        try
                        {
                            Main.Bridge.ActiveModId = handler.ModId;
                            if (payload == null)
                            {
                                handler.Callback.Invoke();
                            }
                            else
                            {
                                handler.Callback.Invoke(payload.ToPython());
                            }
                        }
                        catch (Exception ex)
                        {
                            Main.Mod.Logger.LogException(ex);
                        }
                        finally
                        {
                            Main.Bridge.ActiveModId = null;
                        }
                    }
                }
            });
        }

        private sealed class EventHandler
        {
            public string ModId { get; set; }
            public PyObject Callback { get; set; }
        }
    }
}
