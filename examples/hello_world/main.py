from pythonmod import events, harmony, log, settings, ui

settings.bool("enabled", "启用", True)
settings.choice("message_mode", "消息模式", "toast", ["toast", "log"])


def load(ctx):
    log.info(f"{ctx['name']} loaded")
    if settings.get("enabled", True) and settings.get("message_mode", "toast") == "toast":
        ui.toast("Hello from PythonMod")


def unload(ctx):
    log.info(f"{ctx['name']} unloaded")


@events.on("scene_loaded")
def on_scene_loaded(scene):
    log.info(f"scene loaded: {scene}")


@harmony.postfix("scrController.CountValidKeysPressed")
def after_count(ctx):
    result = ctx.get("result")
    if result:
        log.debug(f"valid keys pressed: {result}")
