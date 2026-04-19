# StudioNeoV2 Pose Bridge — MCP Server Project Plan

A Python MCP server that wraps the StudioNeoV2 plugin's HTTP API and exposes it as a small, token-efficient set of tools an LLM can call to observe and manipulate character poses.

This document is the build spec. An agent following it should be able to produce a working, installable MCP server without further architectural decisions.

---

## 1. Goals and non-goals

**Goals**
- Translate MCP tool calls into HTTP requests against the plugin running in StudioNeoV2.
- Keep per-call token cost low: minimal default payloads, opt-in detail, screenshot-first verification, server-side caching of last-known state.
- Provide a clean iteration loop: observe → propose → apply → re-observe.
- Manage durable checkpoints (the plugin only keeps them in memory; this server persists them to disk).
- Be runnable as a `stdio` MCP server so it plugs straight into Claude Desktop / Cursor / any MCP-compatible client.

**Non-goals (v1)**
- No GUI.
- No remote access; the plugin is loopback-only and so is the server.
- No multi-Studio support; one server talks to one plugin instance.
- No automatic pose libraries / preset packs.

---

## 2. Stack

| Item | Choice |
|---|---|
| Language | Python 3.11+ |
| MCP SDK | `mcp` (official Python SDK) |
| HTTP client | `httpx` (sync — MCP tool handlers are async, but `httpx.AsyncClient` is fine) |
| Config | `pydantic-settings` reading from env + `.env` |
| Logging | `structlog` |
| Storage | Plain JSON files under `~/.studio_pose_bridge/` |
| Packaging | `uv` or `pip` + `pyproject.toml`; entry point `studio-pose-mcp` |

---

## 3. Repository layout

```
studio-pose-mcp/
├─ pyproject.toml
├─ README.md
├─ .env.example
└─ src/
   └─ studio_pose_mcp/
      ├─ __init__.py
      ├─ __main__.py             # python -m studio_pose_mcp
      ├─ server.py               # MCP server bootstrap, tool registration
      ├─ config.py               # Settings (port, token, base_url, storage dir)
      ├─ plugin_client.py        # Async httpx client wrapping the plugin API
      ├─ cache.py                # In-memory pose + screenshot caches
      ├─ checkpoints.py          # Disk-backed checkpoint store
      ├─ regions.py              # Region constants (mirror of plugin defaults)
      ├─ formatting.py           # Compact pose dict formatters
      ├─ errors.py               # Exception types -> MCP errors
      └─ tools/
         ├─ __init__.py          # Tool registry
         ├─ characters.py
         ├─ pose.py
         ├─ bones.py
         ├─ screenshot.py
         └─ checkpoints.py
```

---

## 4. Configuration

`config.py` (pydantic `BaseSettings`):

| Field | Env var | Default |
|---|---|---|
| `plugin_base_url` | `POSE_PLUGIN_URL` | `http://127.0.0.1:7842` |
| `plugin_token` | `POSE_PLUGIN_TOKEN` | required, no default |
| `storage_dir` | `POSE_STORAGE_DIR` | `~/.studio_pose_bridge` |
| `default_screenshot_size` | `POSE_SCREENSHOT_SIZE` | `512` |
| `default_regions` | `POSE_DEFAULT_REGIONS` | `torso,left_arm,right_arm,left_leg,right_leg` |
| `request_timeout_s` | `POSE_TIMEOUT` | `10.0` |
| `screenshot_timeout_s` | `POSE_SCREENSHOT_TIMEOUT` | `30.0` |
| `log_level` | `POSE_LOG_LEVEL` | `INFO` |

The MCP server should refuse to start without a token. On first run, print a hint that the token is shown in the BepInEx console when the plugin loads.

---

## 5. Plugin client

`plugin_client.py` is a thin async wrapper. One class, `PluginClient`, with one method per plugin endpoint. It handles:
- Adding the `X-Pose-Token` header.
- Translating non-2xx responses into typed exceptions (`PluginUnauthorized`, `PluginNotFound`, `PluginBadRequest`, `PluginInternal`).
- Unwrapping the `{ok, data}` envelope so callers see `data` directly.
- A single shared `httpx.AsyncClient` with connection pooling, separate timeouts for screenshot vs everything else.

Methods (1:1 with plugin endpoints):

```python
async def health() -> dict
async def list_characters() -> list[dict]
async def select_character(char_id: int) -> None
async def get_pose(char_id: int, regions: list[str], space: str = "local",
                   include_ik: bool = False, precision: int = 1) -> dict
async def write_bones(char_id: int, changes: list[dict], space: str = "local") -> dict
async def set_fk_ik(char_id: int, fk: bool | None = None, ik: bool | None = None) -> None
async def screenshot(char_id: int, angle: str = "current", size: int = 512,
                     framing: str = "full_body", fmt: str = "png") -> dict
async def save_checkpoint(char_id: int, name: str, regions: list[str] | None) -> dict
async def restore_checkpoint(char_id: int, name: str) -> dict
async def get_regions() -> dict
```

---

## 6. Caching layer

`cache.py` keeps two things keyed by character ID:

1. **Last full pose** — the dict returned by the most recent `get_pose` call. Used by the diff tool to compute "what changed since I last looked" without another round trip.
2. **Last screenshot hash** — SHA-1 of the most recent base64 image per `(char_id, angle, size, framing)`. If a new screenshot has the same hash, the tool can return `{"unchanged": true}` instead of re-sending the base64 bytes. Saves enormous tokens during iterative refinement.

Both caches are in-memory only; cleared on server restart. No TTL — invalidate explicitly when `write_bones` is called for that character.

---

## 7. Checkpoint store

`checkpoints.py` persists checkpoints to disk so they survive Studio restarts.

Layout:
```
~/.studio_pose_bridge/checkpoints/<character_name>__<char_id>/<name>.json
```

Each file contains the same payload returned by the plugin's save endpoint plus a `created_at` ISO timestamp and the region list it covers.

API:
```python
def save(char_id: int, char_name: str, name: str, payload: dict) -> Path
def load(char_id: int, char_name: str, name: str) -> dict
def list(char_id: int | None = None) -> list[dict]
def delete(char_id: int, char_name: str, name: str) -> None
```

When restoring, the server first reads from disk, then calls the plugin's bone-write endpoint with the stored bone changes. It does NOT call the plugin's restore endpoint, because the plugin's in-memory checkpoint may have been lost on restart.

---

## 8. Tool surface (this is the LLM-facing API)

Eight tools total. Designed so the common loop is **3 tools**: `get_pose_summary`, `set_bones`, `get_screenshot`. Everything else is auxiliary.

### 8.1 `list_characters`

**Description**: "List all characters currently loaded in StudioNeoV2. Returns each character's ID (stable for the session), name, and selection state. Call this first, or whenever the user mentions a character you don't recognize."

**Input**: none.

**Output**:
```json
{ "characters": [ { "id": 0, "name": "Aiko", "selected": true } ] }
```

### 8.2 `select_character`

**Description**: "Mark a character as the active target. Subsequent tools that take an optional `character_id` will use this one if not specified."

**Input**: `{ "character_id": int }`

**Output**: `{ "selected": int }`

### 8.3 `get_pose_summary`

**Description**: "Return the current pose of a character, grouped by body region. Defaults to torso + arms + legs. Use this to understand the starting state before editing. Pass specific `regions` to keep the response small. Eulers are in degrees, local space, rounded to 1 decimal."

**Input**:
```json
{
  "character_id": 0,
  "regions": ["torso", "left_arm"],
  "include_ik": false
}
```

`character_id` optional if a character is selected. `regions` optional, defaults to `default_regions` from config.

**Output**: pose dict from plugin, plus a `cached_at` timestamp. Updates the pose cache.

**Token note**: the formatter in `formatting.py` should output bones as a flat list of `"bone_name: x,y,z"` strings rather than nested objects when possible. Compare:

```
# verbose (don't do this by default):
{"bone": "cf_j_shoulder_L", "rot_euler": [2.1, 0.0, -1.4]}

# compact (do this):
"cf_j_shoulder_L: 2.1,0,-1.4"
```

The compact form roughly halves token count for a typical region.

### 8.4 `set_bones`

**Description**: "Apply rotation changes to one or more bones in a single batched call. Prefer one large batch over many small calls. `mode` can be `absolute` (replace) or `relative` (add to current). Always call `get_screenshot` afterwards to verify the result visually."

**Input**:
```json
{
  "character_id": 0,
  "changes": [
    { "bone": "cf_j_shoulder_L", "rot_euler": [0, 0, -20], "mode": "absolute" },
    { "bone": "cf_j_arm00_L",    "rot_euler": [0, 5, 0],    "mode": "relative" }
  ]
}
```

**Output**: plugin response (`applied`, `skipped`, `warnings`). Invalidates pose + screenshot caches for that character.

### 8.5 `get_screenshot`

**Description**: "Render a screenshot of the character from a chosen angle. Use this aggressively — visual feedback is much cheaper than reading every bone value back. If the image is identical to the previous screenshot for the same parameters, returns `{ unchanged: true }` instead of the bytes."

**Input**:
```json
{
  "character_id": 0,
  "angle": "three_quarter",
  "size": 512,
  "framing": "full_body"
}
```

`angle` ∈ `front, back, left, right, three_quarter, top, current`. `framing` ∈ `head, upper_body, full_body, tight`.

**Output**: either
```json
{ "image_base64": "...", "width": 512, "height": 512, "format": "png" }
```
or
```json
{ "unchanged": true, "previous_at": "2026-04-13T12:00:00Z" }
```

The tool should return the image as an MCP `ImageContent` block, not as a JSON string, so the LLM can actually see it.

### 8.6 `save_checkpoint`

**Description**: "Snapshot the current pose under a name so you can revert to it later. Cheap — call this before any risky edit."

**Input**: `{ "character_id": 0, "name": "before_arm_fix", "regions": ["left_arm"] }`

`regions` optional; defaults to all regions.

**Output**: `{ "saved": "before_arm_fix", "bone_count": 12 }`

Persists to disk via `checkpoints.py`.

### 8.7 `restore_checkpoint`

**Description**: "Revert the character to a previously saved checkpoint."

**Input**: `{ "character_id": 0, "name": "before_arm_fix" }`

**Output**: `{ "restored": "before_arm_fix", "bones_changed": 12 }`

Reads from disk, calls `set_bones` on the plugin, invalidates caches.

### 8.8 `get_diff_since_checkpoint`

**Description**: "Compare the current pose to a saved checkpoint and return only the bones that changed. Useful for verifying what your last batch of edits actually did, without re-reading the full pose."

**Input**: `{ "character_id": 0, "checkpoint": "before_arm_fix", "tolerance_deg": 0.5 }`

**Output**:
```json
{
  "changed": [
    { "bone": "cf_j_shoulder_L", "from": [0,0,0], "to": [0,0,-20] }
  ],
  "unchanged_count": 134
}
```

---

## 9. Server bootstrap

`server.py`:

```python
from mcp.server import Server
from mcp.server.stdio import stdio_server

from .config import settings
from .plugin_client import PluginClient
from .tools import register_all

async def main():
    settings_obj = settings()  # raises if token missing
    client = PluginClient(settings_obj)
    await client.health()  # fail fast if plugin not running

    server = Server("studio-pose-bridge")
    register_all(server, client, settings_obj)

    async with stdio_server() as (read, write):
        await server.run(read, write, server.create_initialization_options())
```

`__main__.py` just does `asyncio.run(main())`.

`tools/__init__.py` imports each tool module's `register(server, client, settings)` function and calls them in order.

Each tool module follows the same pattern:

```python
from mcp.server import Server
from mcp.types import Tool, TextContent

def register(server: Server, client, settings):
    @server.list_tools()
    async def _list():  # actually aggregate at the top level
        ...

    @server.call_tool()
    async def _call(name, arguments):
        ...
```

In practice, register a single top-level `list_tools` and `call_tool` in `server.py` that dispatches by tool name to the per-module handlers, rather than re-decorating in each file. The per-module files just export a `TOOLS = [...]` list and a `HANDLERS = {...}` dict.

---

## 10. Error handling

Map plugin exceptions to MCP errors:

| Plugin exception | MCP error |
|---|---|
| `PluginUnauthorized` | tool error: `"Plugin token rejected. Check POSE_PLUGIN_TOKEN matches the value in BepInEx config."` |
| `PluginConnectionError` (httpx connect refused) | tool error: `"Cannot reach plugin at {url}. Is StudioNeoV2 running with the Pose Bridge plugin loaded?"` |
| `PluginNotFound` | tool error: `"Character or resource not found: {detail}"` |
| `PluginBadRequest` | tool error: `"Bad request: {detail}"` |
| `PluginInternal` | tool error: `"Plugin internal error. Check BepInEx log."` |

All errors include enough info that the LLM can self-correct (e.g. "did you call list_characters first?") without crashing the conversation.

---

## 11. Logging

`structlog` to stderr. INFO by default. Each tool call logs:
- tool name
- argument summary (truncate base64, hide token)
- duration ms
- result summary (`bones_written=N`, `image_size=KB`, etc.)

DEBUG level adds full request/response bodies for the plugin client.

Never log to stdout — stdout is the MCP transport.

---

## 12. Testing

### 12.1 Unit tests

`tests/test_formatting.py`
- Compact pose formatter round-trips correctly.
- Precision rounding works.
- Region filtering drops unrequested regions.

`tests/test_cache.py`
- Pose cache invalidation on `set_bones`.
- Screenshot hash dedup returns `unchanged: true` on identical bytes.

`tests/test_checkpoints.py`
- Save → load → delete on a tempdir.
- Filename sanitization (character names with spaces, slashes).

### 12.2 Integration tests (manual, with plugin running)

A `scripts/smoke.py` that:
1. Calls `health`.
2. Lists characters; aborts if zero.
3. Selects character 0.
4. Saves a `_smoke_baseline` checkpoint.
5. Reads the pose summary.
6. Writes a small relative change to one shoulder bone.
7. Reads pose again, asserts the bone moved.
8. Takes a screenshot, asserts non-empty PNG.
9. Restores the baseline.
10. Asserts the diff is empty.

Run it with `python -m studio_pose_mcp.scripts.smoke` after starting Studio with the plugin.

### 12.3 MCP client test

A short Claude Desktop config snippet in the README:

```json
{
  "mcpServers": {
    "studio-pose": {
      "command": "uv",
      "args": ["run", "studio-pose-mcp"],
      "env": {
        "POSE_PLUGIN_TOKEN": "<paste token from BepInEx log>"
      }
    }
  }
}
```

---

## 13. Suggested iteration loop (document this in README for the user)

When the LLM is asked to pose a character:

1. `list_characters` (only if not already known).
2. `get_screenshot` at `three_quarter` + `get_pose_summary` for the relevant regions. This is the baseline.
3. `save_checkpoint` named `pre_<task>` — cheap insurance.
4. Plan the entire pose change in one thinking pass.
5. One `set_bones` call with the full batch.
6. One `get_screenshot` to verify visually.
7. If wrong: at most one corrective `set_bones`. If still wrong after that, `get_diff_since_checkpoint` to see what actually moved (often FK/IK mode is wrong) and reconsider rather than nudging blindly.
8. Stop when the screenshot matches the request, or after 5 iterations total.

The README should make explicit that **screenshots are the cheap channel and bone dumps are the expensive channel**. When in doubt, render, don't read.

---

## 14. Definition of done for v1

- [ ] `pip install -e .` (or `uv sync`) installs cleanly on Python 3.11+.
- [ ] `studio-pose-mcp` starts and immediately health-checks the plugin, failing loud if the plugin is unreachable or the token is wrong.
- [ ] All 8 tools registered and callable from a real MCP client (Claude Desktop or `mcp` CLI inspector).
- [ ] `get_pose_summary` returns compact-formatted output by default and respects `regions`.
- [ ] `set_bones` invalidates caches and the next `get_pose_summary` reflects the change.
- [ ] `get_screenshot` returns an `ImageContent` block the LLM can actually see; second identical call returns `unchanged: true`.
- [ ] Checkpoints persist across server restarts.
- [ ] Smoke script passes end-to-end against a real Studio instance with one character loaded.
- [ ] Errors from the plugin surface as actionable MCP tool errors, never as raw stack traces.
- [ ] No writes to stdout from anywhere in the codebase.

---

## 15. Stretch (post-v1, do not build now)

- Pose preset library (read-only directory of JSON checkpoints shipped with the package).
- "Mirror left to right" tool that reads one arm and writes the mirrored values to the other.
- Multi-character batch operations.
- WebSocket subscription to the plugin (when the plugin gains push events) so the cache stays warm without polling.
- Optional screenshot downscaling on the server side if the plugin returns oversized images.
