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

WeaponController
 -> NoiseEventHub
 -> HearingSensor
```

`WeaponDefinition` contains immutable configuration. `WeaponRuntime` contains mutable ammo, fire cooldown and reload state.

## Status effects

`StatusEffectDefinition` describes duration and stacking rules. `StatusEffectInstance` owns runtime duration/stack state. `StatusEffectController` owns active effects. Later, modifiers such as Slow and Suppression will feed movement and tactical scoring without hard-coded checks spread across unrelated classes.

## Inventory/equipment

`ItemDefinition` is immutable item data. `ItemInstance` is runtime identity/state. `InventoryController` owns items. `EquipmentController` owns the primary/secondary slots. UI must call these APIs instead of editing collections directly.

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

The current foundation already separates participants from persistence. Migration and validation are the next implementation layer.

## Camera and animation

Camera owns Explore/Aim/shoulder/collision/obstruction responsibilities. Animation controllers centralize Animator parameters so gameplay classes do not set Animator parameters across the codebase.

PrimeTween is allowed only for presentation smoothing such as UI and obstruction fading. It must not own AI state transitions, combat cooldowns, damage or save logic.

Text UI uses TextMeshPro (`TMP_Text`/`TextMeshProUGUI`), not legacy `UnityEngine.UI.Text`.

## Debugging

`AIDebugOverlay` displays state, tactical action scores and memory information. Scene gizmos visualize perception and remembered positions. Debug code observes runtime systems and does not become a gameplay dependency.

## Performance direction

Optimization is evidence-driven. Sensor frequency, tactical decision frequency, pooling and allocation changes should be made after profiling. `TickScheduler` is provided as the first boundary for moving expensive logic away from every-frame updates.
