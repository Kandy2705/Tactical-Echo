# Class diagrams

Five UML class diagrams, generated straight from the current source under `Assets/_Project/` —
one per domain covered in the presentation script, plus one domain-level overview that ties them
together. Each `.svg` has its `.dot` source next to it in this folder; regenerate after a
structural change with `dot -Tsvg <name>.dot -o <name>.svg` (Graphviz).

## Overview — how the four domains connect

![Domain overview](diagrams/architecture-classes-overview.svg)

No methods here on purpose — this is the "which domain is allowed to depend on which" map.
`Core/` (the state machine engine and the event hub) is the only thing every domain may depend
on; `AI` depends on `Combat` and `Camera & Animation` for execution (movement, weapon, animation)
but never the other way around; `DebugTools` only ever reads.

## AI

![AI class diagram](diagrams/ai-class-diagram.svg)

`EnemyBrain` is a Facade: it owns every AI sub-system by composition/aggregation and keeps its
own `StateMachine<EnemyStateId>` (from `Core/StateMachine`), but never contains decision logic
itself. `TacticalEvaluator` scores `ITacticalAction` — six concrete Strategy implementations, one
class each, added without touching the evaluator. `EnemyStateBase` states implement `IState` and
each own exactly one behaviour (Patrol, Investigate, Combat, Search, Retreat, Dead).

## Combat

![Combat class diagram](diagrams/combat-class-diagram.svg)

One damage pipeline (`IDamageable` → `DamageSystem` → `Health`) serves Player and Enemy alike —
bullets and status-effect damage-over-time both end up calling the same `TryApply`.
`WeaponController` creates its own `WeaponRuntime` (composition) and reads `WeaponDefinition` /
`WeaponAudioProfile` as data assets, so a new weapon is a new asset, not new code.

## Camera & Animation

![Camera & Animation class diagram](diagrams/camera-animation-class-diagram.svg)

Three independent controllers — camera, player animation, enemy animation — each read from
`Combat` / `Character` where they need to, but never from each other. `DeathAnimationPlayer` is
the one static utility both animation controllers call so the death clip only has one code path.

## DebugTools

![DebugTools class diagram](diagrams/debugtools-class-diagram.svg)

Template Method: `DebugOverlayBase.LateUpdate()` is fixed (throttled rebuild, draw), every
concrete overlay only implements `OverlayName` / `BuildText()` plus an `Observe(...)` entry point
into whichever system it displays — adding a new overlay never touches the base class.
