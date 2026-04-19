from __future__ import annotations

import base64
import json
import sys
import time
from typing import Any

import structlog
from mcp.server.fastmcp import FastMCP
from mcp.server.fastmcp import Image as McpImage

from studio_pose_mcp.cache import PoseCache
from studio_pose_mcp.checkpoints import CheckpointStore
from studio_pose_mcp.config import Settings
from studio_pose_mcp.errors import (
    PluginBadRequest,
    PluginConnectionError,
    PluginInternal,
    PluginNotFound,
    PluginUnauthorized,
)
from studio_pose_mcp.formatting import format_pose_compact, utc_now_iso
from studio_pose_mcp.plugin_client import PluginClient
from studio_pose_mcp.regions import ALL_REGION_KEYS, parse_regions_csv

log = structlog.get_logger(__name__)


def _configure_logging(level: str) -> None:
    import logging

    lvl = getattr(logging, level.upper(), logging.INFO)
    structlog.configure(
        processors=[
            structlog.processors.add_log_level,
            structlog.processors.TimeStamper(fmt="iso"),
            structlog.dev.ConsoleRenderer(colors=sys.stderr.isatty()),
        ],
        logger_factory=structlog.PrintLoggerFactory(file=sys.stderr),
        wrapper_class=structlog.make_filtering_bound_logger(lvl),
    )


def _tool_error(exc: Exception) -> str:
    if isinstance(exc, ValueError):
        return str(exc)
    if isinstance(exc, FileNotFoundError):
        return f"Checkpoint not found: {exc}"
    if isinstance(exc, PluginUnauthorized):
        return (
            "Plugin token rejected. Check POSE_PLUGIN_TOKEN matches the value in BepInEx config "
            "(com.suitji.studio_pose_bridge.cfg)."
        )
    if isinstance(exc, PluginConnectionError):
        return (
            f"Cannot reach plugin at the configured URL. Is StudioNEOV2 running with Studio Pose Bridge loaded? "
            f"({exc})"
        )
    if isinstance(exc, PluginNotFound):
        return f"Character or resource not found: {exc.detail or exc}"
    if isinstance(exc, PluginBadRequest):
        return f"Bad request: {exc.detail or exc}"
    if isinstance(exc, PluginInternal):
        return f"Plugin internal error. Check BepInEx log. ({exc.detail or exc})"
    return f"Error: {exc}"


def build_mcp(
    client: PluginClient,
    settings: Settings,
    cache: PoseCache,
    ck_store: CheckpointStore,
) -> FastMCP:
    mcp = FastMCP(
        "studio-pose-bridge",
        instructions=(
            "Bridge to StudioNeoV2 Studio Pose Bridge (HTTP). Use screenshots for visual feedback; "
            "minimize full pose dumps. Author: Suit-Ji."
        ),
    )

    def _resolve_id(character_id: int | None) -> int:
        if character_id is not None:
            return character_id
        if cache.selected_id is None:
            raise ValueError("No character selected. Call select_character or pass character_id.")
        return cache.selected_id

    @mcp.tool()
    async def list_characters() -> str:
        t0 = time.perf_counter()
        try:
            chars = await client.list_characters()
            out = {"characters": [{"id": c["id"], "name": c["name"], "selected": c.get("selected")} for c in chars]}
            log.info("tool", name="list_characters", ms=int((time.perf_counter() - t0) * 1000), n=len(chars))
            return json.dumps(out)
        except Exception as e:
            log.exception("list_characters")
            return json.dumps({"error": _tool_error(e)})

    @mcp.tool()
    async def select_character(character_id: int) -> str:
        t0 = time.perf_counter()
        try:
            await client.select_character(character_id)
            cache.selected_id = character_id
            log.info("tool", name="select_character", ms=int((time.perf_counter() - t0) * 1000), id=character_id)
            return json.dumps({"selected": character_id})
        except Exception as e:
            log.exception("select_character")
            return json.dumps({"error": _tool_error(e)})

    @mcp.tool()
    async def get_pose_summary(
        character_id: int | None = None,
        regions: str | None = None,
        include_ik: bool = False,
    ) -> str:
        t0 = time.perf_counter()
        try:
            cid = _resolve_id(character_id)
            reg_list = parse_regions_csv(regions) if regions else parse_regions_csv(settings.default_regions)
            pose = await client.get_pose(cid, reg_list, include_ik=include_ik)
            cache.set_pose(cid, pose)
            compact = format_pose_compact(pose, reg_list)
            out = {
                "pose_compact": compact,
                "cached_at": utc_now_iso(),
                "character_id": cid,
            }
            log.info(
                "tool",
                name="get_pose_summary",
                ms=int((time.perf_counter() - t0) * 1000),
                char=cid,
                regions=len(reg_list),
            )
            return json.dumps(out)
        except Exception as e:
            log.exception("get_pose_summary")
            return json.dumps({"error": _tool_error(e)})

    @mcp.tool()
    async def set_bones(
        character_id: int | None = None,
        changes: list[dict[str, Any]] | None = None,
        space: str = "local",
    ) -> str:
        t0 = time.perf_counter()
        try:
            cid = _resolve_id(character_id)
            ch_list = changes or []
            if not isinstance(ch_list, list):
                raise ValueError("changes must be a JSON array")
            result = await client.write_bones(cid, ch_list, space=space)
            cache.invalidate_char(cid)
            log.info(
                "tool",
                name="set_bones",
                ms=int((time.perf_counter() - t0) * 1000),
                char=cid,
                applied=len(result.get("applied") or []),
            )
            return json.dumps(result)
        except Exception as e:
            log.exception("set_bones")
            return json.dumps({"error": _tool_error(e)})

    @mcp.tool()
    async def get_screenshot(
        character_id: int | None = None,
        angle: str = "current",
        size: int | None = None,
        framing: str = "full_body",
    ) -> str | McpImage:
        t0 = time.perf_counter()
        try:
            cid = _resolve_id(character_id)
            sz = size if size is not None else settings.default_screenshot_size
            data = await client.screenshot(cid, angle=angle, size=sz, framing=framing, fmt="png")
            b64 = data.get("image_base64") or ""
            unchanged, prev_at = cache.set_screenshot(cid, angle, sz, framing, b64)
            if unchanged:
                log.info("tool", name="get_screenshot", ms=int((time.perf_counter() - t0) * 1000), unchanged=True)
                return json.dumps({"unchanged": True, "previous_at": prev_at})
            raw = base64.b64decode(b64)
            log.info(
                "tool",
                name="get_screenshot",
                ms=int((time.perf_counter() - t0) * 1000),
                kb=len(raw) // 1024,
            )
            return McpImage(data=raw, format="png")
        except Exception as e:
            log.exception("get_screenshot")
            return json.dumps({"error": _tool_error(e)})

    @mcp.tool()
    async def save_checkpoint(
        character_id: int | None = None,
        name: str = "",
        regions: str | None = None,
    ) -> str:
        t0 = time.perf_counter()
        try:
            cid = _resolve_id(character_id)
            if not name.strip():
                raise ValueError("name required")
            reg_list = parse_regions_csv(regions) if regions else None
            body_regions = reg_list if reg_list else None
            snap = await client.save_checkpoint(cid, name.strip(), body_regions)
            chars = await client.list_characters()
            char_name = next((c["name"] for c in chars if c["id"] == cid), str(cid))
            payload = snap.get("snapshot")
            if not isinstance(payload, dict):
                payload = snap
            ck_store.save(cid, char_name, name.strip(), payload)
            bones = (snap.get("snapshot") or {}).get("bones") or []
            log.info("tool", name="save_checkpoint", ms=int((time.perf_counter() - t0) * 1000), bones=len(bones))
            return json.dumps({"saved": name.strip(), "bone_count": len(bones)})
        except Exception as e:
            log.exception("save_checkpoint")
            return json.dumps({"error": _tool_error(e)})

    @mcp.tool()
    async def restore_checkpoint(character_id: int | None = None, name: str = "") -> str:
        t0 = time.perf_counter()
        try:
            cid = _resolve_id(character_id)
            if not name.strip():
                raise ValueError("name required")
            chars = await client.list_characters()
            char_name = next((c["name"] for c in chars if c["id"] == cid), str(cid))
            doc = ck_store.load(cid, char_name, name.strip())
            bones = doc.get("bones")
            if not isinstance(bones, list):
                raise PluginInternal("Checkpoint file missing bones[]", None)
            changes = []
            for b in bones:
                if not isinstance(b, dict):
                    continue
                bn = b.get("bone")
                re = b.get("rot_euler")
                if not bn or not isinstance(re, list) or len(re) < 3:
                    continue
                changes.append(
                    {
                        "bone": bn,
                        "rot_euler": [float(re[0]), float(re[1]), float(re[2])],
                        "mode": "absolute",
                    }
                )
            result = await client.write_bones(cid, changes, space="local")
            cache.invalidate_char(cid)
            n = len(result.get("applied") or [])
            log.info("tool", name="restore_checkpoint", ms=int((time.perf_counter() - t0) * 1000), bones=n)
            return json.dumps({"restored": name.strip(), "bones_changed": n})
        except Exception as e:
            log.exception("restore_checkpoint")
            return json.dumps({"error": _tool_error(e)})

    @mcp.tool()
    async def get_diff_since_checkpoint(
        character_id: int | None = None,
        checkpoint: str = "",
        tolerance_deg: float = 0.5,
    ) -> str:
        t0 = time.perf_counter()
        try:
            cid = _resolve_id(character_id)
            if not checkpoint.strip():
                raise ValueError("checkpoint name required")
            chars = await client.list_characters()
            char_name = next((c["name"] for c in chars if c["id"] == cid), str(cid))
            doc = ck_store.load(cid, char_name, checkpoint.strip())
            bones_cp = doc.get("bones")
            if not isinstance(bones_cp, list):
                raise PluginInternal("Invalid checkpoint", None)
            reg_set = {str(b.get("bone")) for b in bones_cp if isinstance(b, dict)}
            pose = await client.get_pose(cid, list({r for r in DEFAULT_REGIONS}), include_ik=False)
            regions = pose.get("regions") or {}
            current: dict[str, list[float]] = {}
            for _rn, bl in regions.items():
                if not isinstance(bl, list):
                    continue
                for b in bl:
                    if not isinstance(b, dict):
                        continue
                    bn = b.get("bone")
                    re = b.get("rot_euler")
                    if bn and isinstance(re, list) and len(re) >= 3:
                        current[str(bn)] = [float(re[0]), float(re[1]), float(re[2])]]

            changed = []
            unchanged_count = 0
            for b in bones_cp:
                if not isinstance(b, dict):
                    continue
                bn = str(b.get("bone", ""))
                re = b.get("rot_euler")
                if not bn or not isinstance(re, list) or len(re) < 3:
                    continue
                fr = [float(re[0]), float(re[1]), float(re[2])]
                to = current.get(bn)
                if to is None:
                    continue
                if all(abs(to[i] - fr[i]) <= tolerance_deg for i in range(3)):
                    unchanged_count += 1
                else:
                    changed.append({"bone": bn, "from": fr, "to": to})
            log.info(
                "tool",
                name="get_diff_since_checkpoint",
                ms=int((time.perf_counter() - t0) * 1000),
                changed=len(changed),
            )
            return json.dumps({"changed": changed, "unchanged_count": unchanged_count})
        except Exception as e:
            log.exception("get_diff_since_checkpoint")
            return json.dumps({"error": _tool_error(e)})

    return mcp


async def run(settings: Settings) -> None:
    _configure_logging(settings.log_level)
    client = PluginClient(settings)
    await client.health()
    cache = PoseCache()
    ck = CheckpointStore(settings)
    mcp = build_mcp(client, settings, cache, ck)
    try:
        await mcp.run_stdio_async()
    finally:
        await client.aclose()
