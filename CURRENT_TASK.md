# Current Task — Ledge Hang System

## Goal
When the character walks to an edge and one foot hangs over a drop larger than leg reach,
detect the ledge and transition to a hanging animation with procedural hand IK placement
on the ledge surface.

---

## Approach

**Unity Animation Rigging + custom edge detection**
- Use `com.unity.animation.rigging` (free, built-in) for hand IK constraints
- Write edge detection to find ledge contact points
- Blend rig weight when entering/exiting hang state
- Reference: [Traverser](https://github.com/AitorSimona/Traverser) — open-source full hang system using Animation Rigging internally; use as implementation reference

Why Animation Rigging over alternatives:
- Free, ships with Unity, URP-compatible
- `TwoBoneIK` constraints on arms work alongside HomebrewIK's `OnAnimatorIK` — no conflicts (different systems: Rig Builder vs OnAnimatorIK callback)
- Traverser shows the full wiring: edge detection → state → IK target placement

---

## How Animation Rigging Works (for this use case)

### Setup
1. Add `Rig Builder` component to the Animator GameObject
2. Create a child `Rig` GameObject with a `Rig` component — add it to the Rig Builder's rig list
3. Under the Rig, create two `TwoBoneIK` constraints (one per arm):
   - **Root** — upper arm bone
   - **Mid** — forearm bone
   - **Tip** — hand bone
   - **Target** — an empty Transform (world-space position the hand moves to)
   - **Hint** — an empty Transform (elbow direction hint)
4. Move the Target transforms to detected ledge contact points at runtime
5. Blend `Rig.weight` (0 = animation only, 1 = full IK)

### Runtime IK blend
```csharp
rig.weight = Mathf.Lerp(rig.weight, targetWeight, Time.deltaTime / blendTime);
```

---

## Ledge Detection Plan

1. **Detect edge ahead**: SphereCast / BoxCast forward from chest height
2. **Confirm drop**: Raycast down past the ledge to confirm significant drop below
3. **Find ledge top**: Raycast from above to find the exact ledge surface point
4. **Hand placement**: Place left/right hand IK targets on the ledge surface,
   offset horizontally to shoulder width
5. **State gate**: Only activate when:
   - Character is grounded (CC grounded on raised platform)
   - Moving toward edge (velocity dot forward > threshold)
   - Drop below ledge > minimum hang threshold (e.g. > 1.0 m)

---

## Files to Create / Modify

| File | Change |
|---|---|
| `Assets/Scripts/LedgeDetector.cs` | New — edge detection, hand target placement |
| `Assets/Scripts/SimpleCharacter.cs` | Add hang state, freeze movement while hanging |
| `Assets/Scripts/Editor/IKTestSetup.cs` | Add Animation Rigging setup to scene builder |
| `Packages/manifest.json` | Add `com.unity.animation.rigging` if not present |

---

## Animation Rigging Package
- Package ID: `com.unity.animation.rigging`
- Version: check Package Manager for latest stable (1.3.x as of 2025)
- Docs: https://docs.unity3d.com/Packages/com.unity.animation.rigging@1.3/manual/index.html
- TwoBoneIK constraint: https://docs.unity3d.com/Packages/com.unity.animation.rigging@1.3/manual/constraints/TwoBoneIKConstraint.html

## Reference Implementation
- Traverser (open source): https://github.com/AitorSimona/Traverser
- Project page: https://aitorsimona.github.io/Traverser/

---

## Known Constraints / Risks

- **Animation Rigging + Animator**: Rig Builder runs after the Animator update. HomebrewIK runs in `OnAnimatorIK`. Order: Animator → OnAnimatorIK (HomebrewIK + IKBodyLower) → Rig Builder (Animation Rigging). This is correct — hand IK applies after body pose is settled.
- **No hang animation yet**: We'd need a hang idle clip. Options: use Armature_Idle FBX package's hang clip if present, or source from Mixamo.
- **SimpleCharacter conflict**: Current WASD movement continues while any state is active. Need a `hanging` bool to block movement input and gravity while hanging.
- **IKBodyLower interaction**: When hanging, feet are off ground — IKBodyLower should be suppressed (`cc.isGrounded` will be false, so it already gates off naturally).

---

## Previous Task — Foot IK (COMPLETE)

Foot IK is working for: flat ground, slopes, ramps, small steps, tall steps up to leg reach.
See git log for implementation history. Key scripts:
- `Assets/HomebrewIK/csHomebrewIK.cs` — third-party foot IK library
- `Assets/Scripts/IKBodyLower.cs` — custom body lowering for tall steps
- `Assets/Scripts/Editor/IKTestSetup.cs` — scene setup script (`Tools > Setup`)

The ledge hang system is the next natural step: when the drop is too large for IKBodyLower to
bridge, instead of the foot floating, the character grabs the ledge.

---

## Useful References
- Animation Rigging docs: https://docs.unity3d.com/Packages/com.unity.animation.rigging@1.3/manual/index.html
- Traverser source: https://github.com/AitorSimona/Traverser
- HomebrewIK docs: https://nonstop-marigold-de3.notion.site/Docs-Homebrew-Foot-IK-v1-6-6455d28e2e184f649e88a429c23047ff
- Unity IK docs: https://docs.unity3d.com/Manual/InverseKinematics.html
