# Studio Pose Bridge (BepInEx plugin)

Loopback HTTP server for **StudioNeoV2** (`StudioNEOV2.exe`) using the game’s `Studio.Studio` / `OCIChar` APIs (verified against `Assembly-CSharp.dll` from HS2 Studio).

## Build

Prerequisites: .NET SDK, game install with managed assemblies.

### Quick build (script)

From this folder:

```powershell
.\build.ps1
```

Or double-click / run `build.bat` (same script).

Optional:

- **`-Hs2StudioRoot`** — game root (defaults to `D:\Honey Select` or env `HS2_STUDIO_ROOT`).
- **`-GameDir`** — if set, copies the DLL to `%GameDir%\BepInEx\plugins\StudioPoseBridge\` (or set env `HS2_GAME_DIR`).

Examples:

```powershell
.\build.ps1 -Configuration Release
.\build.ps1 -Hs2StudioRoot "E:\Games\Honey Select" -GameDir "E:\Games\Honey Select"
```

### Manual `dotnet`

Default paths are set for `D:\Honey Select` in the `.csproj`. Override with:

- MSBuild property **`HS2StudioRoot`** — game root (contains `StudioNEOV2_Data`, `BepInEx`)
- **`HS2_GAME_DIR`** — optional; if set, copies the built DLL to `%HS2_GAME_DIR%\BepInEx\plugins\StudioPoseBridge\` after build

```powershell
dotnet build .\src\StudioPoseBridge\StudioPoseBridge.csproj -c Release
```

Output (repo root): **`build\StudioPoseBridge.dll`**.

## Install

Copy `StudioPoseBridge.dll` to:

`D:\Honey Select\BepInEx\plugins\StudioPoseBridge\StudioPoseBridge.dll`

(or your game’s `BepInEx\plugins\StudioPoseBridge\`).

On first run the plugin generates a token in `BepInEx\config\com.suitji.studio_pose_bridge.cfg`. The BepInEx console prints a short token preview when the server starts.

## API

- Base URL: `http://127.0.0.1:7842` (configurable)
- Header: `X-Pose-Token` on all routes except `GET /v1/health`

See `PLAN_plugin.md` for the full endpoint list.

## Notes

- **KKAPI / HS2API:** not referenced; selection uses `Studio.Studio.Instance.treeNodeCtrl.SelectSingle` and `OCIChar.OnSelect` directly.
- Do **not** commit game DLLs; this repo expects references via `HS2StudioRoot` / your local install.
