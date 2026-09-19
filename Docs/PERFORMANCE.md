# Performance

Optimization in this project is evidence-driven. Nothing here is changed because it "looks
slow" - a change belongs in this document only once a Profiler capture shows the cost, and
the same capture is repeated afterwards.

## Status of this document

The allocation and physics-query audit below is **static analysis of the codebase** and is
complete. The Profiler tables are **not filled in yet**: they require running the Editor or a
player build, which has not been done as part of the audit. Do not treat an empty table as a
result.

## Method

Capture with the same scene, the same route and the same duration every time:

1. Open `Assets/_Project/Scenes/TacticalEcho_Sandbox.unity` and enter Play Mode.
2. Window > Analysis > Profiler, enable **Deep Profile** off, **GC Alloc** column on.
3. Walk the fixed route: spawn point -> down the walkway -> engage the enemy -> break line of
   sight -> let Search run to its timeout -> return to spawn. About 60 seconds.
4. Record from the Profiler: `PlayerLoop` ms median, `Scripts` ms median, GC Alloc per frame,
   and the worst single frame.
5. For a build capture, use a Development Build with Autoconnect Profiler; Editor numbers are
   indicative only and are always worse than a build.

Record the machine: this project has been developed on an Apple M4 with the Metal backend,
where the sandbox scene has been observed at roughly 52-56 FPS in the Editor Game view.

## Physics query audit

Every physics query in `Assets/_Project` was checked for the allocating variant.

| Call site | Query | Allocating? |
| --- | --- | --- |
| `WeaponController.TryFire` | `Physics.Raycast` (single) | No |
| `PlayerCameraController.UpdateCrosshair` | `Physics.Raycast` (single) | No |
| `VisionSensor` line of sight | `Physics.Raycast` (single) | No |
| `CameraObstructionHandler.ScanObstructions` | `Physics.RaycastNonAlloc` into a reused buffer | No |
| `CameraObstructionHandler.ResolveCameraPosition` | `Physics.SphereCastNonAlloc` into a reused buffer | No |
| `EnemyMovement` NavMesh sampling | `NavMesh.SamplePosition` | No |

`RaycastAll`, `SphereCastAll`, `OverlapSphere`, `OverlapBox` and `OverlapCapsule` - the
variants that allocate a fresh array per call - do not appear anywhere in `Assets/_Project`.
Keep it that way: the `NonAlloc` forms with a preallocated buffer are the house style.

One structural reduction was made for the same reason rather than for raw query cost:
`EnemyBrain` registers its perception step (the sensor raycasting) with `TickScheduler`, so
that work runs on the shared budget instead of every frame per enemy. The state machine stays
per frame because it is cheap and drives facing.

## Allocation audit

Checked across `Assets/_Project`: LINQ usage, allocating collection calls, array and
collection construction, and string building inside `Update`/`LateUpdate`/`FixedUpdate` and
the methods they call.

| Finding | Where | Status |
| --- | --- | --- |
| `System.Linq` in runtime code | nowhere | Clean |
| `new` collections or arrays per frame | nowhere in gameplay paths | Clean |
| `GetComponent*` / `FindObjectsByType` per frame | nowhere in gameplay paths | Clean |
| Per-frame string building for the ammo HUD | `PlayerCombatHud` | Clean - event driven off `AmmoChanged`, rebuilds only when ammo changes |
| Per-frame status text for the sandbox enemy label | `EnemyPerceptionDemoView` | Clean - guarded, rebuilds only when state, action, health or nav readiness changed |
| Per-frame `StringBuilder.ToString()` and TMP re-mesh | debug overlays | **Fixed** - `DebugOverlayBase` rebuilds on an interval (default 0.1 s) and skips the assignment when the content is unchanged |
| Reused buffers for physics results | `CameraObstructionHandler` | Clean - `RaycastHit[]` fields, never per call |
| Search sweep points | `SearchState` | Clean - the ring array is allocated once per state instance, not per search |

The only per-frame garbage found was in the debug overlays, which is worth calling out: a
debug tool that allocates every frame pollutes the profile it exists to help read.

## Profiler baseline

Fill in from a capture taken **before** any optimization pass, following the method above.

| Metric | Editor | Development build |
| --- | --- | --- |
| PlayerLoop median (ms) | | |
| Scripts median (ms) | | |
| GC Alloc per frame (B) | | |
| Worst frame (ms) | | |
| Enemies in scene | | |

## Profiler after optimization

Same scene, same route, same duration. A row only counts if the baseline row above is filled
in from the same machine and the same build type.

| Metric | Editor | Development build | Delta |
| --- | --- | --- | --- |
| PlayerLoop median (ms) | | | |
| Scripts median (ms) | | | |
| GC Alloc per frame (B) | | | |
| Worst frame (ms) | | | |

## Culling

Frustum culling is always on: Unity only renders what is inside the camera frustum, with no
setup required.

Occlusion culling - not drawing what is hidden behind other geometry - is **not active in the
sandbox**. `TacticalEcho_Sandbox.unity` carries an `m_OcclusionCullingData` reference inherited
from the Viking Village demo scene it was duplicated from, and that data records
`m_SceneGUID: 217f50a4b9fbfda41b4f6faee8d22d02`, which is the Viking Village scene, not this one
(`b1111111111111111111111111111111`). Unity validates that GUID, so the stale data is ignored.

To enable it: mark the static environment as Occluder Static / Occludee Static, then
Window > Rendering > Occlusion Culling > Bake. Measure before and after with the method above -
in an outdoor seaside scene the win is usually small, so it belongs in the Profiler tables
rather than being assumed.

## Isolating a hitch when the camera turns

A stutter that appears the moment new geometry comes into view has several possible causes
that look identical on screen. Isolate before changing anything - each step is one toggle and
one play session on the same route.

1. **Is it first-sight only?** Turn to a new area, then turn away and back several times. A
   hitch that happens once per area and never again in the same session is shader variant or
   pipeline-state compilation on first draw, not culling. Occlusion culling does not create
   that cost, it defers it: before the bake, geometry behind walls was still submitted and
   compiled early; after it, the compile happens at the moment of reveal. Confirm with a
   Development Build - Editor shader compilation is far worse than a build, and this class of
   hitch usually shrinks or disappears there. The real fix is a `ShaderVariantCollection`
   recorded while playing the route and added to Project Settings > Graphics > Preloaded
   Shaders.
2. **Is it occlusion culling itself?** Main Camera > uncheck **Occlusion Culling** and replay
   the route. If the hitch is unchanged, culling is not the cause.
3. **Is it the obstruction fade?** `CameraObstructionHandler` > uncheck **Enable Renderer
   Fade**. The fade assigns a `MaterialPropertyBlock`, which makes that renderer
   SRP-Batcher incompatible while it is faded, so a large environment mesh entering and
   leaving the fade set costs draw-call churn. The collision push stays active with the fade
   off, so this isolates the two halves of the component.
4. **Is it the camera collision push?** `CameraObstructionHandler` > uncheck **Push Camera Out
   Of Geometry**. One sphere cast per frame should not be measurable; if it is, the mask is
   catching far more colliders than intended.

Only after one of these changes the measurement does it belong in the tables below.

Note on the bake: Unity writes the new `m_OcclusionCullingData` reference and the scene GUID
into the scene file when the **scene is saved**. Baking without saving leaves the scene on disk
pointing at whatever it referenced before.

## Known cost centres to measure first

Ordered by expected cost, to be confirmed or rejected by the capture rather than assumed:

1. `VisionSensor` line-of-sight raycasts, multiplied by enemy count - now on the shared tick
   budget, so the budget interval is the tuning knob.
2. `CameraObstructionHandler` - two casts per frame plus a `MaterialPropertyBlock` write per
   tracked renderer.
3. `TacticalEvaluator` scoring - already self-throttled by its decision interval and switch
   threshold, so it should not appear high; if it does, the interval is wrong.
4. NavMesh pathfinding on `SetDestination` - Patrol and Search issue these on arrival and on
   dwell expiry, not per frame.
