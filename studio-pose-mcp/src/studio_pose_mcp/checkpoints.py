from __future__ import annotations

import json
import re
from datetime import datetime, timezone
from pathlib import Path
from studio_pose_mcp.config import Settings


def _safe_segment(s: str) -> str:
    s = s.strip().replace("\\", "_").replace("/", "_")
    s = re.sub(r"[^\w\-. ]+", "_", s)
    return s[:120] if len(s) > 120 else s


def _dir_for_char(storage: Path, char_id: int, char_name: str) -> Path:
    seg = _safe_segment(f"{char_name}__{char_id}")
    return storage / "checkpoints" / seg


class CheckpointStore:
    def __init__(self, settings: Settings):
        self._root = settings.storage_dir
        self._root.mkdir(parents=True, exist_ok=True)

    def save(self, char_id: int, char_name: str, name: str, payload: dict) -> Path:
        d = _dir_for_char(self._root, char_id, char_name)
        d.mkdir(parents=True, exist_ok=True)
        fn = _safe_segment(name) + ".json"
        path = d / fn
        doc = {
            **payload,
            "created_at": datetime.now(timezone.utc).isoformat(),
            "checkpoint_name": name,
            "character_id": char_id,
            "character_name": char_name,
        }
        path.write_text(json.dumps(doc, indent=2), encoding="utf-8")
        return path

    def load(self, char_id: int, char_name: str, name: str) -> dict:
        d = _dir_for_char(self._root, char_id, char_name)
        path = d / (_safe_segment(name) + ".json")
        if not path.is_file():
            raise FileNotFoundError(str(path))
        return json.loads(path.read_text(encoding="utf-8"))

    def delete(self, char_id: int, char_name: str, name: str) -> None:
        d = _dir_for_char(self._root, char_id, char_name)
        path = d / (_safe_segment(name) + ".json")
        if path.is_file():
            path.unlink()

    def list_all(self, char_id: int | None = None) -> list[dict]:
        base = self._root / "checkpoints"
        if not base.is_dir():
            return []
        out: list[dict] = []
        for d in base.iterdir():
            if not d.is_dir():
                continue
            for f in d.glob("*.json"):
                try:
                    data = json.loads(f.read_text(encoding="utf-8"))
                except Exception:
                    continue
                cid = data.get("character_id")
                if char_id is not None and cid != char_id:
                    continue
                out.append(
                    {
                        "path": str(f),
                        "character_id": cid,
                        "name": data.get("checkpoint_name") or f.stem,
                        "created_at": data.get("created_at"),
                    }
                )
        return out
