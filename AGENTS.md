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
