# Current Task — HomebrewIK Foot IK Integration

## Goal
Get `csHomebrewIK` working correctly on the character in `FootIK_Test.unity`.
The character should have feet that adapt to uneven terrain: lift off the ground
correctly, rotate to match surface normals, and not clip or float.

---

## Project Layout

| File | Role |
|---|---|
| `Assets/FootIK_Test.unity` | Test scene. Never saved manually — always rebuilt by the setup script. |
| `Assets/Scripts/Editor/IKTestSetup.cs` | Editor tool (`Tools > Setup`) that rebuilds the scene from scratch. |
| `Assets/Scripts/FootIK.cs` | Our own IK implementation (simpler, custom). Currently not used in setup. |
| `Assets/Scripts/SimpleCharacter.cs` | WASD movement + jump via CharacterController. |
| `Assets/Scripts/FollowCamera.cs` | Third-person follow camera. |
| `Assets/Scripts/IKDebugMenu.cs` | Runtime overlay with IK diagnostics. |
| `Assets/HomebrewIK/csHomebrewIK.cs` | The HomebrewIK library component. |
| `Assets/HomebrewIK/Editor/editorHomebrewIK.cs` | Custom inspector for HomebrewIK. |
| `Assets/FootIK_Demo.controller` | Auto-generated animator controller (Idle→Run blend tree). |

---

## How the Setup Script Works (`IKTestSetup.cs`)

`Tools > Setup` (or auto-runs on compile if `AutoRun` pref is set):

1. Force-reimports and opens `FootIK_Test.unity`
2. Removes existing "Player" GameObject and stray components
3. Instantiates `Assets/HomebrewIK/Demo/Models/Armature_Idle.prefab` as "Player"
4. Assigns URP materials (prefab defaults to Built-in RP → pink in URP)
5. Builds `FootIK_Demo.controller` with Idle→Run blend tree, IK Pass enabled
6. Adds `csHomebrewIK` to the animator's GameObject with tuned values
7. Sizes `CharacterController` from mesh bounds with `SoleBias = 0.09f`
8. Adds `SimpleCharacter`, `IKDebugMenu`, `FollowCamera`
9. Wires camera → player and player → camera
10. Saves scene

---

## HomebrewIK — How It Works (from official docs)

### Required setup
- Avatar must be **Humanoid** rig
- Script must be on the **same GameObject as the Animator**
- **IK Pass** must be enabled on the animator base layer
- **Ground Layers** must be set to the terrain layers only (exclude character's own layer)
- **Avatar** must be assigned in the Animator component

### Key parameters

| Property | What it does | Our value | Default |
|---|---|---|---|
| `raySphereRadius` | Ankle height above ground (sphere radius = ankle height by design) | 0.05 | 0.05 |
| `ankleHeightOffset` | Extra offset above raySphereRadius for ankle | **0.045f** ✓ verified | 0 |
| `lengthFromHeelToToes` | Used for height correction when foot tilts on slope | **not set → 0.1f** | 0.1 |
| `rayCastRange` | Total downward cast distance | 1.5 | 1.0 |
| `groundLayers` | Layers to hit | ~Character layer | Default |
| `enableBodyPositioning` | Hips follow lower foot | true | true |
| `enableFootLifting` | IK target follows animation when foot is above raycast hit | true | true |
| `enableIKPositioning` | Apply position IK | true | true |
| `enableIKRotating` | Apply rotation IK | true | true |
| `globalWeight` | Master IK blend weight | 1.0 | 1.0 |
| `crouchRange` | Max downward body adjustment | **0.5f** | 0.25 |
| `smoothTime` | Lerp smoothing speed | not set → 0.075 | 0.075 |
| `floorRange` | Tolerance above ground still treated as grounded | not set → 0 | 0 |
| `leftFootRayStartHeight` | Ray start height above left ankle | not set → 0.5 | 0.5 |
| `rightFootRayStartHeight` | Ray start height above right ankle | not set → 0.5 | 0.5 |

### How IK positioning works (doc)
- Only **Y axis** is corrected. X/Z always follow animation.
- This is intentional — correcting all axes would make feet lag horizontally.
- `GetIKPosition()` is called inside `OnAnimatorIK()` (not Update) because
  position must be fetched after animation curves are applied.
- The foot's IK position is fetched using `GetIKPosition()`, then only Y is overridden.

### How IK rotation works (doc)
- `FromToRotation(transform.up, normalBuffer) * GetIKRotation(goal)`
- Gets the **delta** rotation caused by the surface normal, applies it on top of
  the animation's rotation. Does not set an absolute rotation.
- Rotation gets reset every frame just like position, so getting+setting the same
  value in a loop does not accumulate.

### How foot lifting works (doc)
- If foot's animated position is **higher** than ray hit position → IK target is
  set to the animated bone position (foot follows animation freely).
- If foot's animated position is **lower** → IK target is set to ray hit position.
- Rotation target is **always** applied to prevent ankle twist.
- `floorRange` adds tolerance: values slightly above ray hit still treated as grounded.

### Body positioning (doc)
- Moves `animator.bodyPosition` to follow the lower of the two feet.
- `crouchRange`: max downward shift. `stretchRange`: max upward shift.
- Doc warns: "not robust against all kinds of environments" — may need custom
  implementation for games using rigidbodies/colliders on the player.

---

## Known Issues / Discrepancies Found

### 1. `lengthFromHeelToToes` not explicitly set
- We leave it at the default `0.1f`.
- This value is used to calculate height correction when the foot is tilted on a slope.
- If `0.1f` doesn't match the Armature_Idle mesh's actual foot length, toes will
  clip underground on slopes.
- **To fix:** Measure the foot mesh in Unity (distance from ankle bone to toe tip)
  and set this value explicitly in the setup script.

### 2. `crouchRange = 0.5f` — higher than default
- Default is `0.25f`. We set `0.5f`.
- May cause the character to visibly squat too much on uneven terrain.
- **To investigate:** Try `0.25f` and compare.

### 3. Foot IK per-state checkbox not enabled
- Doc section D: "Foot IK option inside each animator state blocks" makes
  `GetIKPosition()` return the same value as the bone transform position.
- Without it, the ankle may not match the animation's original bone positions
  exactly (floating/clipping on retargeted avatars).
- Our setup enables IK Pass (layer level) but not Foot IK per state.
- **To investigate:** Enable "Foot IK" checkbox on the Locomotion blend tree state
  in the generated controller.

### 4. `SoleBias = 0.09f` in CharacterController sizing
- We sink the character root 9cm below the foot mesh to align the capsule bottom
  with the feet.
- This interacts with HomebrewIK's ankle height system (raySphereRadius = 0.05).
- Could cause the raycast to start too low or the feet to sit too high/low.
- **To investigate:** Check if character floats or clips when standing still.

### 5. Our `FootIK.cs` vs `csHomebrewIK`
- We have two IK implementations in the project:
  - `FootIK.cs` — our own, simpler, uses Raycast (not SphereCast), only corrects Y
  - `csHomebrewIK.cs` — HomebrewIK library, uses SphereCast, has more features
- Current setup uses `csHomebrewIK` only. `FootIK.cs` is not attached.

---

## What We're Trying to Solve
(to be filled in when we remember the specific visual problem from last session)

Possible issues we were investigating:
- Feet clipping through ground on slopes?
- Feet floating above ground?
- Feet dragging / not lifting during walk cycle?
- Body squatting too aggressively?

---

## Verified Values (confirmed in-game)
- `ankleHeightOffset = 0.045f` — feet clip at 0, look correct at 0.045
- `lengthFromHeelToToes = 0.203f` — manually tuned via debug menu, locked in setup
- `crouchRange` — above 0.1 makes no visible difference, not the fix for the floating foot issue
- `IKBodyLower.maxExtraLowering = 0.460f` — enough to plant foot on tall steps; body lowers proportionally to gap, so it stops as soon as foot reaches ground
- `IKBodyLower.gapThreshold = 0.410f` — lower causes foot on ramp to clip below surface

---

## Active Problem — Floating Foot on Height Transitions

**Symptom:** When standing at an edge where one foot is on a raised surface and the
other is over lower ground, the lower foot floats visibly above the flat ground.

**Diagnostics captured:**
- R gap (IK-bone): -0.2627 `^ pull` — IK needs to pull foot down 26cm, beyond leg reach
- CC bottom Y: 0.3283 — CharacterController is sitting 33cm above flat ground
- The CC is grounded on the raised surface; the whole body is elevated with it

**Root cause:** `csHomebrewIK` body positioning only moves `animator.bodyPosition`
(the skeleton), NOT the actual GameObject / CharacterController root. So even with
large `crouchRange`, the leg can't stretch far enough to reach the lower ground.

**Doc acknowledgement:**
> "Non-Universal Default Body Positioning Implementation — you may want to use
> raycasting, or even modify the parent transform's properties itself"

**Planned fix: Custom body lowering script**
- Add a new MonoBehaviour (e.g. `IKBodyLower.cs`) that runs alongside `csHomebrewIK`
- Each frame, raycast straight down from both foot positions to find ground contacts
- Compute the lowest foot's ground Y vs current root Y
- Smoothly lower the actual `transform.position` (CC root) so the lower foot can reach
- Must be careful not to fight the CharacterController's own vertical movement
  (only apply when CC is grounded, clamp the offset, use SmoothDamp)
- Disable during jump/fall

**Key constraint:** Moving `transform.position` directly while CC is active can cause
jitter. The safer approach may be to offset `CharacterController.center` (local) instead
of the root position, or to call `CC.Move()` with a downward correction each frame.

---

## Status — IK COMPLETE

Foot IK is working well for its intended scope:
- Flat ground, slopes, ramps ✓
- Small steps (csHomebrewIK crouchRange handles alone) ✓
- Tall steps up to leg-reach distance (IKBodyLower body lowering) ✓

## Known Limitation — Drop Too Large

When one foot hangs over a drop larger than leg reach, the foot floats.
This is not an IK problem. Two gameplay solutions exist but are out of scope:
1. **Edge nudge** — detect hanging foot, push character back onto platform (`SimpleCharacter.cs`)
2. **Ledge hang** — detect edge, transition to hanging animation + hand IK system

---

## Useful References
- HomebrewIK docs: https://nonstop-marigold-de3.notion.site/Docs-Homebrew-Foot-IK-v1-6-6455d28e2e184f649e88a429c23047ff
- Unity IK docs: https://docs.unity3d.com/Manual/InverseKinematics.html
- Unity execution order: https://docs.unity3d.com/Manual/ExecutionOrder.html
