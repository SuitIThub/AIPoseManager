from __future__ import annotations

import httpx
from urllib.parse import quote

from studio_pose_mcp.config import Settings
from studio_pose_mcp.errors import (
    PluginBadRequest,
    PluginConnectionError,
    PluginInternal,
    PluginNotFound,
    PluginUnauthorized,
)


class PluginClient:
    def __init__(self, settings: Settings):
        self._settings = settings
        self._client = httpx.AsyncClient(
            base_url=settings.plugin_base_url.rstrip("/"),
            headers={"X-Pose-Token": settings.plugin_token},
            timeout=httpx.Timeout(settings.request_timeout_s),
        )
        self._screenshot_timeout = httpx.Timeout(settings.screenshot_timeout_s)

    async def aclose(self) -> None:
        await self._client.aclose()

    async def _request(
        self,
        method: str,
        path: str,
        *,
        params: dict | None = None,
        json_body: dict | list | None = None,
        screenshot: bool = False,
    ) -> dict:
        timeout = self._screenshot_timeout if screenshot else self._client.timeout
        try:
            r = await self._client.request(
                method,
                path,
                params=params,
                json=json_body,
                timeout=timeout,
            )
        except httpx.ConnectError as e:
            raise PluginConnectionError(
                f"Cannot reach plugin at {self._settings.plugin_base_url}", str(e)
            ) from e
        except httpx.RequestError as e:
            raise PluginConnectionError("HTTP request failed", str(e)) from e

        try:
            body = r.json()
        except Exception as e:
            raise PluginInternal(f"Invalid JSON from plugin (HTTP {r.status_code})", str(e)) from e

        if r.status_code == 401:
            raise PluginUnauthorized("Plugin token rejected", None)
        if r.status_code == 404:
            raise PluginNotFound(body.get("error", "not found"), None)
        if r.status_code == 400:
            raise PluginBadRequest(body.get("error", "bad request"), None)
        if r.status_code >= 500 or not isinstance(body, dict):
            raise PluginInternal(body.get("error", "plugin error") if isinstance(body, dict) else str(body), None)

        if not body.get("ok"):
            code = body.get("code", "")
            err = body.get("error", "error")
            if r.status_code == 404 or code == "E_NO_CHARACTER":
                raise PluginNotFound(err, None)
            if r.status_code == 400 or code == "E_BAD_REQUEST":
                raise PluginBadRequest(err, None)
            raise PluginInternal(err, None)

        return body.get("data") or {}

    async def health(self) -> dict:
        # Health has no auth header in plugin — use plain client once
        async with httpx.AsyncClient(timeout=self._client.timeout) as c:
            r = await c.get(f"{self._settings.plugin_base_url.rstrip('/')}/v1/health")
            r.raise_for_status()
            body = r.json()
            if not body.get("ok"):
                raise PluginInternal(body.get("error", "health failed"), None)
            return body.get("data") or {}

    async def list_characters(self) -> list[dict]:
        data = await self._request("GET", "/v1/characters")
        return data.get("characters") or []

    async def select_character(self, char_id: int) -> None:
        await self._request("POST", f"/v1/characters/{char_id}/select")

    async def get_pose(
        self,
        char_id: int,
        regions: list[str],
        space: str = "local",
        include_ik: bool = False,
        precision: int = 1,
    ) -> dict:
        params: dict[str, str] = {
            "regions": ",".join(regions),
            "space": space,
            "include_ik": "true" if include_ik else "false",
            "precision": str(precision),
        }
        return await self._request("GET", f"/v1/characters/{char_id}/pose", params=params)

    async def write_bones(
        self, char_id: int, changes: list[dict], space: str = "local"
    ) -> dict:
        return await self._request(
            "POST",
            f"/v1/characters/{char_id}/bones",
            json_body={"space": space, "changes": changes},
        )

    async def set_fk_ik(self, char_id: int, fk: bool | None = None, ik: bool | None = None) -> None:
        body: dict = {}
        if fk is not None:
            body["fk"] = fk
        if ik is not None:
            body["ik"] = ik
        await self._request("POST", f"/v1/characters/{char_id}/fk_ik", json_body=body)

    async def screenshot(
        self,
        char_id: int,
        angle: str = "current",
        size: int = 512,
        framing: str = "full_body",
        fmt: str = "png",
    ) -> dict:
        params = {
            "angle": angle,
            "size": str(size),
            "format": fmt,
            "framing": framing,
        }
        return await self._request(
            "GET",
            f"/v1/characters/{char_id}/screenshot",
            params=params,
            screenshot=True,
        )

    async def save_checkpoint(
        self, char_id: int, name: str, regions: list[str] | None
    ) -> dict:
        body: dict = {"name": name}
        if regions is not None:
            body["regions"] = regions
        return await self._request(
            "POST", f"/v1/characters/{char_id}/checkpoints", json_body=body
        )

    async def restore_checkpoint_plugin(self, char_id: int, name: str) -> dict:
        enc = quote(name, safe="")
        return await self._request(
            "POST",
            f"/v1/characters/{char_id}/checkpoints/{enc}/restore",
            json_body={},
        )

    async def get_regions(self) -> dict:
        return await self._request("GET", "/v1/bones/regions")
