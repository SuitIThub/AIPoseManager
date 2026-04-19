# Testing plan — Studio Pose Bridge + MCP

Use this checklist to verify the **BepInEx plugin** (HTTP) and **studio-pose-mcp** (Claude Desktop / MCP) end-to-end.

**Checkboxes:** Markdown task lists (`- [ ]` / `- [x]`) are **interactive on GitHub** (click in the file view to toggle when you have write access). In Cursor/VS Code they usually appear as static boxes unless you use an extension that syncs task list state. You can still tick them manually by editing `- [ ]` → `- [x]`.

---

## Prerequisites

- [ ] StudioNeoV2 running with **at least one character** in the scene
- [ ] Plugin loaded: BepInEx log shows `listening on http://127.0.0.1:7842/` (or your port) and a **token preview**
- [ ] Token matches: `BepInEx\config\com.suitji.studio_pose_bridge.cfg`, MCP env / Claude bundle `user_config`, and any manual HTTP tests
- [ ] MCP connected in Claude Desktop (tools visible; no startup error about health or token)

---

## Phase A — Plugin HTTP API

Quick automated slice: set `POSE_PLUGIN_TOKEN` and run `python -m studio_pose_mcp.scripts.smoke` from `studio-pose-mcp`. Then complete any remaining rows below with `curl` or a REST client.

### Auth & health

- [ ] **A1** `GET /v1/health` (no token) → `200`, `ok: true`, `data.version`, `scene_loaded` sensible
- [ ] **A2** `GET /v1/characters` **without** `X-Pose-Token` → `401`, `E_UNAUTHORIZED`
- [ ] **A3** Same with **wrong** token → `401`

### Characters & schema

- [ ] **A4** `GET /v1/characters` with token → `200`, `data.characters` is an array; IDs stable for the session
- [ ] **A5** `POST /v1/characters/{id}/select` → `200`, `ok: true`
- [ ] **A6** `GET /v1/bones/regions` → `200`, `regions` includes keys like `torso`, `left_arm`

### Pose read / write

- [ ] **A7** `GET /v1/characters/{id}/pose?regions=torso&precision=1` → `200`, bones with `rot_euler`
- [ ] **A8** `POST /v1/characters/{id}/bones` — one known bone, small **relative** delta → `200`, bone in `applied`, not in `skipped`
- [ ] **A9** `GET` pose same region again → values reflect change vs A7

### FK/IK & screenshot

- [ ] **A10** `POST /v1/characters/{id}/fk_ik` body `{"fk":true,"ik":false}` → `200`
- [ ] **A11** `GET /v1/characters/{id}/screenshot?angle=three_quarter&size=512&format=png` → `200`, `image_base64` decodes to PNG; width/height plausible

### Checkpoints (plugin memory)

- [ ] **A12** `POST /v1/characters/{id}/checkpoints` body `{"name":"test_cp","regions":["torso"]}` → `200`, snapshot / `bone_count`
- [ ] **A13** `POST /v1/characters/{id}/checkpoints/test_cp/restore` → `200`, `diff` present

### Optional (debug)

- [ ] **A14** Plugin `Logging.Level` = **Debug**, `GET /v1/characters/{id}/bones/all` → `200`, long `bones` list

---

## Phase B — MCP tools (Claude Desktop)

Use a **fixed character id** from `list_characters` where needed.

### Core

- [ ] **B1** `list_characters` — JSON includes `characters`; matches Studio
- [ ] **B2** `select_character` — returns `selected`; optional: list again shows `selected: true` on that row
- [ ] **B3** `get_pose_summary` **without** `character_id` (after B2) — uses selection; `pose_compact` non-empty; `cached_at` present
- [ ] **B4** `get_pose_summary` **with** `character_id` + narrow `regions` (e.g. `torso`) — compact lines only for that region

### Bones & screenshots

- [ ] **B5** `set_bones` — small **relative** nudge on a shoulder → response has `applied`, no wrapper error JSON
- [ ] **B6** `get_pose_summary` again — compact output reflects change
- [ ] **B7** `get_screenshot` — **first** call returns an **image** in the transcript
- [ ] **B8** `get_screenshot` — **second** call same angle/size/framing → `{ "unchanged": true, ... }`
- [ ] **B9** `get_screenshot` — change `angle` or `framing` → new image (not unchanged-only)

### Checkpoints & diff

- [ ] **B10** `save_checkpoint` — name e.g. `mcp_test_before` → `saved` + `bone_count` > 0
- [ ] **B11** `set_bones` — another visible change → `applied`
- [ ] **B12** `get_diff_since_checkpoint` — checkpoint `mcp_test_before`, small `tolerance_deg` → `changed` / `unchanged_count` sensible
- [ ] **B13** `restore_checkpoint` — same name → `bones_changed` > 0; pose/screenshot near “before”
- [ ] **B14** `get_diff_since_checkpoint` again after restore — few or no entries in `changed` (within tolerance)

---

## Phase C — End-to-end LLM loop (single session)

- [ ] **C1** `list_characters` → pick one id
- [ ] **C2** `select_character` → `get_screenshot` (`three_quarter`) + `get_pose_summary` (`torso,left_arm`)
- [ ] **C3** `save_checkpoint` (`pre_edit`)
- [ ] **C4** `set_bones` (one batch, 2–3 bones)
- [ ] **C5** `get_screenshot` — visually confirms change
- [ ] **C6** `get_diff_since_checkpoint` (`pre_edit`)
- [ ] **C7** `restore_checkpoint` (`pre_edit`) → `get_screenshot` — roughly back to pre-edit

---

## Phase D — Resilience

- [ ] **D1** Restart **Studio** (plugin reloads; new token if config wiped) — update token everywhere; `list_characters` works
- [ ] **D2** Restart **MCP only** — reconnect; `get_pose_summary` works if Studio still running
- [ ] **D3** After `save_checkpoint`, restart **MCP server** (not Studio) — `restore_checkpoint` still works (disk under `~/.studio_pose_bridge` / `POSE_STORAGE_DIR`)
- [ ] **D4** Wrong `character_id` on a tool — actionable error, **no** raw stack trace in the assistant
- [ ] **D5** Studio quit — MCP call fails with clear connection error

---

## Coverage summary

| Layer        | What this validates |
|-------------|----------------------|
| Plugin HTTP | Auth, characters, pose, bones, FK/IK, screenshot, checkpoints, optional debug bones |
| MCP         | All 8 tools, selection default, screenshot dedup, checkpoint save/diff/restore, disk persistence |

When everything above is checked, treat **v1** as verified for your machine.
