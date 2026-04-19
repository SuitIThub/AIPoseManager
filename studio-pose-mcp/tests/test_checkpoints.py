from pathlib import Path

from studio_pose_mcp.checkpoints import CheckpointStore
from studio_pose_mcp.config import Settings


def test_save_load_delete(tmp_path: Path):
    s = Settings(
        **{
            "plugin_base_url": "http://127.0.0.1:7842",
            "plugin_token": "x",
            "storage_dir": tmp_path,
            "default_screenshot_size": 512,
            "default_regions": "torso",
            "request_timeout_s": 10.0,
            "screenshot_timeout_s": 30.0,
            "log_level": "INFO",
        }
    )
    store = CheckpointStore(s)
    payload = {"bones": [{"bone": "a", "rot_euler": [0, 0, 0], "space": "local"}]}
    p = store.save(1, "Test / Char", "cp1", payload)
    assert p.is_file()
    loaded = store.load(1, "Test / Char", "cp1")
    assert loaded["bones"][0]["bone"] == "a"
    store.delete(1, "Test / Char", "cp1")
    assert not p.is_file()


def test_filename_sanitization(tmp_path: Path):
    s = Settings(
        **{
            "plugin_base_url": "http://127.0.0.1:7842",
            "plugin_token": "x",
            "storage_dir": tmp_path,
            "default_screenshot_size": 512,
            "default_regions": "torso",
            "request_timeout_s": 10.0,
            "screenshot_timeout_s": 30.0,
            "log_level": "INFO",
        }
    )
    store = CheckpointStore(s)
    store.save(2, "weird/name", "n", {"bones": []})
    d = tmp_path / "checkpoints" / "weird_name__2"
    assert d.is_dir()
    assert list(d.glob("*.json"))
