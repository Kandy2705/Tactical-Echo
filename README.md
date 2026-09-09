# Tactical Echo

Tactical Echo is a 3D third-person shooter technical showcase built for the Kong Studios Round 2 review. The project prioritizes technical depth, clear ownership boundaries, debugging visibility, and maintainable code over visual polish.

## Current foundation

- Unity 6000.3.11f1
- Universal Render Pipeline 17.3.0
- Input System 1.19.0
- AI Navigation 2.0.11
- TextMeshPro through Unity UI
- PrimeTween is installed under `Assets/Plugins/PrimeTween` and is reserved for presentation-only tweening.

## Project-owned code

All personal gameplay code lives under `Assets/_Project`.

```text
Assets/_Project/
  Core/
  Character/
  AI/
  Combat/
  Inventory/
  Camera/
  Animation/
  SaveLoad/
  DebugTools/
  Optimization/
  UI/
  Input/
  Scenes/
  Settings/
```

## Third-party assets

Third-party art/audio/VFX packages are isolated under `Assets/_ThirdParty`. PrimeTween remains under `Assets/Plugins` because it is a plugin.

```text
Assets/_ThirdParty/
  Animations/Kevin Iglesias/
  Audio/The_Sound_Guild_FREE_PACK_Footsteps_Volume_02/
  Environment/Tree_Packs/
  Environment/Viking Village/
  VFX/JMO Assets/
  Weapons/Gece Studio/
```

## Core dependency rule

```text
Sensors -> Memory -> EnemyBrain -> State Machine -> Tactical Evaluator
                                                -> Tactical Action
                                                -> Movement / Weapon / Animation

PlayerInput -> PlayerController -> Weapon / Camera / Animation

SaveManager <-> ISaveParticipant
Debug tools observe systems but do not own gameplay decisions.
```

## Scope order

1. Player movement + baseline camera
2. Rifle hitscan, ammo, reload, damage, noise
3. Vision + hearing
4. Memory without position cheating
5. Patrol / Investigate / Combat / Search / Retreat states
6. Tactical scoring and cover selection
7. Advanced animation and camera
8. Status effects
9. Inventory/equipment
10. Versioned save/load
11. Debug tooling and profiler-driven optimization

The initial classes are intentionally a foundation. Gameplay-specific transitions, cover scoring, animation synchronization, recoil/spread, save migration and profiler-driven optimizations are implemented incrementally after the architecture is stable.
