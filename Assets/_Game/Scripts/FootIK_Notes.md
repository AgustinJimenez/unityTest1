# FootIK — How It Works

## The Problem

A character walking over uneven terrain (ramps, stairs, cobblestones) looks wrong if its feet clip through the surface or float above it. Animation clips are baked on flat ground, so the foot height is always relative to a flat floor. When the real floor is higher or lower, the mismatch is visible.

**Foot IK** corrects foot placement at runtime by bending the leg joints so the foot lands exactly on the surface beneath it.

---

## Unity's IK System

Unity provides `OnAnimatorIK(int layerIndex)` — a callback fired **after the animation clip is sampled but before IK is solved**. Inside it you can set IK goals:

```csharp
animator.SetIKPositionWeight(AvatarIKGoal.LeftFoot, 1f);
animator.SetIKPosition(AvatarIKGoal.LeftFoot, targetWorldPos);
```

Unity's two-bone IK solver then bends the upper leg → knee → foot chain so the foot reaches `targetWorldPos`. The weight parameter (0–1) blends between the pure animation pose and the IK-corrected pose.

**Requirements:**
- The Animator must be **Humanoid** rig type.
- The base layer must have **IK Pass** checked in the Animator window.
  `FootIK.cs` enables this automatically from `Awake()`.

---

## Getting the Foot Position

Inside `OnAnimatorIK`, we need to know where the foot is *this frame in the animation* — i.e. where would the foot be if we applied no IK correction?

**Use `GetBoneTransform`, not `GetIKPosition`:**

```csharp
Transform footBone = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
Vector3 animatedPos = footBone.position; // world pos after clip sampling, before IK
```

`animator.GetIKPosition(goal)` returns the *IK goal position* (what you set last frame), not the raw animation position. Using it as a raycast origin would accumulate drift.

---

## The Raycast

We cast a ray **straight down** from slightly above the foot bone:

```
origin = footBone.position + Vector3.up * rayOriginHeight
direction = Vector3.down
distance = rayOriginHeight + rayDistance
```

Starting above the foot handles the case where the animated foot is already below or at ground level (e.g. the low point of a step animation). Total cast distance covers the maximum expected step height plus the origin offset.

**Important:** Set `groundMask` to only include actual ground layers and **exclude the character's own physics layer**. Otherwise the ray can hit the CharacterController capsule or leg colliders instead of the terrain.

---

## Swing vs Stance

Foot IK should only correct a foot when it is **in contact with the ground (stance phase)**. During the swing phase (foot moving through the air), the animation should drive the foot freely.

We detect stance/swing using the foot's height relative to the character root:

```
footLocalY = footBone.position.y - transform.position.y
inSwing    = !grounded || footLocalY > swingHeightThreshold
```

| Condition | footLocalY | Result |
|---|---|---|
| Standing still | ≈ 0 | Stance — IK on |
| Mid-step (foot near floor) | 0.05 – 0.15 | Stance — IK on |
| Swing phase (foot raised) | > swingHeightThreshold | Swing — IK off |

### Why Not Use Raycasting Alone?

If the ray hits ground, we could assume stance. But on flat terrain the ray **always** hits, even when the foot is swinging forward mid-step. This would cause the IK to fight the animation — trying to pin the foot to the ground while the clip lifts it.

The height threshold disengages IK the moment the foot is raised enough that it is clearly not in contact.

---

## The Dragging Bug — and the Fix

A naive implementation stores a smoothed IK position and lerps toward the target each frame:

```csharp
smoothPos = Vector3.Lerp(smoothPos, targetPos, dt * speed); // ← WRONG
```

**What goes wrong:**
1. Foot plants on ground at position A. `smoothPos = A`. IK weight = 1. ✓
2. Foot lifts off (swing). IK weight lerps toward 0. `smoothPos` stays at A.
3. Foot swings forward, plants at position B.
4. IK weight lerps back to 1. `smoothPos` now lerps **from A to B** — the foot visibly drags from A.

**The fix:** During swing, reset the smoothed state *instantly* to the animated bone position. No lerp.

```csharp
if (inSwing)
{
    yOffset   = 0f;          // ← instant reset, no drag on next plant
    smoothRot = bone.rotation;
    weight    = Mathf.Lerp(weight, 0f, dt * weightSmoothing);
}
```

When the foot plants again, it lerps *from the animated position* — which is already at the new plant location — so there is no lag.

---

## Only Correct Y

The IK target is **not** a smoothed 3D position. Only the Y component is corrected:

```csharp
float targetOffset = (rayHit.point.y + footHeightOffset) - bone.position.y;
yOffset = Mathf.Lerp(yOffset, targetOffset, dt * ySmoothing);

Vector3 ikPos = bone.position + Vector3.up * yOffset; // X/Z from animation
```

This lets the foot move freely along the ground plane while the IK only lifts or lowers it to match the surface height. If we smoothed all three axes, the foot would lag horizontally behind the walk cycle — the "barely moving" symptom.

---

## Body (Hips) Adjustment

On steep terrain, if one foot needs to reach significantly higher than the other, one leg may need to over-extend. We compensate by dropping the hips slightly:

```csharp
float drop = -Mathf.Abs(leftContact.y - rightContact.y) * 0.5f;
animator.bodyPosition += Vector3.up * smoothedDrop;
```

This is done inside `OnAnimatorIK` via `animator.bodyPosition`, which offsets the entire skeleton. Dropping by **half the height difference** ensures neither leg needs to stretch beyond its natural range.

---

## Foot Rotation

The foot is rotated to lie flat on the surface:

```csharp
Vector3 fwd = Vector3.ProjectOnPlane(transform.forward, hit.normal);
Quaternion target = Quaternion.LookRotation(fwd, hit.normal);
smoothRot = Quaternion.Slerp(smoothRot, target, dt * rotationSmoothing);
```

We project the character's forward vector onto the hit plane so the foot points in the correct direction even on sloped surfaces, then construct a rotation that aligns the foot's up axis with the surface normal.

---

## Parameter Tuning Guide

| Parameter | Effect | Typical range |
|---|---|---|
| `rayOriginHeight` | Raise if foot clips ground before ray fires | 0.4 – 0.8 |
| `rayDistance` | Must exceed max step height + origin height | 1.0 – 2.0 |
| `footHeightOffset` | Prevents foot from clipping surface | 0.02 – 0.06 |
| `swingHeightThreshold` | Raise for taller steps; lower for snappier IK | 0.15 – 0.35 |
| `weightSmoothing` | IK blend speed. Too low = delayed activation | 8 – 15 |
| `ySmoothing` | Y correction speed. Too low = floaty | 10 – 20 |
| `rotationSmoothing` | Foot tilt speed. Too high = jittery on rough surfaces | 6 – 12 |
| `bodySmoothing` | Hip drop speed | 6 – 12 |
| `maxBodyDrop` | Clamp on hip descent | 0.2 – 0.5 |

---

## Known Limitations

- **Horizontal IK only lifts/lowers feet** — the foot cannot be shifted left/right or forward/back by this system. If a stair edge is narrow, the foot will appear to stand partly off it.
- **No plant locking** — during slow movement or idle, the foot can drift slightly as the animation shifts the bone. A full plant-and-lift system (storing a world-space contact position per foot and only releasing it when the foot lifts) would eliminate this, at the cost of more state.
- **Requires Humanoid rig** — `GetBoneTransform` only works for humanoid avatars.
- **Ground layer setup** — if the character's own colliders are on the ground mask, the ray will hit them. Put the character on a dedicated physics layer.

---

## Files

| File | Purpose |
|---|---|
| `FootIK.cs` | Runtime component — attach to the same GameObject as the Animator |
| `FootIK_Notes.md` | This document |
| `Editor/ThirdPersonSetup/Character.cs` | Auto-attaches `FootIK` via `Tools > Third Person > Complete Setup` |
