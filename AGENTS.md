# Repository Guidelines

This repository is a Unity 6 (6000.3.3f1) project using URP and the new Input System.

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
- `Tools > Third Person > Dry Run Setup` previews what the setup would do without making changes.
- Auto-run is enabled on compile by default; toggle via `Tools > Third Person > Auto Run Setup`.

### Complete Setup Script (`ThirdPersonSetup.cs`)
Location: `Assets/Scripts/Editor/ThirdPersonSetup/`

The Complete Setup script is a multi-file partial class that automates the entire third-person character setup. It consists of:

**Files:**
- `ThirdPersonSetup.cs` - Main entry point with menu items
- `Config.cs` - Configuration constants (paths, sizes, animation names)
- `Scene.cs` - Scene object creation (Ground, Ramp, Stairs, Player, Camera)
- `Character.cs` - Character model application and material fixes
- `Animator.cs` - Animator controller setup with states and transitions
- `Sprint.cs` - Sprint animation blend tree setup
- `Report.cs` - Setup report logging and TMP settings validation
- `DryRun.cs` - Dry run mode description

**What it creates:**
1. **Ground** - 50x50 plane at origin with default material
2. **Ramp** - Angled surface for testing slope movement
3. **Stairs** - Step geometry with combined MeshCollider for foot IK testing
4. **Player** - GameObject with:
   - `CharacterController` (radius 0.3, height 1.8, center offset 0.9)
   - `ThirdPersonController` (movement logic)
   - `PlayerInput` (new Input System)
   - `CharacterModel` child with Animator and `AnimatorFootIk`
5. **Camera** - Main Camera with `ThirdPersonCamera` component

**Animator States:**
- Idle, Walk, TurnLeft, TurnRight (locomotion)
- JumpBegin, JumpLoop, JumpFall, JumpLand (jump sequence)
- Sprint (2D blend tree with directional animations)

**Parameters:**
- `Speed` (float), `Horizontal` (float), `Vertical` (float)
- `IsGrounded` (bool), `Jump` (trigger)
- `VerticalVelocity` (float), `IsSprinting` (bool)

**Character Model Priority:**
1. Kevin Iglesias Human Character Dummy (`Assets/Kevin Iglesias/Human Character Dummy/Prefabs/Human Character Dummy.prefab`)
2. Fallback: Any FBX in `Assets/Mixamo/character/`

**Note:** The Complete Setup script and MCP Unity are complementary. MCP allows Claude to make targeted modifications, while Complete Setup provides a full reset to a known working state.

## MCP Unity Integration (Claude Code)
This project has MCP Unity configured, allowing Claude Code to interact directly with the Unity Editor.

### Setup
- Package: `com.gamelovers.mcp-unity` (installed via git URL in `Packages/manifest.json`)
- Config: `.mcp.json` in project root points to the MCP server
- Server runs on port 8090 (WebSocket)

### Usage
1. Open Unity Editor and ensure the MCP server is running (`Tools > MCP Unity > Server Window`)
2. Start Claude Code from the project directory
3. Claude can now use MCP tools to:
   - Query scene hierarchy and assets
   - Create/modify GameObjects and components
   - Manage scenes (create, load, save)
   - Create and assign materials
   - Run tests
   - Send messages to Unity console
   - Execute menu items

### Troubleshooting
- If MCP times out, ensure Unity is focused/responsive and the server is running
- After restarting Unity, you may need to restart Claude Code to reconnect
- Server build location: `Library/PackageCache/com.gamelovers.mcp-unity@.../Server~/build/index.js`

## Input & Animation Notes
- Input actions live in `Assets/InputSystem_Actions.inputactions`; prefer `PlayerInput` action maps.
- For Mixamo imports, keep a consistent avatar and ensure root motion is disabled on scripted movement.

## Current Workflow & Debugging
- After code changes, let Unity recompile, wait for auto-setup to finish, then press Play.
- `ThirdPersonController` logs jump/state transitions by default; use the Console for clip names and timestamps.
- For historical logs, check the Unity Editor log (macOS: `~/Library/Logs/Unity/Editor.log`) and filter with `rg` when needed.
- When you need my attention to continue, play a TTS notification with a clear custom message.
