from __future__ import annotations

import hashlib
from dataclasses import dataclass, field
from datetime import datetime, timezone
from typing import Any


@dataclass
class PoseCache:
    last_pose_by_char: dict[int, dict] = field(default_factory=dict)
    screenshot_hash: dict[tuple[int, str, int, str], str] = field(default_factory=dict)
    screenshot_time: dict[tuple[int, str, int, str], str] = field(default_factory=dict)
    selected_id: int | None = None

    def invalidate_char(self, char_id: int) -> None:
        self.last_pose_by_char.pop(char_id, None)
        keys = [k for k in self.screenshot_hash if k[0] == char_id]
        for k in keys:
            self.screenshot_hash.pop(k, None)
            self.screenshot_time.pop(k, None)

    def set_pose(self, char_id: int, pose: dict) -> None:
        self.last_pose_by_char[char_id] = pose

    def set_screenshot(
        self, char_id: int, angle: str, size: int, framing: str, b64: str
    ) -> tuple[bool, str | None]:
        key = (char_id, angle, size, framing)
        h = hashlib.sha1(b64.encode("utf-8")).hexdigest()
        prev = self.screenshot_hash.get(key)
        now = datetime.now(timezone.utc).isoformat()
        if prev is not None and prev == h:
            return True, self.screenshot_time.get(key)
        self.screenshot_hash[key] = h
        self.screenshot_time[key] = now
        return False, None
