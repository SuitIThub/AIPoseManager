"""Manual integration smoke test. Requires StudioNEOV2 + Studio Pose Bridge with a character loaded."""

from __future__ import annotations

import asyncio
import os
import sys

import httpx


async def main() -> None:
    base = os.environ.get("POSE_PLUGIN_URL", "http://127.0.0.1:7842").rstrip("/")
    token = os.environ.get("POSE_PLUGIN_TOKEN", "")
    if not token:
        print("Set POSE_PLUGIN_TOKEN", file=sys.stderr)
        sys.exit(1)
    headers = {"X-Pose-Token": token}
    async with httpx.AsyncClient(timeout=30.0) as c:
        r = await c.get(f"{base}/v1/health")
        r.raise_for_status()
        print("health:", r.json())
        r = await c.get(f"{base}/v1/characters", headers=headers)
        r.raise_for_status()
        body = r.json()
        if not body.get("ok"):
            raise RuntimeError(body)
        chars = body.get("data", {}).get("characters") or []
        if not chars:
            print("No characters in scene; load a character first.", file=sys.stderr)
            sys.exit(2)
        cid = chars[0]["id"]
        await c.post(f"{base}/v1/characters/{cid}/select", headers=headers)
        r = await c.get(
            f"{base}/v1/characters/{cid}/pose",
            headers=headers,
            params={"regions": "torso,left_arm"},
        )
        r.raise_for_status()
        print("pose ok")
        r = await c.post(
            f"{base}/v1/characters/{cid}/bones",
            headers=headers,
            json={
                "space": "local",
                "changes": [
                    {
                        "bone": "cf_j_shoulder_L",
                        "rot_euler": [0, 0, 0],
                        "mode": "relative",
                    }
                ],
            },
        )
        r.raise_for_status()
        print("bones:", r.json())
        r = await c.get(
            f"{base}/v1/characters/{cid}/screenshot",
            headers=headers,
            params={"angle": "three_quarter", "size": "256"},
        )
        r.raise_for_status()
        sj = r.json()
        assert sj.get("ok")
        img = sj.get("data", {}).get("image_base64")
        assert img
        print("screenshot bytes (b64 len):", len(img))
        r = await c.post(
            f"{base}/v1/characters/{cid}/checkpoints",
            headers=headers,
            json={"name": "_smoke_baseline", "regions": ["torso"]},
        )
        r.raise_for_status()
        print("checkpoint saved")
    print("smoke OK")


if __name__ == "__main__":
    asyncio.run(main())
