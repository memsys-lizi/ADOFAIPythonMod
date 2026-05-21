# PythonMod

PythonMod 是 ADOFAI 的 UnityModManager 前置 Mod。它通过 Python.NET 托管 CPython，并在一个 UMM 面板里管理所有 Python 子 Mod。

默认游戏目录：

```text
D:\Steam\steamapps\common\A Dance of Fire and Ice
```

## 项目结构

```text
src/PythonMod/              C# UMM 前置 Mod
tools/pythonmod-cli/        Python 开发者 CLI
examples/hello_world/       示例 Python 子 Mod
scripts/                    本地辅助脚本
```

## 构建前置 Mod

第一次构建前安装内置 CPython 运行时。当前兼容后端使用 CPython 3.8 x64：

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\Install-CPythonRuntime.ps1
```

构建并部署到游戏目录：

```powershell
dotnet build .\PythonMod.sln -c Release
```

## 开发 Python Mod

安装 CLI：

```powershell
python -m pip install -e .\tools\pythonmod-cli
```

创建新 Mod：

```powershell
pythonmod new MyCoolMod --author 你的名字
cd MyCoolMod
```

开发安装到游戏：

```powershell
pythonmod dev --game "D:\Steam\steamapps\common\A Dance of Fire and Ice"
```

发布打包：

```powershell
pythonmod pack
```

推荐使用稳定 API：

```python
from pythonmod import events, log, settings, ui

settings.bool("enabled", "启用", True)

def load(ctx):
    log.info(f"{ctx['name']} loaded")
    if settings.get("enabled", True):
        ui.toast("Hello from PythonMod")

@events.on("scene_loaded")
def on_scene_loaded(scene):
    log.info(f"scene loaded: {scene}")
```

VS Code/Pylance 可通过 `Mods/PythonMod/Stubs` 获得 `pythonmod.*` 的补全。Unity/ADOFAI 原始类型暴露属于高级逃生口，普通 Mod 优先使用 `pythonmod.log/events/settings/storage/ui/game/harmony`。
