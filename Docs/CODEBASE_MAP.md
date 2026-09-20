# Tactical Echo Codebase Ownership Map

This document answers one question before adding a feature: **which existing class already owns this responsibility?**

Read this together with `Docs/ARCHITECTURE.md` before changing gameplay code. `ARCHITECTURE.md` defines the architectural rules and boundaries; this file maps those rules to the current codebase so future work extends the correct owner instead of creating duplicate managers, controllers, helpers or systems.

## Rules for future implementation

1. **Reuse an existing owner first.** A new feature does not automatically require a new class. Find the current owner below and extend it if the responsibility, state and lifecycle already belong there.
2. **Do not create parallel systems.** Do not add classes such as `NewWeaponManager`, `EnemyCombatController`, `DamageManager`, `CameraManager`, `PlayerAnimationHelper`, etc. when an existing boundary already owns that job.
3. **Do not turn an existing class into a God Object.** Reuse is the default, but a genuinely separate responsibility, independent runtime state/lifecycle, reusable contract, or architectural boundary can justify a new class.
4. **One source of truth.** Immutable tuning lives in definitions. Mutable runtime state lives in runtime/instance classes. Controllers execute. UI/debug observe public APIs and events.
5. **Gameplay orchestration must delegate.** `PlayerController` and `EnemyBrain` coordinate other systems; they should not reimplement weapon rules, health logic, camera rendering, Animator details, NavMesh details, or UI presentation.
6. **Animation parameters stay inside animation controllers.** Gameplay code requests actions such as fire/reload/death; it does not call Animator parameters directly.
7. **Debug code is never a gameplay dependency.** Anything under AI Debug/DebugTools can observe or bootstrap tests, but production gameplay must not depend on it.

## Fast feature routing

| Feature to add/change | Extend here first | Notes |
| --- | --- | --- |
| New player input binding/read | `TacticalEcho_InputActions.inputactions` + `PlayerInputReader.cs` | InputReader reports intent only; no gameplay rules. |
| Player movement, character facing, player-level orchestration | `PlayerController.cs` | Delegate camera, weapon, animation, health and UI work to their owners. |
| Player visual/Animator reference resolution | `PlayerVisualController.cs` | Do not put gameplay rules here. |
| Explore/Aim camera, shoulder switch, recoil, crosshair, hit marker | `PlayerCameraController.cs` | Camera behavior and camera-facing feedback belong here. |
| Camera obstruction/fade/collision | `CameraObstructionHandler.cs` | Fades obstructing renderers via `MaterialPropertyBlock` (needs Transparent/Fade-surface materials to be visible) and resolves the camera-collision push for `PlayerCameraController`. |
| Player Animator parameters | `PlayerAnimationController.cs` | Locomotion, aim, fire, reload, death requests. |
| Enemy Animator parameters | `EnemyAnimationController.cs` | Add missing enemy animation commands here. `PlayDeath()`/`ClearDeath()` raise and release the death latch as a pair. |
| Shared full-body death clip playback | `DeathAnimationPlayer.cs` | Specialized playback implementation behind animation controllers. `Play()` takes over the Animator's output entirely; `Stop()` gives it back. |
| Left-hand rifle IK / grip alignment | `WeaponHandIKController.cs` + `WeaponGripPoints.cs` | Preserve authored weapon pose; do not add right-hand IK unless explicitly redesigned. |
| Static weapon tuning | `WeaponDefinition.cs` | Damage, range, fire rate, ammo capacity, spread, recoil, noise. |
| Runtime ammo/reload/fire cooldown/spread | `WeaponRuntime.cs` | Mutable weapon state only. |
| Hitscan firing, damage dispatch, reload execution, gun noise, on-hit status effect application (Suppression/Bleed), tracer/audio/muzzle feedback | `WeaponController.cs` | Weapon execution boundary used by Player and AI. |
| Weapon audio clip data | `WeaponAudioProfile.cs` | Data only. |
| Body-part multiplier/critical region | `DamageHitZone.cs` | Head/torso/arm/leg metadata, and the `CharacterHitZone` layer that keeps body colliders shootable but physically inert. |
| Damage request payload | `DamageInfo.cs` | Domain data only. |
| Apply damage to a hit collider | `DamageSystem.cs` | Resolve `IDamageable` and invoke it. |
| HP, death event | `Health.cs` | Shared Player/Enemy health owner. Reports `IsAlive` until initialized so observers cannot latch a death before `Awake()`. |
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
| Status-effect config | `StatusEffectDefinition.cs` | Immutable effect data: damage-over-time (`damagePerTick`/`tickInterval`) and modifier channels (`moveSpeedMultiplier`). |
| Status-effect runtime duration/stack | `StatusEffectInstance.cs` | Mutable instance state. |
| Active status effects | `StatusEffectController.cs` | Apply, stack, expire, tick damage-over-time through the shared damage pipeline, and answer `HasEffect(effectId)` for consumers such as EnemyBrain. |
| Item config | `ItemDefinition.cs` | Immutable item data, including the `WeaponDefinition` a weapon item equips. |
| Runtime item identity/quantity | `ItemInstance.cs` | Runtime item state. |
| Inventory contents | `InventoryController.cs` | Add/remove/own item collection. |
| Primary/secondary equipment | `EquipmentController.cs` | Equipment slot ownership, starting loadout and active-slot selection. |
| Save participant contract | `ISaveParticipant.cs` | Capture/restore contract. |
| Save schema version, migration, validation | `SaveSchema.cs` | What a save may contain and how an old one is upgraded. |
| Weapon/status/save debug views | `WeaponDebugOverlay.cs`, `StatusEffectDebugOverlay.cs`, `SaveDebugOverlay.cs` | Read-only overlays built on `DebugOverlayBase`. |
| Save schema/records | `SaveGameData.cs` | Serialized save DTOs. |
| Save files, temp/backup/load | `SaveManager.cs` | Persistence orchestration. |
| Persistent object identity | `StableId.cs` | Stable save identity. |
| Reduce expensive every-frame work after profiling | `TickScheduler.cs` | Register scheduled callbacks (`EnemyBrain` perception already does); optimize only with evidence. |
| Patrol waypoints in a scene | `PatrolRoute.cs` | Authored route markers, like `CoverPoint` is authored cover. |

## File-by-file ownership map

### AI

| File | Owns | Extend here when | Keep out |
| --- | --- | --- | --- |
| `AI/Brain/EnemyBrain.cs` | High-level AI coordination, sensor/memory consumption, state transitions, tactical context (including Suppression read from `StatusEffectController`), execution references, death transition | Connecting perception to state decisions; building context for tactical evaluation; coordinating existing execution components | Raw raycasts, NavMesh implementation, weapon mechanics, Animator parameters, UI/debug drawing, effect-specific logic (read `HasEffect` instead) |
| `AI/Cover/CoverEvaluator.cs` | Cover candidate validation and scoring | Better cover scoring, travel/threat criteria, NavMesh-valid cover selection | State transitions or direct movement |
| `AI/Cover/CoverPoint.cs` | Authored cover location and optional peek point, plus `ConfigurePeekPoint` for spawners | Extra metadata that belongs to a cover point | Global cover search/AI decisions |
| `AI/Debug/EnemyAIGizmos.cs` | Scene gizmos for perception and memory | More read-only visualization | Gameplay state or decisions |
| `AI/Memory/EnemyMemory.cs` | Last seen/heard positions, times and confidence decay | Memory confidence, remembered target information, forgetting rules | Direct sensing or movement |
| `AI/Navigation/EnemyMovement.cs` | NavMeshAgent execution | Destination, stop, path/reached behavior and later movement execution details. `Stop()` parks the agent (`isStopped`, zeroed velocity); `SetDestination()` releases it again | Tactical scoring and perception |
| `AI/Perception/HearingSensor.cs` | Hearing NoiseEventHub events and reporting pending noise | Hearing radius/filters/sensor-side detection | Remembering noise or choosing actions |
| `AI/Perception/ISensor.cs` | Common sensor tick contract | Only when all sensors need a shared contract capability | Sensor-specific data |
| `AI/Perception/VisionSensor.cs` | FOV/range/LOS vision detection | Vision scanning and target visibility rules | Enemy memory or combat decisions |
| `AI/States/EnemyStateBase.cs` | Shared state base/lifecycle access to Brain | Behavior common to all enemy states | Per-state logic that belongs in a concrete state |
| `AI/States/EnemyStateId.cs` | Enemy high-level state identifiers | Add an ID only when there is a genuinely new high-level state | Tactical action IDs |
| `AI/Patrol/PatrolRoute.cs` | Authored patrol path: ordered waypoints, looping and nearest-waypoint lookup | Route shape and route queries | Who walks it, or when |
| `AI/States/PatrolState.cs` | Walking the authored `PatrolRoute` with a dwell at each waypoint, and holding position when no route is assigned | Patrol pacing, route-following rules | Route data itself; investigate/search/combat logic |
| `AI/States/InvestigateState.cs` | Reaction to a remembered/heard position | Move to and inspect `EnemyMemory` information | Following live player Transform after LOS is lost |
| `AI/States/CombatState.cs` | Combat-state orchestration | Invoke tactical evaluation and execute the selected intent | Low-level shooting/NavMesh implementation |
| `AI/States/SearchState.cs` | Sweeping the remembered position plus a deterministic ring around it, bounded by a timeout back to Patrol | Sweep shape, dwell pacing | Tracking the real player without LOS |
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
| `Animation/Runtime/EnemyAnimationController.cs` | Enemy Animator parameter API | Enemy locomotion/aim/fire/reload, plus the death latch (`PlayDeath`/`ClearDeath`) that mutes parameter writes while dead | Enemy tactical decisions |
| `Animation/Runtime/DeathAnimationPlayer.cs` | Shared full-body death clip playback through Playables | Death clip playback mechanics, Playable graph lifecycle (`Play`/`Stop`), root-motion handling for the fall (the clip is not pose-baked) and full-body death-specific IK handling, scoped to the Animator's own hierarchy | Health/death rules or state decisions |
| `Animation/Runtime/WeaponHandIKController.cs` | Humanoid left-hand rifle IK and state-dependent IK weight | Support-hand grip behavior and transitions | Weapon firing rules; avoid changing authored right-hand weapon pose here |

### Camera

| File | Owns | Extend here when | Keep out |
| --- | --- | --- | --- |
| `Camera/PlayerCameraController.cs` | Explore/Aim camera, look, follow, shoulder, FOV, recoil, crosshair and hit marker | Any player-camera behavior or camera-facing aiming feedback | Weapon damage/ammo rules or player movement rules |
| `Camera/Obstruction/CameraObstructionHandler.cs` | What counts as an obstruction: renderer fading and the camera-collision resolve (`ResolveCameraPosition`) | Obstruction/collision/fade behavior; presentation smoothing may use PrimeTween | Where the camera is - that stays in `PlayerCameraController`; camera mode decisions |

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
| `Combat/Damage/DamageHitZone.cs` | Body region type, multiplier and critical flag, and the physics isolation of body-part colliders (`CharacterHitZone` layer, ignored against every collision layer) | New hit region metadata/multiplier rules; anything about how body colliders participate in physics | HP mutation |
| `Combat/Damage/DamageFeedback.cs` | Resolved damage information for presentation | Additional presentation-safe result fields | Damage authority |
| `Combat/Health/Health.cs` | Current/max HP, shared IDamageable implementation, HealthChanged/Died events, initialization contract (`IsAlive` is true until initialized, so `OnEnable` observers cannot read a full-health character as dead) | Healing/health reset only if they belong to universal Health semantics | Player/Enemy-specific death behavior |
| `Combat/Impacts/SurfaceImpactSystem.cs` | Surface classification and pooled bullet-hole/blood hit presentation | Surface impact VFX/audio/pooling | Damage calculation or health |

### Combat / Status effects

| File | Owns | Extend here when | Keep out |
| --- | --- | --- | --- |
| `Combat/StatusEffects/StatusEffectDefinition.cs` | Immutable effect ID, duration, max stacks, stack rule, damage tick and modifier channels | Effect configuration shared by instances; a new modifier channel | Per-target runtime timers; reaching into the systems a modifier affects |
| `Combat/StatusEffects/StatusEffectInstance.cs` | Runtime stacks and expiration for one applied effect | Per-instance duration/stack state | Collection ownership or UI |
| `Combat/StatusEffects/StatusEffectController.cs` | Active effect collection, apply/stack/replace/expiration, damage-over-time ticking through the shared damage pipeline, `HasEffect` lookup, and aggregated modifiers (`MoveSpeedMultiplier`) | Target-owned status lifecycle; a new aggregated modifier | Hard-coded Player/Enemy special cases; writing into movement or combat directly |

### Combat / Weapons

| File | Owns | Extend here when | Keep out |
| --- | --- | --- | --- |
| `Combat/Weapons/FireMode.cs` | Weapon fire-mode identifiers | Add a fire mode only when weapon behavior genuinely needs it | Fire timing implementation |
| `Combat/Weapons/WeaponDefinition.cs` | Immutable weapon tuning/configuration | New static weapon properties | Ammo/cooldown/reload runtime values |
| `Combat/Weapons/WeaponRuntime.cs` | Mutable ammo, fire cooldown, reload state and spread | Runtime gun state/rules | Physics raycasts, VFX or UI |
| `Combat/Weapons/WeaponController.cs` | Weapon execution: firing/hitscan, hit resolution, damage dispatch, reload lifecycle, noise, immediate shot feedback, and one `WeaponRuntime` per definition so a swapped-away weapon keeps its ammo (`Equip`) | Shared weapon execution used by Player and Enemy | Player input, AI tactical scoring, HUD implementation, which weapon should be in hand |
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
| `DebugTools/DebugOverlayBase.cs` | Shared overlay shape: TMP target or self-built screen text, throttled rebuild (`refreshInterval`, default 0.1s) | Behaviour common to every debug overlay | Anything specific to one observed system |
| `DebugTools/AIDebugOverlay.cs` | TMP display of AI state, tactical scores and memory | Additional read-only AI debugging information | Any gameplay authority |
| `DebugTools/WeaponDebugOverlay.cs` | TMP display of weapon runtime: ammo, reload timing, current and effective spread | Additional read-only weapon debugging information | Weapon rules; it must not drive firing |
| `DebugTools/StatusEffectDebugOverlay.cs` | TMP display of active status effects: stacks, remaining, damage tick | Additional read-only status debugging information | Applying or expiring effects |
| `DebugTools/SaveDebugOverlay.cs` | TMP display of save files and the last write/read result, plus debug save/load keys | Additional read-only save debugging information | Save format, validation or migration rules |

### Inventory / Equipment

| File | Owns | Extend here when | Keep out |
| --- | --- | --- | --- |
| `Inventory/Items/ItemDefinition.cs` | Immutable item identity/display/type/max stack/icon, and the `WeaponDefinition` link for weapon items | Static item data | Runtime quantity/ownership; any ballistics value that belongs on `WeaponDefinition` |
| `Inventory/Items/ItemInstance.cs` | Runtime item instance ID, definition and quantity | Mutable per-item state | Inventory collection policy |
| `Inventory/InventoryController.cs` | Owning and mutating the item collection | Add/remove/stack inventory behavior | Direct UI collection mutation |
| `Inventory/Equipment/EquipmentController.cs` | Primary/secondary slot ownership, starting loadout, which slot is active, and pushing that slot's `WeaponDefinition` into the character's `WeaponController` | Equip/unequip/switch-slot behavior, loadout rules | Weapon firing implementation, ammo bookkeeping |

### Optimization

| File | Owns | Extend here when | Keep out |
| --- | --- | --- | --- |
| `Optimization/TickScheduler.cs` | The shared low-frequency tick budget and its scene-wide instance (`TickScheduler.Shared`) | Systems that should share the budget; budget interval policy | Premature optimization or gameplay decisions |

### Save / Load

| File | Owns | Extend here when | Keep out |
| --- | --- | --- | --- |
| `SaveLoad/Contracts/ISaveParticipant.cs` | Stable ID + capture/restore contract | Requirements common to every persistent participant | File I/O |
| `SaveLoad/Data/SaveGameData.cs` | `SaveRecord`, the versioned save DTO and `CurrentSchemaVersion` | Serialized schema fields | File system operations; validation and migration rules |
| `SaveLoad/Data/SaveSchema.cs` | Current schema version, migration steps keyed by the version they upgrade from, and what a valid save may contain | A new schema version and its migration step; stricter validation rules | File I/O, participant knowledge |
| `SaveLoad/Persistence/SaveManager.cs` | Participant registration, JSON file write, temp/main/backup read path, and the last write/read outcome | Restore orchestration and file persistence | Gameplay-specific state logic; schema rules, which belong to `SaveSchema` |
| `SaveLoad/StableId.cs` | Stable serialized object ID | Persistent identity generation/validation | Save-file I/O |

### UI

| File | Owns | Extend here when | Keep out |
| --- | --- | --- | --- |
| `UI/PlayerCombatHud.cs` | TMP ammo and reload-state HUD, observing WeaponController events | Weapon HUD presentation | Ammo mutation or reload rules |
| `UI/FloatingDamageNumberSystem.cs` | Pooled TMP screen-space damage numbers, random offset/rise/fade and critical styling | Damage-number presentation | Damage calculation or Health mutation |

## Existing extension points that should be filled before creating replacements

The foundation intentionally contains several unfinished extension points. Future tasks should complete these owners instead of making parallel classes:
The architecture provides clear extension points designed for scalability. Future features should extend these established owners rather than introducing parallel classes:

- `PatrolState`, `InvestigateState`, `CombatState`, `SearchState` and `RetreatState` own their own behaviour; extend those states rather than adding a controller beside them. Patrol follows a `PatrolRoute`, Search sweeps a ring derived from `EnemyMemory` - both must keep deriving destinations from remembered snapshots, never the live player Transform. No `PatrolRoute`/`CoverPoint` is authored in any scene or prefab yet, so Patrol only holds position and `TakeCoverAction` never finds cover - both paths are complete and wired, just unused for now.
- `ShootAction`, `AdvanceAction`, `TakeCoverAction`, `RepositionAction`, `ReloadAction` and `RetreatAction` already exist, already score decisions, and their `Execute` methods are fully implemented (they delegate to `EnemyMovement`, `WeaponController` and the animation controllers). Extend an existing action's scoring/execution instead of creating `EnemyShootController`, `EnemyReloadController`, etc.
- `CameraObstructionHandler` fades obstructing renderers via `MaterialPropertyBlock` (alpha on `_BaseColor`) and resolves camera collision by pulling the camera in front of geometry (`ResolveCameraPosition`, `Physics.SphereCastNonAlloc`). The fade only has a visible effect on materials whose Surface Type is Transparent/Fade - extend this component (not a new one) for either behaviour.
- `SaveManager` already owns main/temp/backup persistence and `ISaveParticipant` already defines capture/restore. Validation, migration and restoration should grow inside the SaveLoad boundary rather than as unrelated gameplay managers.
- `StatusEffectDefinition`/`StatusEffectInstance`/`StatusEffectController` form the status pipeline. `WeaponController` applies on-hit effects through a configurable `onHitEffects` rule list (each rule is a `StatusEffectDefinition` plus a `requireCriticalHit` flag), so a new on-hit effect is one more list entry, not a new field/branch. Suppression and Bleed are applied on hit and both Player and Enemy weapons use the same definitions; `EnemyBrain` reads Suppression through `StatusEffectController.HasEffect` into `TacticalContext.Suppression`. `Slow_Standard.asset` is wired end-to-end too - `PlayerController` and `EnemyMovement` both read `StatusEffectController.MoveSpeedMultiplier` to scale movement speed. Both `Player_Kaia.prefab` and the enemy prefab carry a `StatusEffectController`.
- `InventoryController` and `EquipmentController` already establish inventory/equipment ownership, are attached to both the Player and Enemy prefabs, and drive the weapon in hand through `WeaponController.Equip`. Future UI should use their APIs rather than maintaining a second inventory list, and a new weapon should be a `WeaponDefinition` plus an `ItemDefinition` that points at it - not a new controller or a second weapon field on a character.
- `PatrolState`, `InvestigateState`, `CombatState`, `SearchState` and `RetreatState` own their own behaviour; extend those states rather than adding a controller beside them. Patrol follows a `PatrolRoute`, Search sweeps a ring derived from `EnemyMemory` - both derive destinations from remembered snapshots, never the live player Transform. When no `PatrolRoute` or `CoverPoint` is authored in a scene, states fall back gracefully (e.g., Patrol holds position).
- `ShootAction`, `AdvanceAction`, `TakeCoverAction`, `RepositionAction`, `ReloadAction` and `RetreatAction` already exist, score decisions, and their `Execute` methods delegate to `EnemyMovement`, `WeaponController` and the animation controllers. Extend an existing action's scoring/execution instead of creating redundant controllers.
- `CameraObstructionHandler` fades obstructing renderers via `MaterialPropertyBlock` (alpha on `_BaseColor`) and resolves camera collision by pulling the camera in front of geometry (`ResolveCameraPosition`, `Physics.SphereCastNonAlloc`). The fade effect applies to materials whose Surface Type is Transparent/Fade - extend this component for obstruction or collision behaviour.
- `SaveManager` owns persistence orchestration (main/temp/backup paths) and `ISaveParticipant` defines the capture/restore contract. Validation, migration, and state restoration belong within the SaveLoad boundary rather than ad-hoc managers.
- `StatusEffectDefinition`/`StatusEffectInstance`/`StatusEffectController` form the status pipeline. `WeaponController` applies on-hit effects through a configurable `onHitEffects` rule list (each rule pairs a `StatusEffectDefinition` with a `requireCriticalHit` flag), allowing new effects to be added without code modifications. Suppression and Bleed are applied on hit across both Player and Enemy weapons; `EnemyBrain` reads Suppression via `StatusEffectController.HasEffect` into `TacticalContext.Suppression`. `Slow_Standard.asset` is wired end-to-end - `PlayerController` and `EnemyMovement` both query `StatusEffectController.MoveSpeedMultiplier` to scale movement speed. Both `Player_Kaia.prefab` and the enemy prefab carry a `StatusEffectController`.
- `InventoryController` and `EquipmentController` establish inventory and loadout ownership, attached to both Player and Enemy prefabs, and drive the active weapon through `WeaponController.Equip`. UI and future systems should interact through their public APIs, and a new weapon is added via `WeaponDefinition` and `ItemDefinition` assets rather than hardcoded references.
