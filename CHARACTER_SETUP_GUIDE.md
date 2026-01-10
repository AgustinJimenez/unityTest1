# Character Setup Guide

## Current Status
You've successfully imported Leonard from Mixamo, but you're seeing:
- ✅ Character is loaded
- ❌ White mesh (no materials/textures)
- ❌ T-pose (no animations)

Both are normal and easy to fix!

---

## Solution 1: Configure FBX Import Settings (Do This First!)

### Step 1: Set Rig to Humanoid
1. In Unity Project window, navigate to **Assets/Characters/**
2. Click on **leonard_character.fbx**
3. In Inspector, click the **"Rig"** tab
4. Change **Animation Type** to **"Humanoid"**
5. Click **"Apply"** at the bottom
6. Wait for Unity to process

### Step 2: Re-run Character Swap
- Go to **Tools → Third Person → Replace Capsule with Character** again
- This will properly configure the Animator

---

## Solution 2: Download Animations from Mixamo

The T-pose issue is because we need animations! Here's how:

### Download an Idle Animation:

1. Go to **[Mixamo.com](https://www.mixamo.com/)**
2. Sign in with your Adobe account
3. Make sure **Leonard** is selected (or select him again)
4. Click the **"Animations"** tab
5. Search for **"Idle"**
6. Click on an idle animation you like
7. Click **"Download"** button
8. Settings:
   - Format: **FBX for Unity**
   - Skin: **Without Skin** (we already have the character)
   - Frame Rate: **30 fps**
   - Keyframe Reduction: **Uniform** (default is fine)
9. Save to your `Assets/Characters/` folder (name it something like `leonard_idle.fbx`)

### Import the Animation in Unity:

1. Unity will auto-import the animation FBX
2. Select **Player → CharacterModel** in the Hierarchy
3. In Inspector, find the **Animator** component
4. Create an **Animator Controller**:
   - Right-click in Project → **Create → Animator Controller**
   - Name it "LeonardController"
   - Drag it into the **Animator → Controller** field on CharacterModel
5. Double-click the **LeonardController** to open Animator window
6. Drag your **leonard_idle** animation into the Animator window
7. Right-click the idle state → **Set as Layer Default State** (orange)
8. Press **Play** - Leonard should now be animated!

---

## Solution 3: Fix White Materials (Optional)

The white mesh is because Mixamo doesn't include textures. You have options:

### Option A: Create Simple Material (Quick)
1. In Project window, right-click → **Create → Material**
2. Name it "LeonardMaterial"
3. Change color to something you like (brown, tan, etc.)
4. Drag the material onto Leonard in the Scene view

### Option B: Download Textured Version (Better)
Mixamo characters can have textures, but you might need to:
1. Try a different character that includes textures
2. Or create your own materials in Unity
3. Or leave it white for prototyping (works fine!)

---

## Quick Start (Minimal Setup)

If you just want to test movement without animations:

1. **Configure FBX as Humanoid** (Rig tab → Animation Type → Humanoid → Apply)
2. **Re-run the character swap**
3. **Add a simple material** for color
4. **Test movement** - It'll be in T-pose but movement will work!

Later, download animations from Mixamo to make it look better.

---

## Recommended Workflow

1. ✅ Import character FBX (Done!)
2. ✅ Set Rig to Humanoid
3. ✅ Swap capsule with character (Done!)
4. ⬜ Download 3-4 animations from Mixamo:
   - Idle
   - Walking
   - Running
   - Jump
5. ⬜ Set up Animator Controller with animations
6. ⬜ Connect animations to movement states

This gives you a fully animated third-person character!

---

## Need Help?

- **Mixamo Website**: https://www.mixamo.com/
- **Unity Animator Tutorial**: Window → Animation → Animator (to open Animator window)
- Check Unity's documentation on Animator Controllers for more advanced setups

---

## What's Working Now

Even in T-pose:
- ✅ Movement (WASD)
- ✅ Camera (Mouse look)
- ✅ Jumping (Space)
- ✅ Sprinting (Shift)

The T-pose is just visual - everything else works!
