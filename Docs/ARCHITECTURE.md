# Tactical Echo Architecture

## Design goal

Keep data collection, decision making, execution, persistence and presentation separate so no single `EnemyAI` or `PlayerController` becomes a God Object.

## AI flow

```text
VisionSensor ----+
                 +--> EnemyMemory --> EnemyBrain --> StateMachine
HearingSensor ---+                         |
                                           +--> TacticalEvaluator
                                                   |
                                                   +--> Shoot
                                                   +--> Advance
                                                   +--> TakeCover
                                                   +--> Reposition
                                                   +--> Reload
                                                   +--> Retreat

Execution boundaries:
EnemyMovement / WeaponController / AnimationController
```

Sensors only collect information. `EnemyMemory` owns remembered information. `EnemyBrain` coordinates high-level state. `TacticalEvaluator` scores combat options. Tactical actions issue intent; movement, weapon and animation components execute it.

The AI must never keep following the real player transform after line-of-sight is lost. Search and investigate behavior use remembered positions instead.

## Combat flow

```text
PlayerInputReader
 -> PlayerController
 -> WeaponController
 -> WeaponRuntime
 -> DamageInfo
 -> IDamageable
 -> Health
 -> DeadState / PlayerController
 -> AnimationController.PlayDeath()
 -> DeathAnimationPlayer (Playable graph owns the Animator until Stop())

WeaponController
 -> NoiseEventHub
 -> HearingSensor

WeaponController
 -> StatusEffectController (on the hit target)
```

`WeaponDefinition` contains immutable configuration. `WeaponRuntime` contains mutable ammo, fire cooldown and reload state.

## Status effects

`StatusEffectDefinition` describes duration/stacking rules and an optional damage-over-time rate (`damagePerTick`/`tickInterval`). `StatusEffectInstance` owns runtime duration/stack/tick state. `StatusEffectController` owns active effects, ticks damage-over-time back through the shared damage pipeline (`DamageInfo` -> `IDamageable`), and exposes `HasEffect(effectId)` for consumers.

`WeaponController` applies a configured `StatusEffectDefinition` to whatever it hits (any damaging hit is suppressive; a critical hit also applies Bleed), so status application stays part of the shared weapon execution boundary rather than being hard-coded per Player/Enemy. `EnemyBrain` reads an active Suppression effect through `StatusEffectController.HasEffect` and turns its stack count into `TacticalContext.Suppression`, so `TakeCoverAction`/`ShootAction`/etc. score against it without EnemyBrain special-casing the effect itself.

Effects reach gameplay through modifier channels, never by writing into other systems. `StatusEffectDefinition.MoveSpeedMultiplier` is the first such channel: `StatusEffectController.MoveSpeedMultiplier` multiplies it out across every active stack, and the movement owners read it - `EnemyMovement` scales the agent speed it was configured with, `PlayerController` scales walk/sprint. Nothing in the effect pipeline touches a NavMeshAgent or a CharacterController. Because the channel is data, `Slow_Standard.asset` is a definition with a multiplier and no code of its own, and Suppression also carries a mild slow.

## Inventory/equipment

`ItemDefinition` is immutable item data. `ItemInstance` is runtime identity/state. `InventoryController` owns items. `EquipmentController` owns the primary/secondary slots. UI must call these APIs instead of editing collections directly.

A weapon item is the single link between the two halves of the project: `ItemDefinition.WeaponDefinition` points at the combat configuration, so inventory owns what the item is and `WeaponDefinition` owns how it shoots, with no ballistics data duplicated on the item. `EquipmentController` builds its starting loadout from authored `ItemDefinition` assets in `Awake`, adds them to the `InventoryController` and equips them through its own `Equip` API, then pushes the active slot's `WeaponDefinition` into the character's `WeaponController`. Slot selection is a request that `EquipmentController` can refuse - an empty slot is never brought into hand - and input only names the slot it wants. `WeaponController` keeps one `WeaponRuntime` per definition for its lifetime, so swapping to the other slot and back returns a weapon with the ammo it was left with instead of a freshly loaded one. Both Player and Enemy carry the same two components; the AI simply never asks for a slot change.

## Save/load

```text
ISaveParticipant.CaptureState()
 -> SaveGameData(schemaVersion)
 -> JSON
 -> temp file
 -> replace main file + backup

Load
 -> main file
 -> fallback backup
 -> validate/migrate
 -> restore by StableId
```

`SaveSchema` owns the version number this build writes, what a valid save may contain, and how an older one is brought up to date; `SaveManager` owns the files and the participants and asks `SaveSchema` whether parsed data may be trusted. A load reads, rejects anything written by a newer build, walks one migration step per version until the data reaches the current schema, then validates it - records present, every record carrying a non-blank and unique `stableId`, a representable timestamp. Only data that passes all three is handed back, so a file that parses but is structurally broken falls through to the backup instead of reaching participants. Migration steps are keyed by the version they upgrade *from* and always produce that version plus one: adding a schema field means adding the step that fills it in for older saves, never special-casing a version at the call site. Restore orchestration - matching records back to participants by `StableId` - is still open.

## Camera and animation

Camera owns Explore/Aim/shoulder/collision/obstruction responsibilities. The split between the two camera types is deliberate: `PlayerCameraController` owns where the camera is, `CameraObstructionHandler` owns what counts as an obstruction. The controller computes its desired position and asks the handler to resolve it; the handler sphere-casts from the follow target, ignores colliders in that target's own hierarchy, and returns a point in front of the first blocking surface. Resolving before the smoothing lerp makes the camera slide along a wall instead of snapping once it is already inside one. The same handler fades renderers that remain between camera and target, so detection is written once and used by both behaviours. `PlayerInputReader.SwitchShoulderPressedThisFrame` (Q by keyboard fallback, matching the Reload pattern) drives `PlayerCameraController.SwitchShoulder()` through `PlayerController`. Animation controllers centralize Animator parameters so gameplay classes do not set Animator parameters across the codebase.

PrimeTween is allowed only for presentation smoothing such as UI and obstruction fading; it must not own AI state transitions, combat cooldowns, damage or save logic. Only PrimeTween's Editor installer is currently present under `Assets/Plugins/PrimeTween` - the runtime package itself is not installed yet, so presentation smoothing (camera blend, obstruction fade) currently uses the project's existing manual exponential-blend convention and should move to PrimeTween once the package is actually installed.

Text UI uses TextMeshPro (`TMP_Text`/`TextMeshProUGUI`), not legacy `UnityEngine.UI.Text`.

## Debugging

Every overlay derives from `DebugOverlayBase`, which owns the shared shape: an optional authored TextMeshPro target, a screen-space text the overlay builds for itself when none is assigned, and one text rebuild per frame. `AIDebugOverlay` shows state, tactical action scores and memory; `WeaponDebugOverlay` shows ammo, reload timing and the spread cone that actually drives shot direction; `StatusEffectDebugOverlay` shows active effects with stacks, remaining duration and the damage-over-time tick; `SaveDebugOverlay` shows what is on disk plus the result of the last versioned/validated read. Scene gizmos visualize perception and remembered positions. Debug code observes runtime systems and never becomes a gameplay dependency - the save and load keys on `SaveDebugOverlay` are a harness for exercising the save pipeline, not something gameplay reads.

## Character physics

Body-part colliders (`DamageHitZone`) exist to be found by weapon raycasts, not to participate in physics. They are parented to animated bones, so they jump to a new position every frame rather than sweeping, and a solid collider that teleports into a `CharacterController` is resolved by depenetrating the whole overlap in a single frame. They therefore live on the dedicated `CharacterHitZone` layer, which is ignored against every collision layer at startup. Raycasts are not affected by the collision matrix, so damage and hit-zone multipliers keep working exactly as before. Characters do not block each other physically; separation is the NavMeshAgent's job.

The shared death clip is imported with Unity's default humanoid settings, so its fall to the floor arrives as root motion rather than baked into the pose. Gameplay keeps `applyRootMotion` off because a NavMeshAgent or a CharacterController owns each character's position, so `DeathAnimationPlayer` turns root motion on for the duration of the death clip and restores the Animator's flag and local transform in `Stop()`. Root motion there moves the Animator's own transform, a child of the character root, so it never fights the agent or controller. If the clip is ever reimported with "Root Transform Position (Y) > Bake Into Pose" enabled, this handling becomes redundant rather than wrong.

## Death and animation ownership

Death is a latch, and it is held in two places at once. `EnemyAnimationController`/`PlayerAnimationController` raise an `isDead` flag that mutes every Animator parameter write, and `DeathAnimationPlayer` starts a Playable graph that takes over the Animator's output completely, so the AnimatorController's locomotion blend tree and upper body layer stop driving the rig. That is correct for a corpse, but it means the two must be released together: anything that decides a character is alive again - `EnemyBrain.ConfigureExecution` recomputing `isDead` from a Health that has since initialized, for example - has to call `ClearDeath()`, which both lowers the flag and calls `DeathAnimationPlayer.Stop()`. Releasing only one leaves a character whose AI runs normally while its body stays in the death pose and never animates. `Health` guards the other end of the same problem by reporting `IsAlive` until it has initialized, so an observer running in `OnEnable` cannot read a full-health character as dead before `Awake()` has run.

## Performance direction

`TickScheduler` is the shared low-frequency budget and is now actually used: `EnemyBrain` registers its perception step (sensors, memory decay, perception-driven transitions) with it, so the cost of AI thinking is one interval for the whole scene rather than a timer per component. The state machine deliberately stays per frame - it is cheap and it drives facing and destination updates, which visibly snap at 10 Hz. `VisionSensor` keeps its own scan interval on top; the scheduler is the global budget, the sensor interval is that one sensor's rate.

Optimization is evidence-driven. Sensor frequency, tactical decision frequency, pooling and allocation changes should be made after profiling. `TickScheduler` is provided as the first boundary for moving expensive logic away from every-frame updates.
