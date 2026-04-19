# StudioNeoV2 Pose Bridge — Plugin Project Plan

A BepInEx plugin for StudioNeoV2 (Koikatsu Studio / KKS Studio family) that exposes the active scene's character poses over a local HTTP API so an external MCP server can read state, capture screenshots, and write bone transforms.

This document is the build spec. An agent following it should be able to produce a working, installable plugin without further architectural decisions.

---

## 1. Goals and non-goals

**Goals**
- Run inside StudioNeoV2 as a BepInEx 5 plugin.
- Expose an HTTP API on `127.0.0.1` (loopback only) protected by a shared-secret token.
- Allow an external client to: enumerate characters, read FK/IK bone state by region, write bones in batch, capture screenshots from canned camera angles, save/restore named pose checkpoints.
- Marshal all Unity/Studio API calls onto the main thread safely.
- Survive scene loads, character add/remove, and plugin reload without leaking threads or sockets.

**Non-goals (v1)**
- No remote network access. No authentication beyond a static token.
- No clothing, expression, accessory, or lighting manipulation.
- No animation playback, only static poses.
- No multi-client concurrency; single client at a time is fine.
- No persistence of checkpoints across Studio restarts (the MCP server owns durable storage).

---

## 2. Target environment

| Item | Value |
|---|---|
| Host application | StudioNeoV2 (Koikatsu Sunshine / KKS Studio) |
| Mod loader | BepInEx 5.4.x |
| .NET target | `net46` (matches BepInEx 5 / Unity Mono) |
| Unity version | 2019.4.x (whatever KKS ships) |
| Language | C# 7.3 |
| Required references | `BepInEx.dll`, `0Harmony.dll`, `UnityEngine.CoreModule.dll`, `UnityEngine.IMGUIModule.dll`, `Assembly-CSharp.dll` (Studio), `Assembly-CSharp-firstpass.dll` |
| Optional | `KKAPI.dll` (Studio helpers, scene events) |

Reference DLLs are pulled from a local `BepInEx/core` folder and the game's `<Game>_Data/Managed` folder. Do **not** commit them — add a `Refs/` directory to `.gitignore` and document the copy step in the README.

---

## 3. Repository layout

```
StudioPoseBridge/
├─ src/
│  └─ StudioPoseBridge/
│     ├─ StudioPoseBridge.csproj
│     ├─ Plugin.cs                  # BepInEx entry point
│     ├─ Config/
│     │  └─ PluginConfig.cs         # Port, token, log level
│     ├─ Threading/
│     │  └─ MainThreadDispatcher.cs # MonoBehaviour pump
│     ├─ Http/
│     │  ├─ HttpServer.cs           # HttpListener wrapper
│     │  ├─ Router.cs               # path -> handler dispatch
│     │  ├─ JsonHelper.cs           # SimpleJson or MiniJSON wrapper
│     │  └─ AuthMiddleware.cs       # Token check
│     ├─ Studio/
│     │  ├─ CharacterRegistry.cs    # Enumerate OCIChar
│     │  ├─ BoneAccess.cs           # Read/write FK + IK transforms
│     │  ├─ BoneRegions.cs          # Region -> bone-name mapping
│     │  ├─ PoseSerializer.cs       # Pose dict <-> JSON
│     │  └─ ScreenshotService.cs    # Camera capture + PNG encode
│     └─ Endpoints/
│        ├─ HealthEndpoint.cs
│        ├─ CharactersEndpoint.cs
│        ├─ PoseEndpoint.cs
│        ├─ BonesEndpoint.cs
│        └─ ScreenshotEndpoint.cs
├─ Refs/                            # gitignored — local DLLs
├─ build/                           # output dir
├─ README.md
└─ .gitignore
```

The csproj should copy the built DLL to `$(GameDir)/BepInEx/plugins/StudioPoseBridge/` post-build if an env var `KKS_GAME_DIR` is set, otherwise just leave it in `bin/`.

---

## 4. Plugin entry point

`Plugin.cs` must:

- Be decorated with `[BepInPlugin("dev.studiopose.bridge", "Studio Pose Bridge", "0.1.0")]`.
- Be decorated with `[BepInProcess("StudioNEOV2.exe")]` so it only loads in Studio.
- Inherit from `BaseUnityPlugin`.
- In `Awake()`:
  1. Load config (port, token, autostart bool, log level) via `Config.Bind`.
  2. Create a hidden `GameObject` named `__StudioPoseBridge__`, mark it `DontDestroyOnLoad`, attach `MainThreadDispatcher`.
  3. Construct `HttpServer` and start it if autostart is true.
  4. Log the bound URL and a short preview of the token (first 4 chars + "…") to BepInEx log.
- In `OnDestroy()`: stop the HTTP server, dispose the dispatcher GameObject.

Configuration keys:

| Section | Key | Default | Notes |
|---|---|---|---|
| `Server` | `Port` | `7842` | Loopback only |
| `Server` | `Token` | randomly generated on first run, written back to config | Required header `X-Pose-Token` |
| `Server` | `Autostart` | `true` | |
| `Logging` | `Level` | `Info` | `Debug` shows every request |

---

## 5. Main-thread dispatcher

`MainThreadDispatcher : MonoBehaviour`

```csharp
private static readonly ConcurrentQueue<Action> _queue = new ConcurrentQueue<Action>();

public static Task<T> RunAsync<T>(Func<T> job) {
    var tcs = new TaskCompletionSource<T>();
    _queue.Enqueue(() => {
        try { tcs.SetResult(job()); }
        catch (Exception e) { tcs.SetException(e); }
    });
    return tcs.Task;
}

void Update() {
    while (_queue.TryDequeue(out var action)) {
        try { action(); }
        catch (Exception e) { Plugin.Log.LogError(e); }
    }
}
```

**Hard rule for the agent**: any code that touches `OCIChar`, `Transform`, `Camera`, `RenderTexture`, or anything in the `Studio` namespace MUST be wrapped in `MainThreadDispatcher.RunAsync`. HTTP handlers run on threadpool threads. Direct Unity access from a handler will crash silently.

---

## 6. HTTP server

Use `System.Net.HttpListener` (available in Unity Mono).

`HttpServer` responsibilities:
- Bind to `http://127.0.0.1:{port}/`.
- Accept loop on a background `Thread` (not threadpool — needs to live for the plugin's lifetime).
- For each request: pass to `Router.Dispatch`, await the response, write status + body.
- Catch all exceptions, return JSON error objects, never let an exception kill the listener thread.
- On stop: call `_listener.Stop()` and `_listener.Close()`, join the accept thread with a 1 s timeout.

`Router`:
- Method + path -> `Func<HttpListenerRequest, Task<ApiResponse>>`.
- All routes prefixed with `/v1/`.
- 404 with JSON body for unknown routes.

`AuthMiddleware`:
- Reject any request without a matching `X-Pose-Token` header with HTTP 401 and `{"error":"unauthorized"}`.
- Skip auth for `/v1/health` so the MCP server can probe without configuration.

Response envelope (every endpoint returns this shape):

```json
{ "ok": true, "data": { ... } }
{ "ok": false, "error": "human readable", "code": "E_NOT_FOUND" }
```

JSON: use SimpleJson or MiniJSON (single file, drop into `Http/`). Do not pull Newtonsoft — version conflicts with KKS plugins are common.

---

## 7. Endpoints (full surface)

### 7.1 `GET /v1/health`

No auth. Returns:

```json
{ "ok": true, "data": { "version": "0.1.0", "studio": "neov2", "scene_loaded": true } }
```

### 7.2 `GET /v1/characters`

```json
{ "ok": true, "data": { "characters": [
  { "id": 0, "name": "Aiko", "sex": "female", "selected": true, "position": [0.0, 0.0, 0.0] }
]}}
```

`id` is the dictionary key from `Studio.Instance.dicObjectCtrl` (treeNodeObject ID). It is stable for the lifetime of that character in the scene.

### 7.3 `POST /v1/characters/{id}/select`

Marks the character as the active target for subsequent calls that omit an explicit ID. Empty `data` on success.

### 7.4 `GET /v1/characters/{id}/pose`

Query params:
- `regions` (comma list, optional): `head, neck, torso, hips, left_arm, right_arm, left_hand, right_hand, left_leg, right_leg, left_foot, right_foot`. Default: `torso,left_arm,right_arm,left_leg,right_leg`.
- `space` (optional): `local` (default) or `world`.
- `include_ik` (optional bool): also return current IK target positions.
- `precision` (optional int, default `1`): decimal places for euler degrees.

Response:

```json
{
  "ok": true,
  "data": {
    "character_id": 0,
    "fk_enabled": true,
    "ik_enabled": false,
    "space": "local",
    "regions": {
      "torso": [
        { "bone": "cf_j_spine01", "rot_euler": [2.1, 0.0, -1.4] },
        { "bone": "cf_j_spine02", "rot_euler": [3.0, 0.0, 0.0] }
      ],
      "left_arm": [ ... ]
    }
  }
}
```

Round eulers to `precision` decimals on the server side. Do not return quaternions by default (token cost). Include a quaternion only if `?format=quat` is passed.

### 7.5 `POST /v1/characters/{id}/bones`

Batch write. Body:

```json
{
  "space": "local",
  "changes": [
    { "bone": "cf_j_shoulder_L", "rot_euler": [0, 0, -20] },
    { "bone": "cf_j_arm00_L",    "rot_euler": [0, 90, 0] },
    { "bone": "cf_j_forearm01_L","rot_euler": [0, 0, -90], "mode": "absolute" }
  ]
}
```

`mode` defaults to `absolute`. Other allowed values: `relative` (added to current), `nudge` (alias of relative).

Implementation:
1. Resolve character on main thread.
2. If `fk_enabled` is false, force-enable it before writing FK bones (and remember to log a warning in the response).
3. For each change, find the `OCIChar.BoneInfo` by name, set rotation via the bone's `guideObject.changeAmount.rot = newEuler` (this is the call that hooks Studio's undo + FK pipeline). Do NOT write `transform.localRotation` directly — Studio will overwrite it next frame.
4. Return the post-write euler for each bone so the client can verify.

Response:

```json
{
  "ok": true,
  "data": {
    "applied": [
      { "bone": "cf_j_shoulder_L", "rot_euler": [0, 0, -20] }
    ],
    "skipped": [
      { "bone": "cf_j_doesnotexist", "reason": "unknown_bone" }
    ],
    "warnings": ["fk_was_disabled_auto_enabled"]
  }
}
```

### 7.6 `POST /v1/characters/{id}/fk_ik`

Body: `{ "fk": true, "ik": false }`. Either field optional.

### 7.7 `GET /v1/characters/{id}/screenshot`

Query params:
- `angle`: `front | back | left | right | three_quarter | top | current`. Default `current`.
- `size`: integer pixel size for the longer edge, default `512`, max `1024`.
- `format`: `png` (default) or `jpg`.
- `framing`: `head | upper_body | full_body | tight`, default `full_body`. Used by canned angles to set distance.

For canned angles, instantiate (or reuse) a hidden secondary `Camera` parented to a transform that targets the character's pelvis. Position it based on `framing`. Render to a `RenderTexture`, `Texture2D.ReadPixels`, encode, base64.

For `current`, render the existing main camera to texture without moving it.

Response:

```json
{
  "ok": true,
  "data": {
    "format": "png",
    "width": 512,
    "height": 512,
    "image_base64": "iVBORw0K..."
  }
}
```

The MCP server is responsible for caching by hash; the plugin always renders fresh.

### 7.8 `POST /v1/characters/{id}/checkpoints`

Body: `{ "name": "before_arm_fix", "regions": ["left_arm","right_arm"] }`. Regions optional, default = all known regions.

Stores the current pose in an in-memory dict keyed by `(character_id, name)`. Returns the snapshot so the MCP server can mirror it durably.

### 7.9 `POST /v1/characters/{id}/checkpoints/{name}/restore`

Restores the named checkpoint. Returns the bone diff that was applied.

### 7.10 `GET /v1/bones/regions`

Static endpoint that returns the full region → bone-name mapping the plugin is using. Lets the MCP server discover the schema instead of hardcoding it.

---

## 8. Bone region mapping

`BoneRegions.cs` holds a static dictionary. The agent should populate it with KKS/KK bone names. Starting set (verify against the actual `OCIChar.listBones` output in-game and adjust):

```
head:        cf_j_head, cf_j_neck
torso:       cf_j_spine01, cf_j_spine02, cf_j_spine03
hips:        cf_j_hips, cf_j_waist01, cf_j_waist02
left_arm:    cf_j_shoulder_L, cf_j_arm00_L, cf_j_forearm01_L
right_arm:   cf_j_shoulder_R, cf_j_arm00_R, cf_j_forearm01_R
left_hand:   cf_j_hand_L, cf_j_thumb01_L..03_L, cf_j_index01_L..03_L, ...
right_hand:  (mirror)
left_leg:    cf_j_thigh00_L, cf_j_leg01_L, cf_j_leg03_L
right_leg:   (mirror)
left_foot:   cf_j_foot_L, cf_j_toes_L
right_foot:  (mirror)
```

**Discovery utility**: add a `GET /v1/characters/{id}/bones/all` debug endpoint (gated behind `Logging.Level = Debug`) that dumps every bone name from `OCIChar.listBones` so the agent can verify mappings against a real character on first run.

---

## 9. Screenshot service details

```csharp
RenderTexture rt = RenderTexture.GetTemporary(width, height, 24);
camera.targetTexture = rt;
camera.Render();
RenderTexture prev = RenderTexture.active;
RenderTexture.active = rt;
var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
tex.Apply();
RenderTexture.active = prev;
camera.targetTexture = null;
RenderTexture.ReleaseTemporary(rt);
byte[] bytes = format == "jpg" ? tex.EncodeToJPG(85) : tex.EncodeToPNG();
UnityEngine.Object.Destroy(tex);
return Convert.ToBase64String(bytes);
```

For canned angles, compute the character's pelvis world position, then offset:
- `front`: pelvis + (0, 0.2, 2.0) looking at pelvis+(0,0.1,0)
- `side` (left/right): pelvis + (±2.0, 0.2, 0)
- `back`: pelvis + (0, 0.2, -2.0)
- `three_quarter`: pelvis + (1.4, 0.4, 1.4)
- `top`: pelvis + (0, 2.5, 0.01) looking down

Distances scale with `framing`: `head` × 0.4, `upper_body` × 0.7, `full_body` × 1.0, `tight` × 0.55.

The secondary camera should be created lazily, kept in a pool of one, and never destroyed during a session.

---

## 10. Error codes

| Code | HTTP | Meaning |
|---|---|---|
| `E_UNAUTHORIZED` | 401 | Bad/missing token |
| `E_NOT_FOUND` | 404 | Unknown route |
| `E_NO_CHARACTER` | 404 | Character ID not in scene |
| `E_UNKNOWN_BONE` | 422 | Bone name not found on character (per-bone, returned in `skipped`) |
| `E_BAD_REQUEST` | 400 | Malformed JSON / missing field |
| `E_FK_DISABLED` | 409 | FK write attempted while FK off and auto-enable disabled |
| `E_INTERNAL` | 500 | Anything else; include stack in BepInEx log, not in response |

---

## 11. Logging

Use BepInEx's `ManualLogSource`. Levels:

- `Info`: server start/stop, port, character (de)select.
- `Warning`: auto-enabled FK, unknown bones, restored checkpoint.
- `Error`: exceptions in handlers. Include the request path.
- `Debug` (only if config `Logging.Level=Debug`): every request line, every bone write.

Never log the auth token. Never log full base64 screenshots.

---

## 12. Build, install, smoke test

**Build**
1. Restore NuGet (just `BepInEx.Core` if you want; otherwise reference local DLLs).
2. `dotnet build -c Release`.
3. Output: `bin/Release/net46/StudioPoseBridge.dll`.

**Install**
1. Copy the DLL to `<KKS>/BepInEx/plugins/StudioPoseBridge/StudioPoseBridge.dll`.
2. Launch StudioNeoV2.
3. Open BepInEx console; verify the line `[Info: Studio Pose Bridge] listening on http://127.0.0.1:7842 token=abcd…`.

**Smoke test (with curl, from another shell)**
```
curl http://127.0.0.1:7842/v1/health
curl -H "X-Pose-Token: <token>" http://127.0.0.1:7842/v1/characters
```

Load a character in Studio first or `characters` will return an empty list.

---

## 13. Definition of done for v1

- [ ] Plugin loads in StudioNeoV2 without errors in BepInEx console.
- [ ] `/v1/health` reachable without token.
- [ ] All other endpoints reject missing/wrong token with 401.
- [ ] `/v1/characters` lists every loaded character with stable IDs.
- [ ] `/v1/characters/{id}/pose` returns named regions only, eulers rounded.
- [ ] `/v1/characters/{id}/bones` writes apply, persist after `Update()` ticks, and survive switching to another character and back.
- [ ] FK auto-enable works and emits the warning.
- [ ] `/v1/characters/{id}/screenshot` returns a valid PNG that decodes to the requested size.
- [ ] Canned angles (`front`, `side`, `three_quarter`) frame the active character without showing UI.
- [ ] Checkpoints save and restore correctly across at least 5 round trips.
- [ ] No threadpool exceptions in BepInEx log after a 50-request fuzz from `curl` in a loop.
- [ ] Plugin can be reloaded (BepInEx hot-reload off; restart Studio) without leaking the port.

---

## 14. Stretch (post-v1, do not build now)

- WebSocket channel for push events (manual bone edits in Studio UI → emit `bone_changed`).
- Multiple secondary cameras for stereo / depth captures.
- Expression and look-at controls.
- Per-character undo stack exposed via API.
- Recording mode: capture every applied change to a JSON log for replay.
