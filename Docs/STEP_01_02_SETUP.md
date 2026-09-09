# Tactical Echo - Step 01 + Step 02 Setup

This document records the current foundation and the exact Unity scene setup for the first playable milestone.

## Step 01 - Foundation

Validated project baseline:

- Unity 6000.3.11f1
- URP 17.3.0
- Input System 1.19.0
- AI Navigation 2.0.11
- TextMeshPro for text UI
- PrimeTween for presentation-only tweening
- Project-owned code under `Assets/_Project`
- Third-party content under `Assets/_ThirdParty`
- Main working scene: `Assets/_Project/Scenes/TacticalEcho_Sandbox.unity`

The legacy Viking Village water renderer was migrated from obsolete `RenderTargetHandle` usage to `RTHandle` so it can compile against the current URP version.

## Step 02 - Player Movement + Baseline Camera

### Player hierarchy

Create or prepare this hierarchy inside `TacticalEcho_Sandbox`:

```text
Player
├── Visual
├── CameraTarget
└── AimOrigin
```

Recommended components on `Player`:

- CharacterController
- PlayerInputReader
- PlayerController

`Visual` contains the character model. Keep the root Player transform responsible for gameplay movement and rotation.

### Main Camera

Add `PlayerCameraController` to the Main Camera.

Assign:

- Target -> `Player/CameraTarget`
- Input -> `PlayerInputReader`

Recommended starting CameraTarget local position:

```text
(0, 1.5, 0)
```

### PlayerController references

Assign:

- Input -> PlayerInputReader on Player
- Player Camera -> PlayerCameraController on Main Camera
- Camera Orientation -> Main Camera
- Aim Origin -> `Player/AimOrigin`
- Weapon -> leave empty until Step 03 if the rifle is not configured yet

### PlayerInputReader references

Open:

`Assets/_Project/Input/TacticalEcho_InputActions.inputactions`

Current imported Input Actions already contain `Player/Move`, `Player/Look` and `Player/Sprint`.

Assign:

- Move Action -> Player / Move
- Look Action -> Player / Look
- Sprint Action -> Player / Sprint
- Fire Action -> Player / Attack for now

`Aim Action` and `Reload Action` may remain empty during Step 02. They will be finalized with the shooting controls in Step 03.

### CharacterController starting values

These are starting values, not hard requirements:

```text
Center: (0, 0.9, 0)
Height: 1.8
Radius: 0.3
Step Offset: 0.3
Slope Limit: 45
```

Adjust them to match the actual character model.

## Step 02 acceptance test

Step 02 is considered done when all of these pass:

- WASD moves relative to camera direction.
- Diagonal movement is not faster than straight movement.
- Holding Sprint increases speed.
- Player stays grounded on normal terrain and gravity works when leaving an edge.
- Mouse rotates the camera around the player.
- Character rotates smoothly toward movement direction while exploring.
- No camera movement logic lives inside PlayerController.
- No input polling is duplicated across gameplay classes.
- Console has no project-owned compile errors.

Do not start tactical AI yet. The next milestone is Step 03: rifle shooting core.
