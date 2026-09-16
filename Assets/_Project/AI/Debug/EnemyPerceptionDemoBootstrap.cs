using TacticalEcho.AI.Brain;
using TacticalEcho.AI.Memory;
using TacticalEcho.AI.Navigation;
using TacticalEcho.AI.Perception;
using TacticalEcho.AI.States;
using TacticalEcho.Character.Player;
using TacticalEcho.Combat.Damage;
using TacticalEcho.Combat.Health;
using TacticalEcho.Combat.Weapons;
using TMPro;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;

namespace TacticalEcho.AI.Debugging
{
    /// <summary>
    /// Runtime-only debug harness for the sandbox scene.
    /// It reuses Kaia's existing player visual as a visible enemy so perception can be tested
    /// before a dedicated enemy art/prefab is available.
    /// </summary>
    public static class EnemyPerceptionDemoBootstrap
    {
        private const string SandboxSceneName = "TacticalEcho_Sandbox";
        private const string DemoEnemyName = "Enemy_PerceptionTest_Kaia";
        private const string RuntimeNavMeshName = "Runtime_NavMeshSurface_AI_Test";
        private const float EnemyNavMeshSnapDistance = 8f;

        private static readonly Vector3 RuntimeNavMeshSize = new(120f, 30f, 120f);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void SpawnDemoEnemy()
        {
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != SandboxSceneName)
            {
                return;
            }

            if (Object.FindFirstObjectByType<EnemyPerceptionDemoView>() != null)
            {
                return;
            }

            PlayerVisualController playerVisual = Object.FindFirstObjectByType<PlayerVisualController>();
            if (playerVisual == null || playerVisual.VisualRoot == null)
            {
                Debug.LogWarning("[AI Perception Demo] Player visual was not found; demo enemy was not spawned.");
                return;
            }

            Transform player = playerVisual.transform;
            bool navMeshReady = EnsureNavigationAvailable(player);

            GameObject enemy = new(DemoEnemyName);
            enemy.transform.position = ResolveSpawnPosition(player);

            Vector3 facingAwayFromPlayer = enemy.transform.position - player.position;
            facingAwayFromPlayer.y = 0f;
            if (facingAwayFromPlayer.sqrMagnitude <= 0.001f)
            {
                facingAwayFromPlayer = -player.forward;
            }

            enemy.transform.rotation = Quaternion.LookRotation(facingAwayFromPlayer.normalized, Vector3.up);

            GameObject visualClone = Object.Instantiate(playerVisual.VisualRoot.gameObject, enemy.transform, false);
            visualClone.name = "Visual_Kaia_Enemy";
            visualClone.transform.localPosition = Vector3.zero;
            visualClone.transform.localRotation = Quaternion.identity;
            visualClone.transform.localScale = Vector3.one;

            foreach (WeaponController clonedWeapon in visualClone.GetComponentsInChildren<WeaponController>(true))
            {
                clonedWeapon.enabled = false;
            }

            Health health = enemy.AddComponent<Health>();
            CreateMeshAlignedHitZones(visualClone, enemy);

            EnemyMovement movement = enemy.AddComponent<EnemyMovement>();
            movement.ConfigureAgent(
                speed: 3.5f,
                acceleration: 14f,
                radius: 0.28f,
                height: 1.7f,
                updateRotation: false);

            bool enemyOnNavMesh = navMeshReady && movement.TrySnapToNavMesh(EnemyNavMeshSnapDistance);

            HearingSensor hearing = enemy.AddComponent<HearingSensor>();
            EnemyMemory memory = enemy.AddComponent<EnemyMemory>();

            Transform eyeOrigin = new GameObject("EyeOrigin").transform;
            eyeOrigin.SetParent(enemy.transform, false);
            eyeOrigin.localPosition = new Vector3(0f, 1.55f, 0.05f);

            VisionSensor vision = enemy.AddComponent<VisionSensor>();
            vision.Configure(eyeOrigin, player, Physics.DefaultRaycastLayers, Physics.DefaultRaycastLayers);

            EnemyBrain brain = enemy.AddComponent<EnemyBrain>();
            brain.ConfigurePerception(vision, hearing, memory);
            brain.ConfigureExecution(movement, null, health);

            EnemyAIGizmos gizmos = enemy.AddComponent<EnemyAIGizmos>();
            gizmos.Configure(brain);

            CreateStatusLabel(enemy.transform, out TMP_Text statusText, out Transform labelRoot);

            EnemyPerceptionDemoView view = enemy.AddComponent<EnemyPerceptionDemoView>();
            view.Configure(brain, health, statusText, labelRoot);

            if (!enemyOnNavMesh)
            {
                Debug.LogWarning(
                    "[AI Perception Demo] Enemy could not attach to a NavMesh. " +
                    "The perception test still runs, but movement will stay disabled. " +
                    "Verify that nearby walkable ground has a Collider and is included in the runtime NavMesh volume.");
            }

            Debug.Log(
                "[AI Perception Demo] Spawned Enemy_PerceptionTest_Kaia with mesh-aligned humanoid hit zones. " +
                $"Navigation={(enemyOnNavMesh ? "READY" : "NOT READY")}. " +
                "Head 2.0x, torso 1.0x, arms 0.75x, legs 0.65x.");
        }

        private static bool EnsureNavigationAvailable(Transform player)
        {
            if (HasUsableNavMesh())
            {
                Debug.Log("[AI Perception Demo] Using NavMesh already available in the scene.");
                return true;
            }

            NavMeshModifier playerModifier = player.GetComponent<NavMeshModifier>();
            bool createdModifier = playerModifier == null;
            bool previousIgnore = false;
            bool previousApplyToChildren = false;

            if (createdModifier)
            {
                playerModifier = player.gameObject.AddComponent<NavMeshModifier>();
            }
            else
            {
                previousIgnore = playerModifier.ignoreFromBuild;
                previousApplyToChildren = playerModifier.applyToChildren;
            }

            playerModifier.ignoreFromBuild = true;
            playerModifier.applyToChildren = true;

            GameObject surfaceObject = new(RuntimeNavMeshName);
            surfaceObject.hideFlags = HideFlags.DontSave;
            surfaceObject.transform.position = player.position;

            NavMeshSurface surface = surfaceObject.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.Volume;
            surface.size = RuntimeNavMeshSize;
            surface.center = new Vector3(0f, 5f, 0f);
            surface.layerMask = BuildNavigationLayerMask();
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surface.defaultArea = NavMesh.GetAreaFromName("Walkable");
            surface.ignoreNavMeshAgent = true;
            surface.ignoreNavMeshObstacle = true;
            surface.overrideTileSize = true;
            surface.tileSize = 128;

            bool buildSucceeded = false;
            try
            {
                surface.BuildNavMesh();
                buildSucceeded = HasUsableNavMesh();
            }
            catch (System.Exception exception)
            {
                Debug.LogWarning($"[AI Perception Demo] Runtime NavMesh build failed: {exception.Message}");
            }
            finally
            {
                if (createdModifier)
                {
                    Object.Destroy(playerModifier);
                }
                else
                {
                    playerModifier.ignoreFromBuild = previousIgnore;
                    playerModifier.applyToChildren = previousApplyToChildren;
                }
            }

            if (!buildSucceeded)
            {
                Object.Destroy(surfaceObject);
                Debug.LogWarning(
                    "[AI Perception Demo] No usable NavMesh was found or generated. " +
                    "Runtime baking uses nearby Physics Colliders, so the walkable floor must have a Collider.");
                return false;
            }

            NavMeshTriangulation triangulation = NavMesh.CalculateTriangulation();
            Debug.Log(
                $"[AI Perception Demo] Runtime NavMesh generated around the player " +
                $"({triangulation.vertices.Length} vertices, volume {RuntimeNavMeshSize.x:0}x{RuntimeNavMeshSize.z:0}).");
            return true;
        }

        private static LayerMask BuildNavigationLayerMask()
        {
            int mask = ~0;
            ExcludeLayer(ref mask, "Ignore Raycast");
            ExcludeLayer(ref mask, "Water");
            ExcludeLayer(ref mask, "UI");
            ExcludeLayer(ref mask, "Reflection_Probes");
            ExcludeLayer(ref mask, "AccessibleVolume");
            ExcludeLayer(ref mask, "PostProcessing");
            return mask;
        }

        private static void ExcludeLayer(ref int mask, string layerName)
        {
            int layer = LayerMask.NameToLayer(layerName);
            if (layer >= 0)
            {
                mask &= ~(1 << layer);
            }
        }

        private static bool HasUsableNavMesh()
        {
            NavMeshTriangulation triangulation = NavMesh.CalculateTriangulation();
            return triangulation.vertices != null && triangulation.vertices.Length >= 3;
        }

        private static void CreateMeshAlignedHitZones(GameObject visualClone, GameObject enemyRoot)
        {
            Animator animator = visualClone.GetComponentInChildren<Animator>(true);
            if (animator == null || !animator.isHuman)
            {
                Debug.LogWarning("[AI Perception Demo] Humanoid Animator not found; using fallback body collider.");
                CapsuleCollider fallback = enemyRoot.AddComponent<CapsuleCollider>();
                fallback.height = 1.7f;
                fallback.radius = 0.31f;
                fallback.center = new Vector3(0f, 0.85f, 0f);
                DamageHitZone zone = enemyRoot.AddComponent<DamageHitZone>();
                zone.Configure(DamageHitZoneType.Torso, 1f);
                return;
            }

            CreateSphereZone(animator.GetBoneTransform(HumanBodyBones.Head), "HitZone_Head", DamageHitZoneType.Head, 2f, 0.13f);

            CreateCapsuleZone(
                animator.GetBoneTransform(HumanBodyBones.Chest),
                animator.GetBoneTransform(HumanBodyBones.Hips),
                "HitZone_Torso",
                DamageHitZoneType.Torso,
                1f,
                0.22f);

            CreateCapsuleZone(animator.GetBoneTransform(HumanBodyBones.LeftUpperArm), animator.GetBoneTransform(HumanBodyBones.LeftLowerArm), "HitZone_LeftUpperArm", DamageHitZoneType.Arm, 0.75f, 0.085f);
            CreateCapsuleZone(animator.GetBoneTransform(HumanBodyBones.LeftLowerArm), animator.GetBoneTransform(HumanBodyBones.LeftHand), "HitZone_LeftLowerArm", DamageHitZoneType.Arm, 0.75f, 0.075f);
            CreateCapsuleZone(animator.GetBoneTransform(HumanBodyBones.RightUpperArm), animator.GetBoneTransform(HumanBodyBones.RightLowerArm), "HitZone_RightUpperArm", DamageHitZoneType.Arm, 0.75f, 0.085f);
            CreateCapsuleZone(animator.GetBoneTransform(HumanBodyBones.RightLowerArm), animator.GetBoneTransform(HumanBodyBones.RightHand), "HitZone_RightLowerArm", DamageHitZoneType.Arm, 0.75f, 0.075f);

            CreateCapsuleZone(animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg), animator.GetBoneTransform(HumanBodyBones.LeftLowerLeg), "HitZone_LeftThigh", DamageHitZoneType.Leg, 0.65f, 0.11f);
            CreateCapsuleZone(animator.GetBoneTransform(HumanBodyBones.LeftLowerLeg), animator.GetBoneTransform(HumanBodyBones.LeftFoot), "HitZone_LeftCalf", DamageHitZoneType.Leg, 0.65f, 0.09f);
            CreateCapsuleZone(animator.GetBoneTransform(HumanBodyBones.RightUpperLeg), animator.GetBoneTransform(HumanBodyBones.RightLowerLeg), "HitZone_RightThigh", DamageHitZoneType.Leg, 0.65f, 0.11f);
            CreateCapsuleZone(animator.GetBoneTransform(HumanBodyBones.RightLowerLeg), animator.GetBoneTransform(HumanBodyBones.RightFoot), "HitZone_RightCalf", DamageHitZoneType.Leg, 0.65f, 0.09f);
        }

        private static void CreateSphereZone(
            Transform bone,
            string name,
            DamageHitZoneType zoneType,
            float multiplier,
            float radius)
        {
            if (bone == null)
            {
                return;
            }

            GameObject zoneObject = new(name);
            zoneObject.hideFlags = HideFlags.HideInHierarchy | HideFlags.DontSave;
            zoneObject.transform.SetParent(bone, false);
            zoneObject.transform.localPosition = Vector3.zero;
            zoneObject.transform.localRotation = Quaternion.identity;
            zoneObject.transform.localScale = Vector3.one;

            SphereCollider collider = zoneObject.AddComponent<SphereCollider>();
            collider.radius = radius;

            DamageHitZone zone = zoneObject.AddComponent<DamageHitZone>();
            zone.Configure(zoneType, multiplier);
        }

        private static void CreateCapsuleZone(
            Transform fromBone,
            Transform toBone,
            string name,
            DamageHitZoneType zoneType,
            float multiplier,
            float radius)
        {
            if (fromBone == null || toBone == null)
            {
                return;
            }

            Vector3 endLocal = fromBone.InverseTransformPoint(toBone.position);
            float distance = endLocal.magnitude;
            if (distance <= 0.001f)
            {
                return;
            }

            GameObject zoneObject = new(name);
            zoneObject.hideFlags = HideFlags.HideInHierarchy | HideFlags.DontSave;
            zoneObject.transform.SetParent(fromBone, false);
            zoneObject.transform.localPosition = endLocal * 0.5f;
            zoneObject.transform.localRotation = Quaternion.FromToRotation(Vector3.up, endLocal.normalized);
            zoneObject.transform.localScale = Vector3.one;

            CapsuleCollider collider = zoneObject.AddComponent<CapsuleCollider>();
            collider.direction = 1;
            collider.radius = radius;
            collider.height = Mathf.Max(radius * 2f, distance + radius * 1.5f);
            collider.center = Vector3.zero;

            DamageHitZone zone = zoneObject.AddComponent<DamageHitZone>();
            zone.Configure(zoneType, multiplier);
        }

        private static Vector3 ResolveSpawnPosition(Transform player)
        {
            Vector3 desired = player.position + player.forward * 9f + player.right * 2.5f;
            Vector3 rayOrigin = desired + Vector3.up * 15f;

            if (Physics.Raycast(
                    rayOrigin,
                    Vector3.down,
                    out RaycastHit groundHit,
                    40f,
                    Physics.DefaultRaycastLayers,
                    QueryTriggerInteraction.Ignore))
            {
                desired.y = groundHit.point.y;
            }
            else
            {
                desired.y = player.position.y;
            }

            if (NavMesh.SamplePosition(desired, out NavMeshHit navMeshHit, EnemyNavMeshSnapDistance, NavMesh.AllAreas))
            {
                desired = navMeshHit.position;
            }

            return desired;
        }

        private static void CreateStatusLabel(Transform parent, out TMP_Text statusText, out Transform labelRoot)
        {
            GameObject canvasObject = new("AI_StatusCanvas", typeof(RectTransform));
            canvasObject.transform.SetParent(parent, false);

            RectTransform canvasRect = canvasObject.GetComponent<RectTransform>();
            canvasRect.localPosition = new Vector3(0f, 2.15f, 0f);
            canvasRect.localRotation = Quaternion.identity;
            canvasRect.localScale = Vector3.one * 0.005f;
            canvasRect.sizeDelta = new Vector2(380f, 140f);

            Canvas canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 100;

            GameObject textObject = new("Status", typeof(RectTransform));
            textObject.transform.SetParent(canvasObject.transform, false);

            RectTransform textRect = textObject.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            TextMeshProUGUI text = textObject.AddComponent<TextMeshProUGUI>();
            text.text = "AI TEST\nPATROL\nNAV CHECK";
            text.fontSize = 26f;
            text.alignment = TextAlignmentOptions.Center;
            text.enableWordWrapping = false;
            text.raycastTarget = false;
            text.color = Color.white;

            statusText = text;
            labelRoot = canvasObject.transform;
        }
    }

    public sealed class EnemyPerceptionDemoView : MonoBehaviour
    {
        [SerializeField, Min(0.01f)] private float turnSharpness = 8f;

        private EnemyBrain brain;
        private Health health;
        private TMP_Text statusText;
        private Transform labelRoot;
        private Camera mainCamera;
        private EnemyStateId previousState = (EnemyStateId)(-1);
        private int previousHealth = -1;
        private bool previousNavigationReady;
        private bool hasPreviousNavigationState;

        public void Configure(EnemyBrain newBrain, Health newHealth, TMP_Text newStatusText, Transform newLabelRoot)
        {
            brain = newBrain;
            health = newHealth;
            statusText = newStatusText;
            labelRoot = newLabelRoot;
            RefreshStatus(force: true);
        }

        private void Update()
        {
            if (brain == null)
            {
                return;
            }

            RotateTowardPerceivedPosition();
            FaceStatusTowardCamera();
            RefreshStatus(force: false);
        }

        private void RotateTowardPerceivedPosition()
        {
            Vector3 targetPosition;

            if (brain.CurrentState == EnemyStateId.Combat && brain.Vision != null && brain.Vision.VisibleTarget != null)
            {
                targetPosition = brain.Vision.VisibleTarget.position;
            }
            else if ((brain.CurrentState == EnemyStateId.Investigate || brain.CurrentState == EnemyStateId.Search)
                     && brain.Memory != null
                     && brain.Memory.HasKnownPosition)
            {
                targetPosition = brain.Memory.LastKnownPosition;
            }
            else
            {
                return;
            }

            Vector3 direction = targetPosition - transform.position;
            direction.y = 0f;
            if (direction.sqrMagnitude <= 0.001f)
            {
                return;
            }

            Quaternion desiredRotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
            float t = 1f - Mathf.Exp(-turnSharpness * Time.deltaTime);
            transform.rotation = Quaternion.Slerp(transform.rotation, desiredRotation, t);
        }

        private void FaceStatusTowardCamera()
        {
            if (labelRoot == null)
            {
                return;
            }

            if (mainCamera == null)
            {
                mainCamera = Camera.main;
            }

            if (mainCamera == null)
            {
                return;
            }

            Vector3 direction = labelRoot.position - mainCamera.transform.position;
            if (direction.sqrMagnitude > 0.001f)
            {
                labelRoot.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
            }
        }

        private void RefreshStatus(bool force)
        {
            if (statusText == null || brain == null)
            {
                return;
            }

            int currentHealth = health != null ? Mathf.CeilToInt(health.Current) : 0;
            bool navigationReady = brain.Movement != null && brain.Movement.IsOnNavMesh;
            if (!force
                && previousState == brain.CurrentState
                && previousHealth == currentHealth
                && hasPreviousNavigationState
                && previousNavigationReady == navigationReady)
            {
                return;
            }

            previousState = brain.CurrentState;
            previousHealth = currentHealth;
            previousNavigationReady = navigationReady;
            hasPreviousNavigationState = true;

            string stateDescription;
            Color stateColor;

            switch (brain.CurrentState)
            {
                case EnemyStateId.Investigate:
                    stateDescription = "INVESTIGATE - HEARD SHOT";
                    stateColor = Color.yellow;
                    break;
                case EnemyStateId.Combat:
                    stateDescription = "COMBAT - SEES PLAYER";
                    stateColor = new Color(1f, 0.25f, 0.2f, 1f);
                    break;
                case EnemyStateId.Search:
                    stateDescription = "SEARCH - LAST KNOWN";
                    stateColor = Color.cyan;
                    break;
                case EnemyStateId.Dead:
                    stateDescription = "DEAD";
                    stateColor = Color.gray;
                    break;
                default:
                    stateDescription = "PATROL - NO CONTACT";
                    stateColor = Color.white;
                    break;
            }

            string healthLine = health != null
                ? $"HP {health.Current:0} / {health.Max:0}"
                : string.Empty;
            string navigationLine = navigationReady ? "NAV READY" : "NAV NOT READY";

            statusText.text = $"AI TEST\n{stateDescription}\n{navigationLine}\n{healthLine}";
            statusText.color = stateColor;
        }
    }
}
