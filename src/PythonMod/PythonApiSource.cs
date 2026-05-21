namespace PythonMod
{
    public static class PythonApiSource
    {
        public const string Init = @"from . import log, events, settings, storage, ui, game, harmony

__all__ = ['log', 'events', 'settings', 'storage', 'ui', 'game', 'harmony']
";

        public const string Log = @"import builtins

def debug(message): builtins._pythonmod_host.Log('debug', str(message))
def info(message): builtins._pythonmod_host.Log('info', str(message))
def warn(message): builtins._pythonmod_host.Log('warn', str(message))
def error(message): builtins._pythonmod_host.Log('error', str(message))
";

        public const string Events = @"import builtins

def on(name):
    def decorator(callback):
        builtins._pythonmod_host.RegisterEvent(str(name), callback)
        return callback
    return decorator

def off(name, callback=None):
    builtins._pythonmod_host.UnregisterEvent(str(name), callback)

def emit(name, *args):
    builtins._pythonmod_host.Emit(str(name), list(args))
";

        public const string Settings = @"import builtins

def bool(key, label, default=False):
    return builtins._pythonmod_host.RegisterSetting(str(key), 'bool', str(label), default, None, None, None)

def int(key, label, default=0, min=None, max=None):
    return builtins._pythonmod_host.RegisterSetting(str(key), 'int', str(label), default, min, max, None)

def float(key, label, default=0.0, min=None, max=None):
    return builtins._pythonmod_host.RegisterSetting(str(key), 'float', str(label), default, min, max, None)

def string(key, label, default=''):
    return builtins._pythonmod_host.RegisterSetting(str(key), 'string', str(label), default, None, None, None)

def choice(key, label, default, choices):
    return builtins._pythonmod_host.RegisterSetting(str(key), 'choice', str(label), default, None, None, list(choices))

def button(key, label):
    return builtins._pythonmod_host.RegisterSetting(str(key), 'button', str(label), False, None, None, None)

def get(key, default=None):
    value = builtins._pythonmod_host.GetSetting(str(key))
    return default if value is None else value

def set(key, value):
    builtins._pythonmod_host.SetSetting(str(key), value)
";

        public const string Storage = @"import builtins

def read_json(name, default=None):
    return builtins._pythonmod_host.ReadStorageJson(str(name), default)

def write_json(name, value):
    builtins._pythonmod_host.WriteStorageJson(str(name), value)
";

        public const string Ui = @"import builtins

def toast(message, duration=3.0):
    builtins._pythonmod_host.Toast(str(message), float(duration))

def message_box(title, message):
    builtins._pythonmod_host.MessageBox(str(title), str(message))
";

        public const string Game = @"import builtins

def active_scene():
    return builtins._pythonmod_host.GetActiveSceneName()

def managed_path():
    return builtins._pythonmod_host.GetManagedPath()
";

        public const string Harmony = @"import builtins

class SkipResult(dict):
    pass

def skip(result=None):
    value = SkipResult()
    value['__pythonmod_skip__'] = True
    value['result'] = result
    return value

def patch(target, kind='postfix', signature=None):
    def decorator(callback):
        builtins._pythonmod_host.RegisterPatch(str(kind), str(target), callback, signature)
        return callback
    return decorator

def prefix(target, signature=None):
    return patch(target, 'prefix', signature)

def postfix(target, signature=None):
    return patch(target, 'postfix', signature)

def finalizer(target, signature=None):
    return patch(target, 'finalizer', signature)
";

        public const string ClrInit = @"def raw(assembly_name):
    import clr
    clr.AddReference(assembly_name)
";

        public const string ClrUnity = @"import clr
clr.AddReference('UnityEngine')
clr.AddReference('UnityEngine.CoreModule')
from UnityEngine import *
";

        public const string ClrAdofai = @"import clr
clr.AddReference('Assembly-CSharp')
try:
    clr.AddReference('Assembly-CSharp-firstpass')
except Exception:
    pass
";

        public const string ClrTmpro = @"import clr
clr.AddReference('Unity.TextMeshPro')
from TMPro import *
";
    }
}
