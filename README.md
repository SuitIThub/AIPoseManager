# AIPoseManager

Monorepo for **Studio Pose Bridge**: a BepInEx plugin for **StudioNeoV2** (Honey Select 2 / `StudioNEOV2.exe`) that exposes poses over loopback HTTP, plus a **Python MCP server** that wraps that API for LLM clients (Cursor, Claude Desktop, etc.).

| Project | Path | Role |
|--------|------|------|
| Plugin | [StudioPoseBridge/](StudioPoseBridge/) | In-game HTTP API on `127.0.0.1` |
| MCP | [studio-pose-mcp/](studio-pose-mcp/) | `stdio` MCP tools → HTTP |

**Author:** Suit-Ji · **GitHub:** [github.com/SuitIThub](https://github.com/SuitIThub)

## Quick start

1. Build the plugin (see [StudioPoseBridge/README.md](StudioPoseBridge/README.md)), deploy the DLL under `BepInEx/plugins/StudioPoseBridge/`, start Studio, copy the token from the BepInEx log.
2. Install the MCP server: `pip install -e ./studio-pose-mcp` and set `POSE_PLUGIN_TOKEN` (see [studio-pose-mcp/README.md](studio-pose-mcp/README.md)).

Spec sources: `PLAN_plugin.md`, `PLAN_mcp_server.md`.
