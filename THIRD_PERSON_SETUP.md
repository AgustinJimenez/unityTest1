# Third Person Controller Setup Guide

This guide will help you set up the third-person character controller in your Unity scene.

## What's Included

- **ThirdPersonController.cs**: Handles character movement, jumping, sprinting, and ground detection
- **ThirdPersonCamera.cs**: Smooth third-person camera with mouse/gamepad look, collision detection, and zoom
- **Input Actions**: Pre-configured for keyboard/mouse and gamepad

## Quick Setup (5 minutes)

### 1. Create the Player Character

1. In Unity Editor, go to **Hierarchy** → Right-click → **3D Object → Capsule**
2. Rename it to "Player"
3. Set Transform Position to `(0, 1, 0)` (so it starts above the ground)
4. **Add Components** to the Player:
   - Click **Add Component** → Search for **"Character Controller"**
   - Click **Add Component** → Search for **"Player Input"**
   - Click **Add Component** → Search for **"Third Person Controller"** (your script)

### 2. Configure Character Controller

Select the Player GameObject and configure the Character Controller:
- **Height**: 2
- **Radius**: 0.5
- **Center**: (0, 0, 0)

### 3. Configure Player Input

Select the Player GameObject and configure the Player Input component:
- **Actions**: Click the circle → Select **"InputSystem_Actions"**
- **Default Map**: Select **"Player"**
- **Behavior**: Change to **"Invoke Unity Events"** (or leave as "Send Messages" - both work)

### 4. Setup the Camera

1. Select the **Main Camera** in the Hierarchy
2. Click **Add Component** → Search for **"Third Person Camera"**
3. In the Third Person Camera component:
   - **Target**: Drag the **Player** GameObject into this field
   - **Target Offset**: Set to `(0, 1.5, 0)` (camera looks at player's head)
   - **Distance**: 5 (adjust to preference)

### 5. Create a Ground Plane

1. **Hierarchy** → Right-click → **3D Object → Plane**
2. Rename it to "Ground"
3. Scale it up: Set Transform Scale to `(5, 1, 5)`
4. Position: `(0, 0, 0)`

### 6. Test It!

Press **Play** in Unity Editor and test:
- **WASD** or **Arrow Keys**: Move
- **Mouse**: Look around
- **Space**: Jump
- **Left Shift**: Sprint

## Controls

### Keyboard & Mouse
- **W/A/S/D** or **Arrow Keys**: Move
- **Mouse**: Look around
- **Space**: Jump
- **Left Shift**: Sprint

### Gamepad
- **Left Stick**: Move
- **Right Stick**: Look around
- **Button South (A/Cross)**: Jump
- **Left Stick Press**: Sprint

## Customization

### Movement Settings

Select the **Player** GameObject and adjust the Third Person Controller:
- **Walk Speed**: 4 (default)
- **Sprint Speed**: 8 (default)
- **Rotation Speed**: 10 (how fast character turns)
- **Acceleration**: 10 (how quickly speed changes)

### Jump Settings

- **Jump Height**: 2 (default)
- **Gravity**: -15 (default)

### Camera Settings

Select the **Main Camera** and adjust Third Person Camera:
- **Distance**: 5 (camera distance from player)
- **Min/Max Distance**: 2 to 10 (zoom limits)
- **Mouse Sensitivity**: 2 (mouse look speed)
- **Gamepad Sensitivity**: 100 (gamepad look speed)
- **Min/Max Vertical Angle**: -40° to 70° (vertical look limits)

## Advanced: Building a Test Level

### Add Obstacles

1. Create cubes: **Hierarchy → 3D Object → Cube**
2. Scale and position them as obstacles
3. The player will collide with them automatically

### Add Platforms

1. Create more planes or cubes
2. Position them at different heights
3. Test jumping between platforms

### Adjust Ground Layer (Optional)

If you want the character to only detect specific objects as ground:
1. Create a new Layer called "Ground"
2. Assign the Ground plane to this layer
3. On the **Player's Third Person Controller**:
   - Set **Ground Mask** to only include the "Ground" layer

## Troubleshooting

### Player falls through ground
- Make sure the Ground has a **Collider** component (Plane includes Box Collider by default)
- Check that Character Controller's center is at (0, 0, 0)

### Camera not following player
- Ensure **Target** field in Third Person Camera is assigned to the Player
- Check that the script is on the Main Camera

### Input not working
- Verify **Player Input** component has **InputSystem_Actions** assigned
- Make sure **Default Map** is set to "Player"
- Try changing **Behavior** to "Send Messages" if events don't work

### Character doesn't rotate
- The character rotates to face movement direction automatically
- If camera isn't set up, character might not know which direction to move

### Mouse stuck on screen
- The camera script locks the cursor automatically in Play Mode
- Press **Escape** to unlock cursor while testing

## Next Steps

Now that you have a working third-person controller, you can:
- Import a 3D character model to replace the capsule
- Add animations (idle, walk, run, jump)
- Create obstacles and level geometry
- Add enemies or NPCs
- Implement combat or interaction systems

## File Locations

- **Scripts**: `Assets/Scripts/`
  - `ThirdPersonController.cs`
  - `ThirdPersonCamera.cs`
- **Input Actions**: `Assets/InputSystem_Actions.inputactions`
- **This Guide**: `THIRD_PERSON_SETUP.md`

Enjoy building your third-person game! 🎮
