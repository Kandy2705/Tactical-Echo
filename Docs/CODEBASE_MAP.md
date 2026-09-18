# Tactical Echo Codebase Ownership Map

This document answers one question before adding a feature: **which existing class already owns this responsibility?**

Read this together with `Docs/ARCHITECTURE.md` before changing gameplay code. `ARCHITECTURE.md` defines the architectural rules and boundaries; this file maps those rules to the current codebase so future work extends the correct owner instead of creating duplicate managers, controllers, helpers or systems.

Reviewed against `main` at commit `0c57e4657017d8824b0627c91535e87032ff9f30`. Scope: project-owned C# under `Assets/_Project`.

## Rules for future implementation

1. **Reuse an existing owner first.** A new feature does not automatically require a new class. Find the current owner below and extend it if the responsibility, state and lifecycle already belong there.
2. **Do not create parallel systems.** Do not add classes such as `NewWeaponManager`, `EnemyCombatController`, `DamageManager`, `CameraManager`, `PlayerAnimationHelper`, etc. when an existing boundary already owns that job.
3. **Do not turn an existing class into a God Object.** Reuse is the default, but a genuinely separate responsibility, independent runtime state/lifecycle, reusable contract, or architectural boundary can justify a new class.
4. **One source of truth.** Immutable tuning lives in definitions. Mutable runtime state lives in runtime/instance classes. Controllers execute. UI/debug observe public APIs and events.
5. **Gameplay orchestration must delegate.** `PlayerController` and `EnemyBrain` coordinate other systems; they should not reimplement weapon rules, health logic, camera rendering, Animator details, NavMesh details, or UI presentation.
6. **Animation parameters stay inside animation controllers.** Gameplay code requests actions such as fire/reload/death; it does not call Animator parameters directly.
7. **Debug code is never a gameplay dependency.** Anything under AI Debug/DebugTools can observe or bootstrap tests, but production gameplay must not depend on it.
8. **PrimeTween is presentation-only.** It may smooth UI/visual fading but must not own combat timing, damage, AI transitions or persistence.
9. **Use TextMeshPro for text UI.** Do not introduce legacy `UnityEngine.UI.Text`.
10. When ownership changes, update this file in the same commit.

## Fast feature routing

| Feature to add/change | Extend here first | Notes |
| --- | --- | --- |
| New player input binding/read | `TacticalEcho_InputActions.inputactions` + `PlayerInputReader.cs` | InputReader reports intent only; no gameplay rules. |
| Player movement, character facing, player-level orchestration | `PlayerController.cs` | Delegate camera, weapon, animation, health and UI work to their owners. |
| Player visual/Animator reference resolution | `PlayerVisualController.cs` | Do not put gameplay rules here. |
| Explore/Aim camera, shoulder switch, recoil, crosshair, hit marker | `PlayerCameraController.cs` | Camera behavior and camera-facing feedback belong here. |
| Camera obstruction/fade | `CameraObstructionHandler.cs` | Fades obstructing renderers via `MaterialPropertyBlock`; needs Transparent/Fade-surface materials on the obstructing geometry to be visible. |
| Player Animator parameters | `PlayerAnimationController.cs` | Locomotion, aim, fire, reload, death requests. |
| Enemy Animator parameters | `EnemyAnimationController.cs` | Add missing enemy animation commands here. |
| Shared full-body death clip playback | `DeathAnimationPlayer.cs` | Specialized playback implementation behind animation controllers. |
| Left-hand rifle IK / grip alignment | `WeaponHandIKController.cs` + `WeaponGripPoints.cs` | Preserve authored weapon pose; do not add right-hand IK unless explicitly redesigned. |
| Static weapon tuning | `WeaponDefinition.cs` | Damage, range, fire rate, ammo capacity, spread, recoil, noise. |
| Runtime ammo/reload/fire cooldown/spread | `WeaponRuntime.cs` | Mutable weapon state only. |
| Hitscan firing, damage dispatch, reload execution, gun noise, on-hit status effect application (Suppression/Bleed), tracer/audio/muzzle feedback | `WeaponController.cs` | Weapon execution boundary used by Player and AI. |
| Weapon audio clip data | `WeaponAudioProfile.cs` | Data only. |
| Body-part multiplier/critical region | `DamageHitZone.cs` | Head/torso/arm/leg metadata. |
| Damage request payload | `DamageInfo.cs` | Domain data only. |
| Apply damage to a hit collider | `DamageSystem.cs` | Resolve `IDamageable` and invoke it. |
| HP, death event | `Health.cs` | Shared Player/Enemy health owner. |
| Damage UI feedback payload | `DamageFeedback.cs` | Presentation-facing result, not health mutation. |
| Bullet holes/blood/surface hit VFX | `SurfaceImpactSystem.cs` | Presentation only; no damage authority. |
| Floating damage numbers | `FloatingDamageNumberSystem.cs` | TMP screen-space presentation/pooling. |
| Ammo/reload HUD | `PlayerCombatHud.cs` | Observe `WeaponController` events. |
| Vision | `VisionSensor.cs` | Detect only; no memory or decisions. |
| Hearing | `HearingSensor.cs` | Consume `NoiseEventHub`; detect only. |
| Remember last seen/heard target | `EnemyMemory.cs` | Search/Investigate use remembered positions. |
| High-level AI state coordination | `EnemyBrain.cs` + state classes | Brain coordinates; states own state-specific behavior. |
| AI action scoring | `TacticalEvaluator.cs` + `StandardTacticalActions.cs` | Score/choose intent, then delegate execution. |
| NavMesh movement | `EnemyMovement.cs` | AI movement execution boundary. |
| Cover point data/evaluation | `CoverPoint.cs` + `CoverEvaluator.cs` | Do not mix cover search into Brain. |
| AI state/score/memory debug | `EnemyAIGizmos.cs` / `AIDebugOverlay.cs` | Observation only. |
| Sandbox perception demo enemy | `EnemyPerceptionDemoBootstrap.cs` | Owns sandbox-only edit-time authoring plus runtime binding; never production enemy setup. |
| Status-effect config | `StatusEffectDefinition.cs` | Immutable effect data, including optional damage-over-time (`damagePerTick`/`tickInterval`). |
| Status-effect runtime duration/stack | `StatusEffectInstance.cs` | Mutable instance state. |
| Active status effects | `StatusEffectController.cs` | Apply, stack, expire, tick damage-over-time through the shared damage pipeline, and answer `HasEffect(effectId)` for consumers such as EnemyBrain. |
| Item config | `ItemDefinition.cs` | Immutable item data. |
| Runtime item identity/quantity | `ItemInstance.cs` | Runtime item state. |
| Inventory contents | `InventoryController.cs` | Add/remove/own item collection. |
| Primary/secondary equipment | `EquipmentController.cs` | Equipment slot ownership. |
| Save participant contract | `ISaveParticipant.cs` | Capture/restore contract. |
| Save schema/records | `SaveGameData.cs` | Serialized save DTOs. |
| Save files, temp/backup/load | `SaveManager.cs` | Persistence orchestration. |
| Persistent object identity | `StableId.cs` | Stable save identity. |
| Reduce expensive every-frame work after profiling | `TickScheduler.cs` | Register scheduled callbacks; optimize only with evidence. |

## File-by-file ownership map

### AI

| File | Owns | Extend here when | Keep out |
| --- | --- | --- | --- |
| `AI/Brain/EnemyBrain.cs` | High-level AI coordination, sensor/memory consumption, state transitions, tactical context (including Suppression read from `StatusEffectController`), execution references, death transition | Connecting perception to state decisions; building context for tactical evaluation; coordinating existing execution components | Raw raycasts, NavMesh implementation, weapon mechanics, Animator parameters, UI/debug drawing, effect-specific logic (read `HasEffect` instead) |
| `AI/Cover/CoverEvaluator.cs` | Cover candidate validation and scoring | Better cover scoring, travel/threat criteria, NavMesh-valid cover selection | State transitions or direct movement |
| `AI/Cover/CoverPoint.cs` | Authored cover location and optional peek point | Extra metadata that belongs to a cover point | Global cover search/AI decisions |
| `AI/Debug/EnemyAIGizmos.cs` | Scene gizmos for perception and memory | More read-only visualization | Gameplay state or decisions |
| `AI/Debug/EnemyPerceptionDemoBootstrap.cs` | Sandbox perception-demo prefab/scene authoring, runtime player binding, NavMesh fallback and demo status view | Temporary/debug setup for the perception showcase, including making the demo enemy visible before Play | Production enemy construction, production spawning or gameplay rules |
| `AI/Memory/EnemyMemory.cs` | Last seen/heard positions, times and confidence decay | Memory confidence, remembered target information, forgetting rules | Direct sensing or movement |
| `AI/Navigation/EnemyMovement.cs` | NavMeshAgent execution | Destination, stop, path/reached behavior and later movement execution details | Tactical scoring and perception |
| `AI/Perception/HearingSensor.cs` | Hearing NoiseEventHub events and reporting pending noise | Hearing radius/filters/sensor-side detection | Remembering noise or choosing actions |
| `AI/Perception/ISensor.cs` | Common sensor tick contract | Only when all sensors need a shared contract capability | Sensor-specific data |
| `AI/Perception/VisionSensor.cs` | FOV/range/LOS vision detection | Vision scanning and target visibility rules | Enemy memory or combat decisions |
| `AI/States/EnemyStateBase.cs` | Shared state base/lifecycle access to Brain | Behavior common to all enemy states | Per-state logic that belongs in a concrete state |
| `AI/States/EnemyStateId.cs` | Enemy high-level state identifiers | Add an ID only when there is a genuinely new high-level state | Tactical action IDs |
| `AI/States/PatrolState.cs` | Patrol-state behavior | Patrol routes/idling when implemented | Investigate/search/combat logic |
| `AI/States/InvestigateState.cs` | Reaction to a remembered/heard position | Move to and inspect `EnemyMemory` information | Following live player Transform after LOS is lost |
| `AI/States/CombatState.cs` | Combat-state orchestration | Invoke tactical evaluation and execute the selected intent | Low-level shooting/NavMesh implementation |
| `AI/States/SearchState.cs` | Search around remembered target information, bounded by a timeout back to Patrol | Multi-point search pattern based on `EnemyMemory` | Tracking the real player without LOS |
| `AI/States/RetreatState.cs` | High-level retreat behavior | Retreat state entry/tick/exit | Movement implementation or weapon internals |
| `AI/States/DeadState.cs` | Dead-state entry behavior | Stop/disable state-owned AI activity and request death presentation through the animation boundary | Damage calculation or direct Animator parameter manipulation |
| `AI/TacticalActions/ITacticalAction.cs` | Tactical action contract | Shared requirements for every scored tactical action | Concrete scoring values |
| `AI/TacticalActions/TacticalActionId.cs` | Tactical action identifiers | Add an ID only for a genuinely new tactical intent | High-level state IDs |
| `AI/TacticalActions/TacticalContext.cs` | Read-only decision inputs passed into scoring | New normalized facts required by multiple actions | Mutable world state or references that let actions bypass boundaries |
| `AI/TacticalActions/TacticalEvaluator.cs` | Registering/scoring actions and selecting the best one | Decision frequency, common evaluation policy, selected-action bookkeeping | Movement, shooting or animation execution code |
| `AI/TacticalActions/StandardTacticalActions.cs` | Shoot/Advance/TakeCover/Reposition/Reload/Retreat eligibility, scoring and action intent | Finish existing `Execute` methods and tune action-specific scoring | Duplicated NavMesh/weapon/animation mechanics; call execution owners instead |

### Animation

| File | Owns | Extend here when | Keep out |
| --- | --- | --- | --- |
| `Animation/Runtime/PlayerAnimationController.cs` | All Player Animator parameters/layers and animation commands | Player locomotion/aim/fire/reload/death animation behavior | Player combat rules or input |
| `Animation/Runtime/EnemyAnimationController.cs` | Enemy Animator parameter API | Enemy locomotion/aim/fire/reload and future death command | Enemy tactical decisions |
| `Animation/Runtime/DeathAnimationPlayer.cs` | Shared full-body death clip playback through Playables | Death clip playback mechanics and full-body death-specific IK handling | Health/death rules or state decisions |
| `Animation/Runtime/WeaponHandIKController.cs` | Humanoid left-hand rifle IK and state-dependent IK weight | Support-hand grip behavior and transitions | Weapon firing rules; avoid changing authored right-hand weapon pose here |

### Camera

| File | Owns | Extend here when | Keep out |
| --- | --- | --- | --- |
| `Camera/PlayerCameraController.cs` | Explore/Aim camera, look, follow, shoulder, FOV, recoil, crosshair and hit marker | Any player-camera behavior or camera-facing aiming feedback | Weapon damage/ammo rules or player movement rules |
| `Camera/Obstruction/CameraObstructionHandler.cs` | Camera-to-target obstruction detection and future renderer fading | Complete obstruction/collision/fade behavior; presentation smoothing may use PrimeTween | Camera mode decisions or gameplay state |

### Character / Player

| File | Owns | Extend here when | Keep out |
| --- | --- | --- | --- |
| `Character/Player/PlayerInputReader.cs` | Reading/enabling input actions and keyboard/mouse fallbacks | New player input intent | Movement, weapon, animation or UI behavior |
| `Character/Player/PlayerController.cs` | Player-level orchestration: movement, facing, camera mode request, fire/reload request, health/death reaction | Behavior that truly coordinates multiple player subsystems | Raw weapon rules, Animator details, UI implementation, camera implementation, Health math |
| `Character/Player/PlayerVisualController.cs` | Player visual root/Animator references and root-motion setting | Visual reference setup | Gameplay logic |

### Combat / Damage and Health

| File | Owns | Extend here when | Keep out |
| --- | --- | --- | --- |
| `Combat/Damage/IDamageable.cs` | Shared damageable contract | A capability required by every damageable target | Concrete HP logic |
| `Combat/Damage/DamageInfo.cs` | Immutable damage request data | More domain data every damage receiver needs | UI-only feedback |
| `Combat/Damage/DamageSystem.cs` | Resolve an `IDamageable` from the hit collider and apply DamageInfo | Shared damage dispatch rules | Weapon raycast, HP storage, UI |
| `Combat/Damage/DamageHitZone.cs` | Body region type, multiplier and critical flag | New hit region metadata/multiplier rules | HP mutation |
| `Combat/Damage/DamageFeedback.cs` | Resolved damage information for presentation | Additional presentation-safe result fields | Damage authority |
| `Combat/Health/Health.cs` | Current/max HP, shared IDamageable implementation, HealthChanged/Died events | Healing/health reset only if they belong to universal Health semantics | Player/Enemy-specific death behavior |
| `Combat/Impacts/SurfaceImpactSystem.cs` | Surface classification and pooled bullet-hole/blood hit presentation | Surface impact VFX/audio/pooling | Damage calculation or health |

### Combat / Status effects

| File | Owns | Extend here when | Keep out |
| --- | --- | --- | --- |
| `Combat/StatusEffects/StatusEffectDefinition.cs` | Immutable effect ID, duration, max stacks and stack rule | Effect configuration shared by instances | Per-target runtime timers |
| `Combat/StatusEffects/StatusEffectInstance.cs` | Runtime stacks and expiration for one applied effect | Per-instance duration/stack state | Collection ownership or UI |
| `Combat/StatusEffects/StatusEffectController.cs` | Active effect collection, apply/stack/replace/expiration, damage-over-time ticking through the shared damage pipeline, `HasEffect` lookup for consumers | Target-owned status lifecycle and further modifier aggregation (e.g. Slow feeding movement speed) | Hard-coded Player/Enemy special cases |

### Combat / Weapons

| File | Owns | Extend here when | Keep out |
| --- | --- | --- | --- |
| `Combat/Weapons/FireMode.cs` | Weapon fire-mode identifiers | Add a fire mode only when weapon behavior genuinely needs it | Fire timing implementation |
| `Combat/Weapons/WeaponDefinition.cs` | Immutable weapon tuning/configuration | New static weapon properties | Ammo/cooldown/reload runtime values |
| `Combat/Weapons/WeaponRuntime.cs` | Mutable ammo, fire cooldown, reload state and spread | Runtime gun state/rules | Physics raycasts, VFX or UI |
| `Combat/Weapons/WeaponController.cs` | Weapon execution: firing/hitscan, hit resolution, damage dispatch, reload lifecycle, noise and immediate shot feedback | Shared weapon execution used by Player and Enemy | Player input, AI tactical scoring, HUD implementation |
| `Combat/Weapons/WeaponAudioProfile.cs` | Weapon sound asset references | Fire/reload sound data | Playback timing rules unrelated to weapon execution |
| `Combat/Weapons/WeaponGripPoints.cs` | Right/left grip and muzzle anchors | Weapon attachment/grip anchor data | Character animation decisions |

### Core

| File | Owns | Extend here when | Keep out |
| --- | --- | --- | --- |
| `Core/Events/NoiseEventHub.cs` | Global world-noise event payload and publish/subscribe boundary | More generic data needed by all noise listeners | Hearing decisions or AI memory |
| `Core/StateMachine/IState.cs` | Generic state lifecycle contract | Shared state lifecycle only | Enemy-specific data |
| `Core/StateMachine/StateMachine.cs` | Generic register/change/tick state machine | Reusable state-machine mechanics | Enemy decision logic |

### Debug tools

| File | Owns | Extend here when | Keep out |
| --- | --- | --- | --- |
| `DebugTools/AIDebugOverlay.cs` | TMP display of AI state, tactical scores and memory | Additional read-only AI debugging information | Any gameplay authority |

### Inventory / Equipment

| File | Owns | Extend here when | Keep out |
| --- | --- | --- | --- |
| `Inventory/Items/ItemDefinition.cs` | Immutable item identity/display/type/max stack/icon | Static item data | Runtime quantity/ownership |
| `Inventory/Items/ItemInstance.cs` | Runtime item instance ID, definition and quantity | Mutable per-item state | Inventory collection policy |
| `Inventory/InventoryController.cs` | Owning and mutating the item collection | Add/remove/stack inventory behavior | Direct UI collection mutation |
| `Inventory/Equipment/EquipmentController.cs` | Primary/secondary equipment slot ownership | Equip/unequip/switch-slot behavior | Weapon firing implementation |

### Optimization

| File | Owns | Extend here when | Keep out |
| --- | --- | --- | --- |
| `Optimization/TickScheduler.cs` | Lower-frequency callback scheduling | Profiling shows expensive systems do not need every-frame updates | Premature optimization or gameplay decisions |

### Save / Load

| File | Owns | Extend here when | Keep out |
| --- | --- | --- | --- |
| `SaveLoad/Contracts/ISaveParticipant.cs` | Stable ID + capture/restore contract | Requirements common to every persistent participant | File I/O |
| `SaveLoad/Data/SaveGameData.cs` | `SaveRecord` and versioned save DTO | Serialized schema fields | File system operations |
| `SaveLoad/Persistence/SaveManager.cs` | Participant registration, JSON file write, temp/main/backup read path | Restore orchestration, validation/migration entry flow and file persistence | Gameplay-specific state logic |
| `SaveLoad/StableId.cs` | Stable serialized object ID | Persistent identity generation/validation | Save-file I/O |

### UI

| File | Owns | Extend here when | Keep out |
| --- | --- | --- | --- |
| `UI/PlayerCombatHud.cs` | TMP ammo and reload-state HUD, observing WeaponController events | Weapon HUD presentation | Ammo mutation or reload rules |
| `UI/FloatingDamageNumberSystem.cs` | Pooled TMP screen-space damage numbers, random offset/rise/fade and critical styling | Damage-number presentation | Damage calculation or Health mutation |

## Existing extension points that should be filled before creating replacements

The foundation intentionally contains several unfinished extension points. Future tasks should complete these owners instead of making parallel classes:

- `PatrolState`, `InvestigateState`, `CombatState`, `SearchState` and `RetreatState` are currently state shells. Put the corresponding state behavior there.
- `ShootAction`, `AdvanceAction`, `TakeCoverAction`, `RepositionAction`, `ReloadAction` and `RetreatAction` already exist and already score decisions. Their `Execute` methods are currently empty. Finish them and delegate to `EnemyMovement`, `WeaponController` and animation boundaries instead of creating `EnemyShootController`, `EnemyReloadController`, etc.
- `CameraObstructionHandler` now fades obstructing renderers via `MaterialPropertyBlock` (alpha on `_BaseColor`), not just querying for them. This only has a visible effect on materials whose Surface Type is Transparent/Fade - extend this component (not a new one) once obstructing geometry uses such materials. Camera collision (physically pulling the camera in front of geometry) is still not implemented.
- `SaveManager` already owns main/temp/backup persistence and `ISaveParticipant` already defines capture/restore. Validation, migration and restoration should grow inside the SaveLoad boundary rather than as unrelated gameplay managers.
- `StatusEffectDefinition`/`StatusEffectInstance`/`StatusEffectController` establish the status pipeline. `WeaponController` applies Suppression on any damaging hit and Bleed on a critical hit (`suppressionOnHit`/`bleedOnCriticalHit`), and `EnemyBrain` reads Suppression through `StatusEffectController.HasEffect` into `TacticalContext.Suppression` - both Player and Enemy weapons apply the same definitions, so this is not hard-coded per class. `Bleed_Standard.asset`/`Suppression_Standard.asset` live under `Combat/StatusEffects/Definitions/`. Slow is not implemented yet - feed it through the same pipeline (a definition with a movement-speed modifier read by `EnemyMovement`/`PlayerController`) rather than adding a parallel system. Player currently has no `StatusEffectController` attached, so Bleed only affects Enemy until one is added to `Player_Kaia.prefab`.
- `InventoryController` and `EquipmentController` already establish inventory/equipment ownership. Future UI should use their APIs rather than maintaining a second inventory list.

## Current ownership drift to avoid copying

The codebase is in active development, so this map also records places that should **not become precedent**:

- `EnemyBrain` currently invokes `DeathAnimationPlayer` directly on death. The architecture says animation execution belongs behind an animation controller. When enemy animation/death work is touched, extend `EnemyAnimationController` with the death command and let Brain request it; do not add a separate `EnemyDeathController`.
- `PlayerController` currently bridges weapon damage feedback to hit marker/floating-number presentation. Do not use that as a reason to keep adding UI rendering responsibilities to PlayerController. Prefer existing UI/camera presentation owners and event-based observation.
- `EnemyPerceptionDemoBootstrap` is deliberately a sandbox-only debug harness. Editor authoring and temporary runtime wiring for the perception showcase belong here, but do not grow the real enemy architecture or production spawning inside this file.

These are targeted cleanup directions, not a request for a broad refactor before the relevant feature is worked on.

## When a new class is actually justified

Create a new class only when at least one of these is true and no existing owner above fits cleanly:

- the feature introduces a distinct responsibility that would make the current owner violate single responsibility;
- it owns independent mutable runtime state or a lifecycle that should not be coupled to the existing owner;
- it defines a reusable contract/boundary needed by multiple systems;
- it is a new high-level State or tactical action with its own lifecycle/identity;
- it is an execution boundary that the architecture explicitly separates.

Do **not** create a class merely because the feature has a new name, needs a few methods, or feels easier to implement in isolation.

## Important non-C# anchors

- Player prefab: `Assets/_Project/Prefabs/Player/Player_Kaia.prefab`
- Player input asset: `Assets/_Project/Input/TacticalEcho_InputActions.inputactions`
- Player Animator controller: `Assets/_Project/Animation/Controllers/Player_Kaia_Locomotion.controller`
- Rifle definition: `Assets/_Project/Combat/Weapons/Definitions/Rifle_HK416.asset`
- Status effect definitions: `Assets/_Project/Combat/StatusEffects/Definitions/Suppression_Standard.asset`, `Bleed_Standard.asset`
- Shared death animation resource: `Assets/_ThirdParty/Animations/Resources/Death/Death_From_Front_Headshot.fbx` - imported as Humanoid so it retargets onto Kaia's Avatar; verify the auto-generated bone mapping under Rig > Configure... if it is ever reimported.

The authored `WeaponMount`/gun pose is intentional. `WeaponHandIKController` provides left-hand support IK; the right hand owns the weapon through the hierarchy. Do not replace this with a right-hand IK loop or rewrite authored weapon transforms unless that is an explicit task.

## Required workflow before future code changes

```text
1. Fetch latest main.
2. Read Docs/ARCHITECTURE.md.
3. Read Docs/CODEBASE_MAP.md.
4. Read the current files that own the requested feature.
5. Reuse/extend those owners first.
6. Add a class only if the new-class gate above is satisfied.
7. Implement and test the smallest coherent change.
8. If ownership/architecture changed, update the docs in the same commit.
```

`ARCHITECTURE.md` remains the higher-level source of truth. This file is the current routing/index for where implementations belong.