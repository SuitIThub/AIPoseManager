import os
from pathlib import Path

from pydantic import Field
from pydantic_settings import BaseSettings, SettingsConfigDict


class Settings(BaseSettings):
    """Loads POSE_* environment variables per project plan."""

    model_config = SettingsConfigDict(env_file=".env", env_file_encoding="utf-8", extra="ignore")

    plugin_base_url: str = Field(default="http://127.0.0.1:7842")
    plugin_token: str = Field(default="")
    storage_dir: Path = Field(default_factory=lambda: Path.home() / ".studio_pose_bridge")
    default_screenshot_size: int = Field(default=512)
    default_regions: str = Field(default="torso,left_arm,right_arm,left_leg,right_leg")
    request_timeout_s: float = Field(default=10.0)
    screenshot_timeout_s: float = Field(default=30.0)
    log_level: str = Field(default="INFO")


def _read_env() -> dict:
    return {
        "plugin_base_url": os.environ.get("POSE_PLUGIN_URL", "http://127.0.0.1:7842"),
        "plugin_token": os.environ.get("POSE_PLUGIN_TOKEN", ""),
        "storage_dir": Path(os.environ.get("POSE_STORAGE_DIR", str(Path.home() / ".studio_pose_bridge"))),
        "default_screenshot_size": int(os.environ.get("POSE_SCREENSHOT_SIZE", "512")),
        "default_regions": os.environ.get(
            "POSE_DEFAULT_REGIONS", "torso,left_arm,right_arm,left_leg,right_leg"
        ),
        "request_timeout_s": float(os.environ.get("POSE_TIMEOUT", "10.0")),
        "screenshot_timeout_s": float(os.environ.get("POSE_SCREENSHOT_TIMEOUT", "30.0")),
        "log_level": os.environ.get("POSE_LOG_LEVEL", "INFO"),
    }


def settings() -> Settings:
    s = Settings(**_read_env())
    if not s.plugin_token or not s.plugin_token.strip():
        raise RuntimeError(
            "POSE_PLUGIN_TOKEN is required. Copy the token from the BepInEx console when Studio Pose Bridge loads."
        )
    return s
