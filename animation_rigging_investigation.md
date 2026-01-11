# Animation Rigging Investigation

## Goal
Explore procedural animation in Unity as an alternative or complement to pre-recorded animation files from Mixamo.

## What is Procedural Animation?
Animation driven by code rather than keyframes. Instead of using animation files, we write scripts that manipulate bones and transforms in real-time based on rules and game state.

## Why Investigate This?
- **Current approach:** Using Mixamo animation files (.dae) for Idle, Walking, etc.
- **Question:** Can we define animations purely through code instead of files?
- **Benefits of procedural:**
  - Adapts to environment dynamically (feet on terrain, stairs, slopes)
  - Smaller file sizes (no animation files needed)
  - Infinite variation (not limited to pre-made animations)
  - Immediate reaction to gameplay events

## What We Installed
**Package:** Unity Animation Rigging (v1.3.1)
- Official Unity package for procedural motion
- Installed via: `Packages/manifest.json`
- Added: `"com.unity.animation.rigging": "1.3.1"`

## Approaches to Procedural Animation

### 1. Pure IK (Inverse Kinematics)
Write scripts that directly calculate bone positions using IK algorithms.
- Calculate target positions (e.g., where foot should land)
- IK solver figures out all joint angles automatically
- Full control, but complex to implement

### 2. Animation Rigging Package (Hybrid Approach)
Combine keyframe animations with procedural adjustments.
- Keep existing Mixamo animations as base
- Add IK constraints on top for dynamic adjustments
- Best of both worlds approach

### 3. Direct Bone Manipulation
Manually rotate/position bones in Update() loop.
- Simple but can look robotic
- Good for simple effects (head bob, breathing)

## What We Plan to Try

### Experiment 1: Foot IK (Recommended First)
**Goal:** Make character's feet automatically adjust to terrain height while walking.

**How it works:**
1. Add "Rig Builder" component to character
2. Create rig with "Two Bone IK" constraints for each leg
3. Script moves IK targets based on ground detection (raycasting)
4. Feet automatically plant on uneven ground, stairs, slopes

**Expected result:** Character walks naturally on uneven terrain without feet clipping through ground or floating.

### Experiment 2: Look At Constraint
**Goal:** Make character's head rotate to look at targets (camera, enemies, objects).

**How it works:**
1. Add "Multi-Aim Constraint" to head bone
2. Set target object to follow (camera, enemy, etc.)
3. Head automatically rotates to face target while walking

**Expected result:** More lifelike character that appears aware of surroundings.

### Experiment 3: Hand IK / Reach
**Goal:** Character procedurally reaches for objects.

**How it works:**
1. Add "Two Bone IK" constraints for arms
2. When player presses interaction button, set IK target to object position
3. Arm automatically positions to reach object

**Expected result:** Natural reaching animation without pre-made animation file.

### Experiment 4: Pure Procedural Walking (Advanced)
**Goal:** Replace Mixamo walking animation entirely with code-driven walking.

**How it works:**
1. Detect when foot needs to step (distance threshold)
2. Raycast forward to find ground landing position
3. Move foot to target using IK with smooth interpolation
4. Alternate between left and right feet
5. Adjust stride length based on movement speed

**Expected result:** Fully procedural walking that adapts to any speed or terrain without animation files.

**Challenge:** This is complex and may look robotic compared to hand-crafted Mixamo animations.

## Current Status
- ✅ Animation Rigging package installed
- ⏳ Waiting for Unity Editor to import package
- 📋 Ready to start experiments

## Resources
- [Unity Animation Rigging Documentation](https://docs.unity3d.com/Packages/com.unity.animation.rigging@1.0/manual/index.html)
- [Unity Learn: Working with Animation Rigging](https://learn.unity.com/tutorial/working-with-animation-rigging)
- [Alan Zucconi's Procedural Animation Tutorial](https://www.alanzucconi.com/2017/04/17/procedural-animations/)
- [Unity IK Manual](https://docs.unity3d.com/Manual/InverseKinematics.html)
- [GitHub: Unity Procedural Animation Examples](https://github.com/Sopiro/Unity-Procedural-Animation)
- [GitHub: IK Wall-Walking Spider](https://github.com/PhilS94/Unity-Procedural-IK-Wall-Walking-Spider)
- [Medium: Creating Procedural Animations in Unity/C#](https://medium.com/codex/creating-procedural-animations-in-unity-c-8c5c2394739d)

## Notes
- Animation Rigging works alongside existing animations
- We can keep Mixamo animations and add procedural polish
- Most AAA games use hybrid approach: artist-made base + procedural adjustments
- Start simple (Foot IK) before attempting full procedural walking

## Next Steps
1. Wait for package import in Unity Editor
2. Start with Experiment 1: Foot IK
3. Document results and learnings
4. Decide if procedural approach fits our needs vs. pre-made animations
