from __future__ import annotations

from datetime import datetime, timezone
from typing import Any


def format_pose_compact(pose_data: dict, regions_filter: list[str] | None) -> str:
    """Flatten pose regions to 'bone: x,y,z' lines."""
    regions = pose_data.get("regions") or {}
    lines: list[str] = []
    for name, bones in regions.items():
        if regions_filter and name not in regions_filter:
            continue
        if not isinstance(bones, list):
            continue
        for b in bones:
            if not isinstance(b, dict):
                continue
            bone = b.get("bone", "?")
            reuler = b.get("rot_euler")
            if isinstance(reuler, list) and len(reuler) >= 3:
                lines.append(f"{bone}: {reuler[0]},{reuler[1]},{reuler[2]}")
    return "\n".join(lines)


def utc_now_iso() -> str:
    return datetime.now(timezone.utc).isoformat()


def filter_regions_dict(regions: dict[str, Any], keep: set[str] | None) -> dict[str, Any]:
    if keep is None:
        return regions
    return {k: v for k, v in regions.items() if k in keep}
