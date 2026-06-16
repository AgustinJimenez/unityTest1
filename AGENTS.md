# Repository Guidelines

This repository is a Unity 6 (6000.3.2f1) project using URP and the new Input System.

## Project Structure & Module Organization
- `Assets/` contains all runtime content.
  - `Assets/Scripts/` for gameplay/runtime scripts.
  - `Assets/Scripts/Editor/` for editor-only tools and automation.
  - `Assets/Scenes/` for scene files.
  - `Assets/Settings/` and `ProjectSettings/` for URP and project configuration.
- `Packages/manifest.json` lists Unity packages and versions.
- If you add tests, use `Assets/Tests/EditMode/` and `Assets/Tests/PlayMode/`.

## Build, Test, and Development Commands
- Open via Unity Hub (6000.3.2f1 or compatible).
- Press Play to run locally.
- Build via `File > Build Settings...`; tests via `Window > General > Test Runner`.

## Coding Style & Naming Conventions
- C# style: 4-space indentation, PascalCase for types/methods, camelCase for fields and locals.
- Use `[SerializeField] private` fields for inspector-exposed values; keep public fields minimal.
- Input should use the new Input System (`PlayerInput` and action maps), not `Input.GetKey()`.
- Place new runtime scripts under `Assets/Scripts/` and editor utilities under `Assets/Scripts/Editor/`.

## Testing Guidelines
- Unity Test Framework with `*Tests.cs` files; add coverage for new gameplay logic when feasible.

## Commit & Pull Request Guidelines
- Commit messages follow an imperative, concise style (e.g., `Add ...`, `Document ...`).
- PRs should include a short description, linked issue/task (if any), and reproduction or test steps.
- For visual or scene changes, include screenshots or a brief screen recording.
- Ensure `.meta` files for new/changed assets are committed.

## Unity Editor Workflow Notes
- `.csproj` and `.sln` files are Unity-generated; do not edit them manually.
- Asset compilation is automatic when scripts change; wait for Unity to finish compiling before testing.

## Editor Tools & Setup
- `Tools > Third Person > Complete Setup` builds the player/camera/ground and animation wiring.
- Auto-run is enabled on compile by default; toggle via `Tools > Third Person > Auto Run Setup`.

## Input & Animation Notes
- Input actions live in `Assets/InputSystem_Actions.inputactions`; prefer `PlayerInput` action maps.
- For Mixamo imports, keep a consistent avatar and ensure root motion is disabled on scripted movement.

## Current Workflow & Debugging
- After code changes, let Unity recompile, wait for auto-setup to finish, then press Play.
- `ThirdPersonController` logs jump/state transitions by default; use the Console for clip names and timestamps.
- For historical logs, check the Unity Editor log (macOS: `~/Library/Logs/Unity/Editor.log`) and filter with `rg` when needed.
- When you need my attention to continue, play a TTS notification with a clear custom message.

## MCP for Unity (preferred — semantic editor control)
This project ships the CoplayDev **MCP for Unity** package (`Assets/MCPForUnity`, package `com.coplaydev.unity-mcp` v9.0.3, server `mcp-for-unity-server` v2.14.1). It gives Claude a real API into the Editor (scene graph, GameObjects, components, assets, scripts, console, menu items) — prefer it over computer-control for anything precise.

### Bringing it up (order matters)
1. In Unity, start the bridge: `Window > MCP For Unity` (shortcut `Ctrl+Shift+M`) → it launches a local HTTP server on `http://localhost:3000/mcp` and registers its tools.
2. Only then start/restart Claude Code — MCP servers connect at session startup, so if the bridge is down at launch the tools never register (and `ToolSearch`/`/mcp` won't show them).
3. Keep the Unity Editor open while using the tools.

### Config gotcha (important)
In `.mcp.json` the `unityMCP` entry **must** declare the transport type, or Claude Code silently skips it:
```json
"unityMCP": { "type": "http", "url": "http://localhost:3000/mcp" }
```
A bare `{ "url": "..." }` (no `type`) loads stdio `command` servers fine but will NOT connect this HTTP server. `.mcp.json` is gitignored (local-only), so this lives on each machine.

### Key tools
`manage_scene`, `manage_gameobject`, `manage_components`, `manage_asset`, `manage_prefabs`, `manage_material`, `find_gameobjects`, `read_console`, `execute_menu_item` (e.g. run `Tools/Setup`), `manage_script`/`apply_text_edits`, `run_tests`. Use the `mcpforunity://` resources (`editor_state`, `project_info`, etc.) to read state before mutating. After any script change, poll `editor_state.isCompiling` and check `read_console` for compile errors before using new types.

## Computer Control MCP Setup
This project also has a computer-control MCP server (GUI automation fallback — works with zero Unity-side setup, but pixel-level rather than semantic) that lets Claude interact with the Unity Editor via mouse, keyboard, and screenshots.

### Available Tools
- `take_screenshot` / `take_screenshot_with_ocr` - Capture screen and extract text
- `click_screen` / `move_mouse` / `drag_mouse` - Mouse control
- `type_text` / `press_keys` - Keyboard input
- `key_down` / `key_up` - Hold/release keys
- `mouse_down` / `mouse_up` - Hold/release mouse buttons
- `activate_window` / `list_windows` - Window management
- `wait_milliseconds` - Timing control
- `get_screen_size` - Screen dimensions

### Configuration Files
- `.mcp.json` - MCP server configuration pointing to `computer-control-mcp.exe`
- `.claude/settings.local.json` - Pre-approved permissions for all MCP tools

### Usage Tips
- Use OCR-based screenshots when possible to minimize token usage
- Only request full visual screenshots when OCR can't identify UI elements
- Unity Editor must be visible on screen for interactions to work
- For Windows Graphics Capture (GPU-accelerated windows), the `COMPUTER_CONTROL_MCP_WGC_PATTERNS` env var is set to `unity,game`

### Workflow with Computer Control
1. Take a screenshot to see the current Unity Editor state
2. Use OCR or visual analysis to identify UI elements
3. Click menus, buttons, hierarchy items as needed
4. Type into fields, use keyboard shortcuts
5. Verify actions with follow-up screenshots

## Kimodo Motion Generation Pipeline
Text-prompt 3D motions (NVIDIA Kimodo, installed at `E:\repo\kimodo`) are retargeted onto the
character and exposed as switchable in-game clips. See the auto-memory note for full install/gotcha
details. Workflow:

- **One command** (from the kimodo repo, encoder must be installed/cached):
  `.venv/Scripts/python.exe unity_export/gen_to_unity.py "<prompt>" --name <Name> [--duration 5 --seed N]`
  → generates a T-pose BVH, converts to FBX in Blender, and drops it into
  `Assets/_Game/Resources/KimodoMotions/<Name>.fbx`.
- **Batch many clips**: start `kimodo_textencoder` (loads the 8B encoder once on :9550), then loop
  `gen_to_unity.py` — each call reuses the service instead of reloading the encoder. See
  `unity_export/batch_locomotion.sh`.
- **Unity side**: `Assets/_Game/Scripts/Editor/KimodoImport.cs` (AssetPostprocessor) auto-imports
  anything in `Resources/KimodoMotions/` as Humanoid and renames the clip to the file name (in
  `OnPreprocessAnimation`, not `OnPreprocessModel` — takes aren't parsed yet in the latter).
  `GameMenu.cs` lists all Kimodo clips in the Escape → Animations menu (loaded via
  `Resources.LoadAll`), switchable, retargeted live via a PlayableGraph. The preview anchors the
  character horizontally so clips with baked-in travel play in place.
- **Prompts**: see the **Prompting best practices** block below — short, "A person…", one
  behavior, and do NOT micro-direct the arms (that fights natural swing). In-place motions may
  carry root translation (Kimodo bakes travel into every clip).
- **Consistent character across clips (IMPORTANT):** `--seed` defaults to `None`, so every clip is
  generated with a *different random seed* — in a diffusion model the seed IS the "take", so clips
  end up feeling like different personalities (posture/energy drift). To make the character move
  consistently: **(1) use one fixed `--seed` for the whole set**, **(2)** repeat the same
  build/posture descriptor prefix in every prompt, **(3)** optionally raise `--cfg_weight` for
  tighter prompt adherence. For exact poses, author **constraints** (Full-Body keyframes /
  End-Effector hand-foot / 2D-Root path) in `kimodo_demo` and pass via `--constraints <json>`.
  Multi-prompt strings (periods) generate in sequence with continuity (`--num_transition_frames`).

### Prompting best practices (from NVIDIA's official Best Practices page — IMPORTANT)
Source: https://research.nvidia.com/labs/sil/projects/kimodo/docs/key_concepts/limitations.html
There is little community/forum guidance (Kimodo is new); NVIDIA's page is the authority. Our
earlier verbose, off-distribution prompts (`"a man, average build, neutral relaxed posture, …,
arms relaxed at his sides, calm neutral everyday movement"`) caused the bad results: weird/stiff
walk arms and a vague forward-bend "crouch" that read as bending at the waist. Fixes:

- **Start with "A person…".** This matches the training-data prompt style. Kimodo generates only
  the MOTION — the male appearance comes from our retargeted Unity character mesh — so "a person"
  vs "a man" changes motion *style*, not visible gender. Prefer **"A person…"** for quality.
- **Use the trained STYLE keywords** as subject stylization (the model was trained on these):
  `tired, angry, happy, sad, scared, drunk, injured, stealthy, old, childlike`. e.g.
  **"A stealthy person crouches…"** yields a tactical/masculine sink-down crouch far better than
  hand-describing "hips back, torso upright". Also valid: "An old person…", "A drunk person…".
- **Medium detail, ONE behavior per prompt.** "A person walks." is too vague; long prompts that
  describe each body part *blur motion intent*. **Do NOT micro-direct arms/posture** — telling the
  arms to stay "at his sides" is what broke the walk's natural swing. Split long action sequences
  into multiple prompts.
- **Stay inside trained behaviors:** locomotion, gestures, everyday activities, object
  interactions, videogame combat, dancing, + the styles above. Out-of-domain prompts (e.g.
  baseball) give bad results. BONES-SEED prompts show the right granularity:
  https://huggingface.co/datasets/bones-studio/seed
- **Constraints:** ≤20 keyframes per constraint type (except dense 2D root paths); don't
  contradict the text prompt; enable post-processing when foot-contact accuracy matters. Tune the
  text-vs-constraint tradeoff with `--cfg_type`/`--cfg_weight`.
- **Multi-prompt:** each prompt needs standalone context; the transition happens at the *start* of
  the next prompt (it spends some of its duration transitioning). Max 10 s per prompt.
- **<16 GB VRAM:** `TEXT_ENCODER_DEVICE=cpu` forces the encoder to CPU.

**Clip length / `--duration` convention:** match duration to the clip's role. **Idle/held poses
(Idle, CrouchIdle) → short loops (~2–3 s)** — long idles waste frames and loop worse. Locomotion
cycles (walk/run/strafe) ~4–5 s. Don't over-length idles. Also pick the *pose vocabulary*
carefully: "crouch" → tactical half-stance; for a **deep resting squat** (hips dropped onto the
heels, knees fully bent, torso upright) say **"rests in a deep low squat, sitting on his heels"**,
not "crouches" (which gave a waist-bend stoop).

### Inspecting generated poses (render without opening Unity by hand)
Two ways to eyeball a Kimodo clip's pose:
- **Mesh render (preferred — real character):** `Tools ▸ Capture Kimodo Poses`
  (`Assets/_Game/Scripts/Editor/KimodoPoseCapture.cs`). Samples each matching clip onto the actual
  Humanoid character via `AnimationMode` (same retarget as the in-game preview), renders side+front
  to `E:/repo/kimodo/_renders/mesh/<clip>_{side,front}.png` (outside Assets so it isn't imported).
  Character is isolated on a spare layer + culling mask so the level geometry doesn't clutter the
  shot. Filter substring via `EditorPrefs "KimodoCapture.Filter"` (default `CrouchIdle`).
- **Mesh VIDEO loop:** `Tools ▸ Capture Kimodo Videos` (same script) renders a frame-per-frame PNG
  sequence per clip from a fixed 3/4 camera into `E:/repo/kimodo/_renders/mesh/video/<clip>/`. Encode
  to looped MP4 with ffmpeg (on PATH): `ffmpeg -y -framerate 30 -i frame_%04d.png -c:v libx264
  -pix_fmt yuv420p out.mp4`, then `ffmpeg -stream_loop 2 -i out.mp4 -c copy looped.mp4` for a 3× loop.
  Lets you (or the user, via WhatsApp) judge whether an idle loop holds vs pops/descends.
- **Skeleton stick-figure (no Unity needed):** `kimodo/unity_export/render_pose.py` /
  `render_orbit.py` / `render_compare.py` — headless Blender draws a cylinder-per-bone of the FBX
  (armature-only, so no mesh) from any angle. The FBX imports at a large scale, so bone thickness is
  sized relative to the skeleton bounds. Useful when Unity isn't open; the mesh capture is clearer.

## GVHMR Video-to-Motion Pipeline (`E:\repo\gvhmr`)
Monocular **video → world-grounded SMPL-X motion → Unity Humanoid FBX**. The video-based
counterpart to Kimodo's text-based generation; clips land in the same in-game menu and retarget
onto the character. Runs in **WSL Ubuntu** on the 4090 (pytorch3d has no usable Windows wheel).

**Env:** repo at `/mnt/e/repo/gvhmr`, venv `/mnt/e/repo/gvhmr/.venv` (built with
`virtualenv --always-copy` — plain `venv` fails on `/mnt/e`: it makes a `lib64→lib` symlink and
the mount blocks symlinks). Python 3.10, torch 2.3.0+cu121, pytorch3d 0.7.6. Caches on E:
(WSL `~/.bashrc` exports `HF_HOME`/`TORCH_HOME`/`PIP_CACHE_DIR` → `/mnt/e/repo/ai_models`).
DPVO intentionally not built — always run with `-s` (static cam).

**Run (one command per step, all on E:):**
1. `bash wsl_run.sh <video>` → `outputs/demo/<name>/hmr4d_results.pt` (+ overlay mp4). Multi-angle
   source? Split shots first: `bash wsl_get_video.sh <url> <name>` (PySceneDetect `detect-content
   -t 12`) — GVHMR static-cam assumes ONE continuous shot, cuts produce garbage.
2. `python extract_motion.py <pt> <npz>` — pulls `smpl_params_global` to a plain npz (Blender has no torch).
3. `"C:\Program Files\Blender Foundation\Blender 5.0\blender.exe" -b -P gvhmr_blender_fbx.py --
   <SMPLX_NEUTRAL.npz> <motion.npz> <out.fbx> 30` — builds the SMPL-X skinned body, bakes exact
   LBS via `pose_bone.matrix_basis` FK (order-independent; avoids bone-roll bugs), exports Unity FBX.
   Verify a frame with `render_fbx_check.py`.
4. Copy FBX → `Assets/_Game/Resources/GVHMRMotions/`. `KimodoImport.cs` matches that folder too
   (Humanoid + rename + loop); `GameMenu.cs` lists them under a **GVHMR** category. Unity's auto
   Humanoid map (CreateFromThisModel) maps the SMPL-X bone names correctly (pelvis→Hips,
   spine1→Spine, left_collar→LeftShoulder, …) — **no explicit HumanDescription needed** (verified
   in the generated `.fbx.meta`).

**Gotchas (all real):**
- **Upstream bug:** `hmr4d/utils/body_model/body_model.py` line 1 had `from turtle import forward`
  (stray IDE auto-import) → crashes on headless WSL (no tkinter). Deleted it.
- **No apt/sudo for ffmpeg:** `imageio-ffmpeg`'s static binary is wrapped as `.venv/bin/ffmpeg`.
  `ffprobe` is NOT bundled — use `ffmpeg -i` to read info.
- **Checkpoints:** Google-Drive folder hits an anonymous-download quota; use the HF mirror
  `hf download camenduru/GVHMR "<sub>/<file>" --local-dir inputs/checkpoints` (yolov8x from
  ultralytics release assets). SMPL/SMPL-X body models are license-gated (user downloads): SMPL needs
  the v1.1.0 **Python .pkl** zip (not Julia .npz); SMPL-X needs the v1.1 830MB bundle (118MB Julia
  npz lacks the `hands_mean` keys `smplx_lite` reads).
- GVHMR tracks CG mannequins (e.g. Unity Robot Kyle) fine — crop UI/scrubber bars off the video
  first so they don't confuse YOLO/pose.

## Image-to-3D Generation (experiments)
Tested single-image → textured 3D mesh for generating game assets (e.g. character figures).
Both models cloned outside the Unity project; both run in **WSL Ubuntu** (CUDA 12.x toolkit at
`/usr/local/cuda`), driven from Git Bash via `MSYS_NO_PATHCONV=1 wsl -d Ubuntu -- ...`.

### Verdict
- **Hunyuan3D 2.1 wins** for completeness (`E:\repo\Hunyuan3D-2.1`). Its multiview-diffusion →
  reconstruct pipeline fills in the back/occluded regions; geometry comes out solid and complete.
- **TRELLIS.2** (`E:\repo\TRELLIS.2`, Microsoft, WSL venv `/home/agus/venvs/trellis2-cu124`) gave
  **incomplete/holey** geometry on a complex figure (single-view conditioning doesn't invent the
  hidden sides). `run_trellis.py <img> --pipeline-type {512,1024,1024_cascade,1536_cascade}`.
- **Quality ceiling (important):** AI single-image 3D produces *smooth, soft* results. Intricate
  hard-surface subjects (mechanical armor, chains, fine detail) lose crispness — edges round off,
  detail becomes a painterly texture. No local setting fixes this; it needs real multi-view photos
  or an artist pass. Practical band: usable but not product-photo crisp.

### Hunyuan3D 2.1 — how to run (WSL venv `/home/agus/venvs/hunyuan3d`, Python 3.10, torch 2.5.1+cu124)
- **Shape (untextured):** `run_shape.py <img> --output out.glb --octree 256`
- **Texture (PBR):** `run_tex_wrap.sh <mesh.glb> <img> --output out.glb --views 6 --resolution 768`
  (the wrapper raises `ulimit -n`, sets `HY3DGEN_MODELS`/`HF_HOME`/offline/`CUDA_HOME`).
- Weights load locally via `HY3DGEN_MODELS=/mnt/e/repo/ai_models/hy3dgen` (dit) and
  `HF_HOME=/mnt/e/repo/ai_models/huggingface` (paintpbr + dinov2-giant). **`HY3DGEN_MODELS` now
  persisted as a Windows User env var → E:\** (it defaults to `~/.cache/hy3dgen` on C: otherwise).
- **`run_shape.py` (WSL venv) is the preferred entrypoint.** There is also a Blender-MCP path
  (`api_server.py` on :8081, mode `LOCAL_API`, driven by `generate_hunyuan3d_model`) that imports
  straight into Blender — but it's **image-to-3D only** (rejects text-only) and needed patches:
  run it with ComfyUI's embedded python (has torch+hy3dshape; `pip install uvicorn fastapi`; its
  `python._pth` ignores PYTHONPATH so launch via a `sys.path`-injecting wrapper), and in
  `model_worker.py` wrap the paint-pipeline init in try/except → geometry-only (skips the
  realesrgan/custom-rasterizer build) and guard the `gradio_cache` cleanup against the in-use log.

### Gotchas / fixes (all real, hit during setup)
- **WSL can't download big HF files** (stalls at 0 bytes on the 7.4GB+ checkpoints). Fix: download
  on the **Windows** side (kimodo venv python + HF token at `E:\repo\ai_models\huggingface\token`),
  then point WSL at it via `HY3DGEN_MODELS`/`HF_HOME`. Small files (config, u2net) download fine.
- **768 texture faults at ~22.5GB on the 24GB 4090** (`CUDA driver error: device not ready`) — VRAM
  ceiling. `enable_model_cpu_offload()` breaks the custom pipeline (device mismatch). **Fix: mmgp**
  (`pip install mmgp`) — set `HY3D_MMGP=1`; patched into `hy3dpaint/utils/multiview_utils.py`
  (`offload.profile(models, profile_no=2)`). Drops VRAM to ~10GB, 768 runs. Slower (CPU↔GPU shuttle).
- **`bpy` has no Python-3.10 wheel** → patched `convert_obj_to_glb` in
  `hy3dpaint/DifferentiableRenderer/mesh_utils.py` to use trimesh (`file_type="obj"`, `process=False`
  — don't weld, it breaks UV seams).
- **Texture pipeline decimates mesh to 40k faces** (PS1-level faceting). Raised `target_count` in
  `hy3dpaint/utils/simplify_mesh_utils.py` to **150000** (300k hangs the xatlas UV-unwrap). Apply
  **smooth shading** after import to kill remaining faceting.
- Trimmed-deps tail to install for the texture pass: `pytorch-lightning==1.9.5`, `setuptools==75.6.0`
  (newer drops `pkg_resources`), `open3d==0.18.0`, `realesrgan`, `xatlas`, plus the built
  `custom_rasterizer` (CUDA) and `DifferentiableRenderer` (C++/pybind).
- Free the GPU (close/Solid-shade Blender) before a heavy pass — its viewport VRAM stacks with the run.

## Performance & Thermals (fanless Macs, e.g. M4 Air)
On a fanless laptop the heat comes almost entirely from an **uncapped frame rate** (the GPU
renders flat-out at 100%), NOT from this project being heavy. Levers, biggest first:
- **`PerformanceBoot.cs`** (runs via `[RuntimeInitializeOnLoadMethod]`, no scene wiring) caps to
  **60 fps** (`Application.targetFrameRate`, `vSyncCount = 0`) — the #1 fix. Lower `TargetFps` to 30
  for max battery/coolness. On `RuntimePlatform.OSXPlayer` only it also trims the active URP asset:
  `renderScale 0.85`, `msaaSampleCount 1` (MSAA off), `shadowDistance 35`. Editor + Windows build keep
  full quality (and the editor isn't mutated). To tune another platform, widen that guard.
- **Run a Player build on the Air, not the Editor** — the Editor is the real heat hog on a fanless Mac.
- **Memory:** clip FBXes under `Resources/GVHMRMotions/` are exported **armature-only** (no SMPL-X body
  mesh) via `gvhmr_blender_fbx.py ... nomesh` — clips don't need the mesh. Resources loads on demand,
  so the (menu-hidden) Kimodo loco clips cost **build size only, not RAM/heat**; moving them out of
  `Resources/` is a build-size win but path-risky (crawl clips are path-referenced in IKTestSetup +
  baked into the controller) — do it with the editor open to verify GUID refs hold.

## Character switching (in-game menu)
`CharacterSwitcher.cs` (on the Player) does a runtime Humanoid model swap, listed under the menu's
**Character ▸** page. The original mannequin (built by IKTestSetup, with homebrew foot IK) is
hidden/shown rather than destroyed so it keeps its IK; other characters are instantiated on demand and
animate through the shared controller (no homebrew foot IK on those — editor-time/private-field setup).
After a swap it calls `SimpleCharacter.RefreshAnimator()` + `GameMenu.RefreshAnimator()` to re-bind.
Entries are seeded in IKTestSetup (Mannequin + DoubleL `Armature (1).prefab`, which is a confirmed
Humanoid with avatar + skinned mesh; RPG-Character was rejected — its prefab has no skinned mesh/avatar).
A new playable character must be a **Humanoid** prefab (avatar + skinned mesh); AI-generated static
meshes (Hunyuan3D) need rigging first (Mixamo auto-rigs a clean A-pose well).
