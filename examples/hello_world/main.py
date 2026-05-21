from pythonmod import events, game, harmony, log, settings, ui

settings.bool("enabled", "启用", True)
settings.choice("message_mode", "消息模式", "toast", ["toast", "log"])

TARGET_SCENE = "1-X"
_reported_target_scene = False


def load(ctx):
    log.info(f"{ctx['name']} loaded")
    if settings.get("enabled", True) and settings.get("message_mode", "toast") == "toast":
        ui.toast("Hello from PythonMod")


def unload(ctx):
    log.info(f"{ctx['name']} unloaded")


def _scene_name(scene):
    if isinstance(scene, dict):
        return str(scene.get("name", ""))
    return str(scene)


def _is_target_scene(scene_name):
    return scene_name.strip().lower() == TARGET_SCENE.lower()


def _report_target_scene(scene_name, source):
    global _reported_target_scene
    if _reported_target_scene:
        return

    _reported_target_scene = True
    log.info(f"检测到进入 {TARGET_SCENE} 场景，来源：{source}，当前场景：{scene_name}")


@events.on("scene_loaded")
def on_scene_loaded(scene):
    scene_name = _scene_name(scene)
    log.info(f"scene loaded: {scene_name}")
    if _is_target_scene(scene_name):
        _report_target_scene(scene_name, "scene_loaded")


@events.on("level_started")
def on_level_started():
    scene_name = game.active_scene()
    log.info(f"level started in scene: {scene_name}")
    if _is_target_scene(scene_name):
        _report_target_scene(scene_name, "level_started")


@harmony.postfix("scrController.CountValidKeysPressed")
def after_count(ctx):
    result = ctx.get("result")
    if result:
        log.debug(f"valid keys pressed: {result}")
