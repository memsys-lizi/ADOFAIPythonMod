from __future__ import annotations

import argparse
import json
import shutil
import sys
import zipfile
from pathlib import Path

DEFAULT_GAME_DIR = Path(r"D:\Steam\steamapps\common\A Dance of Fire and Ice")


def normalize_id(name: str) -> str:
    result = []
    for char in name.strip().lower():
        result.append(char if char.isalnum() else "_")
    return "_".join("".join(result).split("_")).strip("_") or "python_mod"


def cmd_new(args: argparse.Namespace) -> int:
    mod_id = normalize_id(args.name)
    target = Path(args.output or args.name).resolve()
    target.mkdir(parents=True, exist_ok=True)
    (target / "Resources").mkdir(exist_ok=True)
    (target / ".vscode").mkdir(exist_ok=True)

    manifest = {
        "id": mod_id,
        "name": args.name,
        "version": "0.1.0",
        "authors": [args.author],
        "description": "A PythonMod mod.",
        "entry": "main.py",
        "inject": "Loaded",
        "python": ">=3.11",
        "dependencies": [],
    }
    write_json(target / "pythonmod.json", manifest)
    write_text(
        target / "main.py",
        """from pythonmod import events, log, settings, ui

settings.bool("enabled", "启用", True)
settings.choice("mode", "模式", "simple", ["simple", "advanced"])


def load(ctx):
    log.info(f"{ctx['name']} loaded")
    if settings.get("enabled", True):
        ui.toast(f"Hello from {ctx['name']}")


def unload(ctx):
    log.info(f"{ctx['name']} unloaded")


@events.on("scene_loaded")
def on_scene_loaded(scene):
    log.info(f"scene loaded: {scene}")
""",
    )
    write_text(
        target / "pyproject.toml",
        f"""[project]
name = "{mod_id}"
version = "0.1.0"
requires-python = ">=3.11"
dependencies = []
""",
    )
    write_json(
        target / ".vscode" / "settings.json",
        {
            "python.analysis.extraPaths": [
                "${workspaceFolder}/.stubs",
                str(DEFAULT_GAME_DIR / "Mods" / "PythonMod" / "Stubs"),
            ]
        },
    )
    print(f"created {target}")
    return 0


def cmd_pack(args: argparse.Namespace) -> int:
    source = Path(args.source).resolve()
    manifest_path = source / "pythonmod.json"
    if not manifest_path.exists():
        raise SystemExit("pythonmod.json not found")

    manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
    output = Path(args.output or f"{manifest['id']}-{manifest.get('version', '0.1.0')}.zip").resolve()
    with zipfile.ZipFile(output, "w", zipfile.ZIP_DEFLATED) as archive:
        for path in source.rglob("*"):
            if path.is_dir():
                continue
            if any(part in {".git", "__pycache__", ".venv"} for part in path.parts):
                continue
            archive.write(path, path.relative_to(source).as_posix())
    print(f"packed {output}")
    return 0


def cmd_install(args: argparse.Namespace) -> int:
    game = Path(args.game).resolve() if args.game else DEFAULT_GAME_DIR
    zip_path = Path(args.zip).resolve()
    mods_dir = game / "Mods" / "PythonMod" / "Mods"
    mods_dir.mkdir(parents=True, exist_ok=True)

    with zipfile.ZipFile(zip_path) as archive:
        manifest_name = next((name for name in archive.namelist() if name.endswith("pythonmod.json")), None)
        if not manifest_name:
            raise SystemExit("zip missing pythonmod.json")
        manifest = json.loads(archive.read(manifest_name).decode("utf-8"))
        mod_id = manifest["id"]
        root = manifest_name[: -len("pythonmod.json")]
        target = mods_dir / mod_id
        target.mkdir(parents=True, exist_ok=True)
        for member in archive.infolist():
            if member.is_dir():
                continue
            name = member.filename[len(root) :] if member.filename.startswith(root) else member.filename
            dest = (target / name).resolve()
            if not str(dest).lower().startswith(str(target.resolve()).lower()):
                raise SystemExit("zip contains unsafe path")
            dest.parent.mkdir(parents=True, exist_ok=True)
            with archive.open(member) as src, dest.open("wb") as dst:
                shutil.copyfileobj(src, dst)
    print(f"installed {mod_id} to {target}")
    return 0


def cmd_dev(args: argparse.Namespace) -> int:
    game = Path(args.game).resolve() if args.game else DEFAULT_GAME_DIR
    source = Path(args.source).resolve()
    manifest = json.loads((source / "pythonmod.json").read_text(encoding="utf-8"))
    target = game / "Mods" / "PythonMod" / "Mods" / manifest["id"]
    if target.exists():
        shutil.rmtree(target)
    shutil.copytree(source, target, ignore=shutil.ignore_patterns(".git", "__pycache__", ".venv"))
    print(f"dev copy installed to {target}")
    return 0


def write_json(path: Path, value: object) -> None:
    path.write_text(json.dumps(value, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")


def write_text(path: Path, value: str) -> None:
    path.write_text(value, encoding="utf-8")


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(prog="pythonmod")
    sub = parser.add_subparsers(dest="command", required=True)

    p_new = sub.add_parser("new")
    p_new.add_argument("name")
    p_new.add_argument("--author", default="Anonymous")
    p_new.add_argument("-o", "--output")
    p_new.set_defaults(func=cmd_new)

    p_pack = sub.add_parser("pack")
    p_pack.add_argument("source", nargs="?", default=".")
    p_pack.add_argument("-o", "--output")
    p_pack.set_defaults(func=cmd_pack)

    p_install = sub.add_parser("install")
    p_install.add_argument("zip")
    p_install.add_argument("--game")
    p_install.set_defaults(func=cmd_install)

    p_dev = sub.add_parser("dev")
    p_dev.add_argument("source", nargs="?", default=".")
    p_dev.add_argument("--game")
    p_dev.set_defaults(func=cmd_dev)
    return parser


def main(argv: list[str] | None = None) -> int:
    parser = build_parser()
    args = parser.parse_args(argv)
    return args.func(args)


if __name__ == "__main__":
    raise SystemExit(main(sys.argv[1:]))
