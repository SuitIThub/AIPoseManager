# studio-pose-mcp

Python **stdio** MCP server that talks to the Studio Pose Bridge plugin over HTTP.

**Author:** Suit-Ji · **GitHub:** [github.com/SuitIThub](https://github.com/SuitIThub)

## Setup

Requires Python 3.11+.

```bash
pip install -e .
```

Environment variables (prefix `POSE_`):

| Variable | Meaning |
|----------|---------|
| `POSE_PLUGIN_TOKEN` | **Required.** Same token as in BepInEx log / plugin config. |
| `POSE_PLUGIN_URL` | Default `http://127.0.0.1:7842` |
| `POSE_STORAGE_DIR` | Default `~/.studio_pose_bridge` (disk checkpoints) |
| `POSE_SCREENSHOT_SIZE` | Default `512` |
| `POSE_DEFAULT_REGIONS` | Comma-separated region names |
| `POSE_TIMEOUT` / `POSE_SCREENSHOT_TIMEOUT` | HTTP timeouts (seconds) |
| `POSE_LOG_LEVEL` | Default `INFO` |

Copy `.env.example` to `.env` or set variables in your MCP host.

The server **refuses to start** without `POSE_PLUGIN_TOKEN`, and runs a **health check** against the plugin before entering the MCP main loop.

## Run

```bash
studio-pose-mcp
# or
python -m studio_pose_mcp
```

## Tools

Eight tools: `list_characters`, `select_character`, `get_pose_summary`, `set_bones`, `get_screenshot`, `save_checkpoint`, `restore_checkpoint`, `get_diff_since_checkpoint`.

Screenshots return an MCP image when the frame changes; identical consecutive screenshots return JSON `{ "unchanged": true, ... }` to save tokens.

## Cursor / Claude Desktop (example)

```json
{
  "mcpServers": {
    "studio-pose": {
      "command": "studio-pose-mcp",
      "env": {
        "POSE_PLUGIN_TOKEN": "<paste from BepInEx log>"
      }
    }
  }
}
```

## Manual smoke (HTTP)

With Studio running and a character loaded:

```bash
set POSE_PLUGIN_TOKEN=...
python -m studio_pose_mcp.scripts.smoke
```

## Tests

```bash
pip install -e ".[dev]"
pytest
```

## Workflow (for LLMs)

1. `list_characters` if IDs are unknown.
2. `get_screenshot` + `get_pose_summary` for baseline.
3. `save_checkpoint` before risky edits.
4. One batched `set_bones`, then `get_screenshot` to verify.
5. Prefer screenshots over dumping every bone.
