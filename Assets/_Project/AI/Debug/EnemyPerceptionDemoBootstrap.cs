using TacticalEcho.AI.Brain;
using TacticalEcho.AI.Cover;
using TacticalEcho.AI.Memory;
using TacticalEcho.AI.Navigation;
using TacticalEcho.AI.Perception;
using TacticalEcho.AI.TacticalActions;
using TacticalEcho.AnimationSystem.Runtime;
using TacticalEcho.Character.Player;
using TacticalEcho.Combat.Damage;
using TacticalEcho.Combat.Health;
using TacticalEcho.Combat.Weapons;
using TMPro;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

namespace TacticalEcho.AI.Debugging
{
    /// <summary>
    /// Sandbox-only perception/tactical demo owner.
    /// In the Editor it authors a visible enemy prefab/scene instance for debugging.
    /// At runtime it binds that authored instance to the current player and keeps demo-only
    /// perception/navigation/tactical wiring isolated from production enemy setup.
    /// </summary>
    public static class EnemyPerceptionDemoBootstrap
    {
        private const string SandboxSceneName = "TacticalEcho_Sandbox";
        private const string EnemyParentName = "Enemies";
        private const string EnemyInstanceName = "Enemy_01";
        private const string DemoEnemyName = "Enemy_PerceptionTest_Kaia";
        private const string RuntimeNavMeshName = "Runtime_NavMeshSurface_AI_Test";
        private const float EnemyNavMeshSnapDistance = 8f;

#if UNITY_EDITOR
        private const string EnemyPrefabFolder = "Assets/_Project/Prefabs/Enemies";
        private const string EnemyPrefabPath = EnemyPrefabFolder + "/Enemy_PerceptionTest_Kaia.prefab";
        private const string PlayerPrefabPath = "Assets/_Project/Prefabs/Player/Player_Kaia.prefab";
        private const string RifleDefinitionPath = "Assets/_Project/Combat/Weapons/Definitions/Rifle_HK416.asset";
#endif

        private static readonly Vector3 RuntimeNavMeshSize = new(120f, 30f, 120f);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InitializeDemoEnemy()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (!IsSandboxScene(scene)) return;

            EnemyPerceptionDemoView view = FindDemoView(scene);
            if (view == null)
            {
                Debug.LogWarning("[AI Perception Demo] No scene-authored Enemy_01 was found. Open TacticalEcho_Sandbox in the Editor or use Tactical Echo/AI/Rebuild Sandbox Perception Enemy.");
                return;
            }

            PlayerVisualController playerVisual = FindPlayerVisual(scene);
            if (playerVisual == null || playerVisual.VisualRoot == null)
            {
                Debug.LogWarning("[AI Perception Demo] Player visual was not found; demo enemy was not initialized.");
                return;
            }

            GameObject enemy = view.gameObject;
            EnemyMovement movement = enemy.GetComponent<EnemyMovement>();
            HearingSensor hearing = enemy.GetComponent<HearingSensor>();
            EnemyMemory memory = enemy.GetComponent<EnemyMemory>();
            VisionSensor vision = enemy.GetComponent<VisionSensor>();
            EnemyBrain brain = enemy.GetComponent<EnemyBrain>();
            Health health = enemy.GetComponent<Health>();
            EnemyAIGizmos gizmos = enemy.GetComponent<EnemyAIGizmos>();
            EnemyAnimationController animationController = enemy.GetComponent<EnemyAnimationController>();
            TacticalEvaluator tacticalEvaluator = enemy.GetComponent<TacticalEvaluator>();
            CoverEvaluator coverEvaluator = enemy.GetComponent<CoverEvaluator>();
            WeaponController weapon = enemy.GetComponentInChildren<WeaponController>(true);
            Transform eyeOrigin = enemy.transform.Find("EyeOrigin");

            if (movement == null || hearing == null || memory == null || vision == null || brain == null || health == null || eyeOrigin == null)
            {
                Debug.LogError("[AI Perception Demo] Scene-authored enemy setup is incomplete. Use Tactical Echo/AI/Rebuild Sandbox Perception Enemy to repair the sandbox demo.", enemy);
                return;
            }

            Transform player = playerVisual.transform;
            vision.Configure(eyeOrigin, player, Physics.DefaultRaycastLayers, Physics.DefaultRaycastLayers);

            if (coverEvaluator != null)
            {
                coverEvaluator.SetCoverPoints(FindCoverPoints(scene));
            }

            brain.ConfigurePerception(vision, hearing, memory);
            brain.ConfigureExecution(movement, weapon, health, animationController);
            brain.ConfigureDecision(tacticalEvaluator, coverEvaluator);
            gizmos?.Configure(brain);

            Transform labelRoot = enemy.transform.Find("AI_StatusCanvas");
            TMP_Text statusText = labelRoot != null ? labelRoot.GetComponentInChildren<TMP_Text>(true) : null;
            view.Configure(brain, health, statusText, labelRoot);

            bool navMeshReady = EnsureNavigationAvailable(player);
            bool enemyOnNavMesh = navMeshReady && movement.TrySnapToNavMesh(EnemyNavMeshSnapDistance);

            if (!enemyOnNavMesh)
            {
                Debug.LogWarning("[AI Perception Demo] Enemy could not attach to a NavMesh. The authored enemy remains visible/debuggable, but movement will stay disabled. Verify nearby walkable ground and runtime NavMesh inputs.", enemy);
            }

            Debug.Log($"[AI Perception Demo] Initialized scene-authored Enemy_01. Navigation={(enemyOnNavMesh ? "READY" : "NOT READY")}. Tactical={(tacticalEvaluator != null ? "READY" : "NOT READY")}. Weapon={(weapon != null ? "READY" : "NOT READY")}. Head 2.0x, torso 1.0x, arms 0.75x, legs 0.65x.", enemy);
        }

        private static CoverPoint[] FindCoverPoints(Scene scene)
        {
            CoverPoint[] allPoints = Object.FindObjectsByType<CoverPoint>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            System.Collections.Generic.List<CoverPoint> scenePoints = new();
            foreach (CoverPoint point in allPoints)
            {
                if (point != null && point.gameObject.scene == scene) scenePoints.Add(point);
            }
            return scenePoints.ToArray();
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

            if (createdModifier) playerModifier = player.gameObject.AddComponent<NavMeshModifier>();
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
                if (createdModifier) Object.Destroy(playerModifier);
                else
                {
                    playerModifier.ignoreFromBuild = previousIgnore;
                    playerModifier.applyToChildren = previousApplyToChildren;
                }
            }

            if (!buildSucceeded)
            {
                Object.Destroy(surfaceObject);
                Debug.LogWarning("[AI Perception Demo] No usable NavMesh was found or generated. Runtime baking uses nearby Physics Colliders, so the walkable floor must have a Collider.");
                return false;
            }

            NavMeshTriangulation triangulation = NavMesh.CalculateTriangulation();
            Debug.Log($"[AI Perception Demo] Runtime NavMesh generated around the player ({triangulation.vertices.Length} vertices, volume {RuntimeNavMeshSize.x:0}x{RuntimeNavMeshSize.z:0}).");
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
            ExcludeLayer(ref mask, DamageHitZone.HitZoneLayerName);
            return mask;
        }

        private static void ExcludeLayer(ref int mask, string layerName)
        {
            int layer = LayerMask.NameToLayer(layerName);
            if (layer >= 0) mask &= ~(1 << layer);
        }

        private static bool HasUsableNavMesh()
        {
            NavMeshTriangulation triangulation = NavMesh.CalculateTriangulation();
            return triangulation.vertices != null && triangulation.vertices.Length >= 3;
        }

        private static bool IsSandboxScene(Scene scene) => scene.IsValid() && scene.isLoaded && scene.name == SandboxSceneName;

        private static PlayerVisualController FindPlayerVisual(Scene scene)
        {
            PlayerVisualController[] players = Object.FindObjectsByType<PlayerVisualController>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            foreach (PlayerVisualController player in players)
            {
                if (player != null && player.gameObject.scene == scene) return player;
            }
            return null;
        }

        private static EnemyPerceptionDemoView FindDemoView(Scene scene)
        {
            EnemyPerceptionDemoView[] views = Object.FindObjectsByType<EnemyPerceptionDemoView>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (EnemyPerceptionDemoView view in views)
            {
                if (view != null && view.gameObject.scene == scene) return view;
            }
            return null;
        }

#if UNITY_EDITOR
        [InitializeOnLoadMethod]
        private static void RegisterEditorAuthoring()
        {
            EditorApplication.delayCall -= EnsureCurrentSandboxAuthoring;
            EditorApplication.delayCall += EnsureCurrentSandboxAuthoring;
            EditorSceneManager.sceneOpened -= OnEditorSceneOpened;
            EditorSceneManager.sceneOpened += OnEditorSceneOpened;
        }

        [MenuItem("Tactical Echo/AI/Rebuild Sandbox Perception Enemy")]
        private static void RebuildSandboxEnemyAuthoring()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (!IsSandboxScene(scene))
            {
                Debug.LogWarning($"[AI Perception Demo] Open {SandboxSceneName} before rebuilding the demo enemy.");
                return;
            }

            DeleteSceneEnemies(scene);
            if (AssetDatabase.LoadAssetAtPath<GameObject>(EnemyPrefabPath) != null) AssetDatabase.DeleteAsset(EnemyPrefabPath);
            EnsureEnemyPrefab(forceRebuild: true);
            EnsureEnemyInstance(scene);
            EditorSceneManager.SaveScene(scene);
            Selection.activeGameObject = FindSceneEnemy(scene);
            Debug.Log($"[AI Perception Demo] Rebuilt {EnemyPrefabPath} and placed {EnemyInstanceName} in {SandboxSceneName}.");
        }

        private static void OnEditorSceneOpened(Scene scene, OpenSceneMode mode)
        {
            if (!EditorApplication.isPlayingOrWillChangePlaymode && IsSandboxScene(scene)) EditorApplication.delayCall += () => EnsureSandboxAuthoring(scene);
        }

        private static void EnsureCurrentSandboxAuthoring()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            Scene scene = SceneManager.GetActiveScene();
            if (IsSandboxScene(scene)) EnsureSandboxAuthoring(scene);
        }

        private static void EnsureSandboxAuthoring(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded || EditorApplication.isPlayingOrWillChangePlaymode) return;

            bool sceneWasDirty = scene.isDirty;
            bool prefabRebuilt = EnsureEnemyPrefab(forceRebuild: false);
            if (prefabRebuilt) DeleteSceneEnemies(scene);
            RemoveDuplicateSceneEnemies(scene);

            bool instanceCreated = EnsureEnemyInstance(scene);
            if (!instanceCreated) return;

            if (!sceneWasDirty) EditorSceneManager.SaveScene(scene);
            else Debug.Log("[AI Perception Demo] Enemy_01 was added to the sandbox, but the scene already had unsaved changes. Save the scene manually when those changes are ready.");
        }

        private static bool EnsureEnemyPrefab(bool forceRebuild)
        {
            EnsureFolderExists(EnemyPrefabFolder);
            GameObject existingPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(EnemyPrefabPath);
            bool needsRebuild = forceRebuild || existingPrefab == null || !IsEnemyPrefabValid(existingPrefab);
            if (!needsRebuild) return false;
            if (existingPrefab != null) AssetDatabase.DeleteAsset(EnemyPrefabPath);

            GameObject enemyRoot = BuildEnemyTemplate();
            try
            {
                GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(enemyRoot, EnemyPrefabPath);
                if (savedPrefab == null)
                {
                    Debug.LogError($"[AI Perception Demo] Failed to save enemy prefab at {EnemyPrefabPath}.");
                    return false;
                }
                AssetDatabase.SaveAssets();
                return true;
            }
            finally
            {
                Object.DestroyImmediate(enemyRoot);
            }
        }

        private static bool IsEnemyPrefabValid(GameObject prefab)
        {
            return prefab != null
                && prefab.GetComponent<EnemyBrain>() != null
                && prefab.GetComponent<EnemyMovement>() != null
                && prefab.GetComponent<VisionSensor>() != null
                && prefab.GetComponent<HearingSensor>() != null
                && prefab.GetComponent<EnemyMemory>() != null
                && prefab.GetComponent<Health>() != null
                && prefab.GetComponent<NavMeshAgent>() is { enabled: false }
                && prefab.GetComponent<EnemyAIGizmos>() != null
                && prefab.GetComponent<TacticalEvaluator>() != null
                && prefab.GetComponent<CoverEvaluator>() != null
                && prefab.GetComponentInChildren<WeaponController>(true) != null
                && prefab.GetComponent<EnemyPerceptionDemoView>() is { HasAuthoringContract: true }
                && prefab.transform.Find("EyeOrigin") != null
                && prefab.transform.Find("AI_StatusCanvas") != null;
        }

        private static GameObject BuildEnemyTemplate()
        {
            GameObject enemy = new(DemoEnemyName);
            Health health = enemy.AddComponent<Health>();

            EnemyMovement movement = enemy.AddComponent<EnemyMovement>();
            movement.ConfigureAgent(3.5f, 14f, 0.28f, 1.7f, true);
            enemy.GetComponent<NavMeshAgent>().enabled = false;

            HearingSensor hearing = enemy.AddComponent<HearingSensor>();
            EnemyMemory memory = enemy.AddComponent<EnemyMemory>();

            GameObject eyeObject = new("EyeOrigin");
            eyeObject.transform.SetParent(enemy.transform, false);
            eyeObject.transform.localPosition = new Vector3(0f, 1.55f, 0.05f);
            Transform eyeOrigin = eyeObject.transform;

            VisionSensor vision = enemy.AddComponent<VisionSensor>();
            vision.Configure(eyeOrigin, null, Physics.DefaultRaycastLayers, Physics.DefaultRaycastLayers);

            EnemyBrain brain = enemy.AddComponent<EnemyBrain>();
            brain.ConfigurePerception(vision, hearing, memory);

            EnemyAIGizmos gizmos = enemy.AddComponent<EnemyAIGizmos>();
            gizmos.Configure(brain);

            GameObject visualClone = AddKaiaVisual(enemy.transform);
            Animator animator = visualClone != null ? visualClone.GetComponentInChildren<Animator>(true) : null;
            EnemyAnimationController animationController = enemy.AddComponent<EnemyAnimationController>();
            animationController.Configure(animator);
            movement.ConfigureAnimation(animationController);

            WeaponController weapon = visualClone != null ? visualClone.GetComponentInChildren<WeaponController>(true) : null;
            if (weapon == null) weapon = CreateFallbackWeapon(enemy.transform);

            TacticalEvaluator tacticalEvaluator = enemy.AddComponent<TacticalEvaluator>();
            CoverEvaluator coverEvaluator = enemy.AddComponent<CoverEvaluator>();
            brain.ConfigureExecution(movement, weapon, health, animationController);
            brain.ConfigureDecision(tacticalEvaluator, coverEvaluator);

            CreateMeshAlignedHitZones(visualClone, enemy);
            CreateStatusLabel(enemy.transform, out TMP_Text statusText, out Transform labelRoot);
            EnemyPerceptionDemoView view = enemy.AddComponent<EnemyPerceptionDemoView>();
            view.Configure(brain, health, statusText, labelRoot);
            return enemy;
        }

        private static WeaponController CreateFallbackWeapon(Transform enemyRoot)
        {
            WeaponDefinition definition = AssetDatabase.LoadAssetAtPath<WeaponDefinition>(RifleDefinitionPath);
            if (definition == null)
            {
                Debug.LogWarning($"[AI Perception Demo] Rifle definition was not found at {RifleDefinitionPath}.");
                return null;
            }

            GameObject weaponObject = new("AI_Rifle");
            weaponObject.transform.SetParent(enemyRoot, false);
            GameObject muzzleObject = new("Muzzle");
            muzzleObject.transform.SetParent(weaponObject.transform, false);
            muzzleObject.transform.localPosition = new Vector3(0f, 1.35f, 0.35f);

            WeaponController weapon = weaponObject.AddComponent<WeaponController>();
            weapon.Configure(definition, muzzleObject.transform);
            return weapon;
        }

        private static GameObject AddKaiaVisual(Transform enemyRoot)
        {
            GameObject playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            if (playerPrefab == null)
            {
                Debug.LogWarning($"[AI Perception Demo] Player prefab not found at {PlayerPrefabPath}. Enemy prefab will use a fallback body collider without a character visual.");
                return null;
            }

            Transform sourceVisual = playerPrefab.transform.Find("Visual");
            if (sourceVisual == null)
            {
                Debug.LogWarning("[AI Perception Demo] Player_Kaia prefab has no Visual child. Enemy prefab will use a fallback body collider without a character visual.");
                return null;
            }

            GameObject visualClone = Object.Instantiate(sourceVisual.gameObject);
            visualClone.name = "Visual_Kaia_Enemy";
            visualClone.transform.SetParent(enemyRoot, false);
            visualClone.transform.localPosition = Vector3.zero;
            visualClone.transform.localRotation = Quaternion.identity;
            visualClone.transform.localScale = Vector3.one;
            return visualClone;
        }

        private static void CreateMeshAlignedHitZones(GameObject visualClone, GameObject enemyRoot)
        {
            Animator animator = visualClone != null ? visualClone.GetComponentInChildren<Animator>(true) : null;
            if (animator == null || !animator.isHuman)
            {
                CapsuleCollider fallback = enemyRoot.AddComponent<CapsuleCollider>();
                fallback.height = 1.7f;
                fallback.radius = 0.31f;
                fallback.center = new Vector3(0f, 0.85f, 0f);
                DamageHitZone zone = enemyRoot.AddComponent<DamageHitZone>();
                zone.Configure(DamageHitZoneType.Torso, 1f);
                return;
            }

            CreateSphereZone(animator.GetBoneTransform(HumanBodyBones.Head), "HitZone_Head", DamageHitZoneType.Head, 2f, 0.13f);
            CreateCapsuleZone(animator.GetBoneTransform(HumanBodyBones.Chest), animator.GetBoneTransform(HumanBodyBones.Hips), "HitZone_Torso", DamageHitZoneType.Torso, 1f, 0.22f);
            CreateCapsuleZone(animator.GetBoneTransform(HumanBodyBones.LeftUpperArm), animator.GetBoneTransform(HumanBodyBones.LeftLowerArm), "HitZone_LeftUpperArm", DamageHitZoneType.Arm, 0.75f, 0.085f);
            CreateCapsuleZone(animator.GetBoneTransform(HumanBodyBones.LeftLowerArm), animator.GetBoneTransform(HumanBodyBones.LeftHand), "HitZone_LeftLowerArm", DamageHitZoneType.Arm, 0.75f, 0.075f);
            CreateCapsuleZone(animator.GetBoneTransform(HumanBodyBones.RightUpperArm), animator.GetBoneTransform(HumanBodyBones.RightLowerArm), "HitZone_RightUpperArm", DamageHitZoneType.Arm, 0.75f, 0.085f);
            CreateCapsuleZone(animator.GetBoneTransform(HumanBodyBones.RightLowerArm), animator.GetBoneTransform(HumanBodyBones.RightHand), "HitZone_RightLowerArm", DamageHitZoneType.Arm, 0.75f, 0.075f);
            CreateCapsuleZone(animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg), animator.GetBoneTransform(HumanBodyBones.LeftLowerLeg), "HitZone_LeftThigh", DamageHitZoneType.Leg, 0.65f, 0.11f);
            CreateCapsuleZone(animator.GetBoneTransform(HumanBodyBones.LeftLowerLeg), animator.GetBoneTransform(HumanBodyBones.LeftFoot), "HitZone_LeftCalf", DamageHitZoneType.Leg, 0.65f, 0.09f);
            CreateCapsuleZone(animator.GetBoneTransform(HumanBodyBones.RightUpperLeg), animator.GetBoneTransform(HumanBodyBones.RightLowerLeg), "HitZone_RightThigh", DamageHitZoneType.Leg, 0.65f, 0.11f);
            CreateCapsuleZone(animator.GetBoneTransform(HumanBodyBones.RightLowerLeg), animator.GetBoneTransform(HumanBodyBones.RightFoot), "HitZone_RightCalf", DamageHitZoneType.Leg, 0.65f, 0.09f);
        }

        private static void CreateSphereZone(Transform bone, string name, DamageHitZoneType zoneType, float multiplier, float radius)
        {
            if (bone == null) return;
            GameObject zoneObject = new(name);
            zoneObject.transform.SetParent(bone, false);
            SphereCollider collider = zoneObject.AddComponent<SphereCollider>();
            collider.radius = radius;
            DamageHitZone zone = zoneObject.AddComponent<DamageHitZone>();
            zone.Configure(zoneType, multiplier);
        }

        private static void CreateCapsuleZone(Transform fromBone, Transform toBone, string name, DamageHitZoneType zoneType, float multiplier, float radius)
        {
            if (fromBone == null || toBone == null) return;
            Vector3 endLocal = fromBone.InverseTransformPoint(toBone.position);
            float distance = endLocal.magnitude;
            if (distance <= 0.001f) return;

            GameObject zoneObject = new(name);
            zoneObject.transform.SetParent(fromBone, false);
            zoneObject.transform.localPosition = endLocal * 0.5f;
            zoneObject.transform.localRotation = Quaternion.FromToRotation(Vector3.up, endLocal.normalized);
            CapsuleCollider collider = zoneObject.AddComponent<CapsuleCollider>();
            collider.direction = 1;
            collider.radius = radius;
            collider.height = Mathf.Max(radius * 2f, distance + radius * 1.5f);
            DamageHitZone zone = zoneObject.AddComponent<DamageHitZone>();
            zone.Configure(zoneType, multiplier);
        }

        private static void CreateStatusLabel(Transform parent, out TMP_Text statusText, out Transform labelRoot)
        {
            GameObject canvasObject = new("AI_StatusCanvas", typeof(RectTransform));
            canvasObject.transform.SetParent(parent, false);
            RectTransform canvasRect = canvasObject.GetComponent<RectTransform>();
            canvasRect.localPosition = new Vector3(0f, 2.15f, 0f);
            canvasRect.localScale = Vector3.one * 0.005f;
            canvasRect.sizeDelta = new Vector2(420f, 170f);

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
            text.text = "AI TEST\nEDIT MODE\nTACTICAL CHECK ON PLAY\nHP -- / 100";
            text.fontSize = 24f;
            text.alignment = TextAlignmentOptions.Center;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.raycastTarget = false;
            text.color = Color.white;
            statusText = text;
            labelRoot = canvasObject.transform;
        }

        private static bool EnsureEnemyInstance(Scene scene)
        {
            if (FindSceneEnemy(scene) != null) return false;
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(EnemyPrefabPath);
            if (prefab == null)
            {
                Debug.LogError($"[AI Perception Demo] Enemy prefab is missing at {EnemyPrefabPath}.");
                return false;
            }

            GameObject parent = FindRootObject(scene, EnemyParentName);
            if (parent == null)
            {
                parent = new GameObject(EnemyParentName);
                SceneManager.MoveGameObjectToScene(parent, scene);
            }

            GameObject enemy = PrefabUtility.InstantiatePrefab(prefab, scene) as GameObject;
            if (enemy == null) return false;
            enemy.name = EnemyInstanceName;
            enemy.transform.SetParent(parent.transform, true);
            PlaceEnemyNearPlayer(scene, enemy.transform);
            EditorUtility.SetDirty(enemy);
            EditorSceneManager.MarkSceneDirty(scene);
            return true;
        }

        private static void PlaceEnemyNearPlayer(Scene scene, Transform enemy)
        {
            PlayerVisualController playerVisual = FindPlayerVisual(scene);
            if (playerVisual == null)
            {
                enemy.position = new Vector3(0f, 0f, 8f);
                enemy.rotation = Quaternion.identity;
                return;
            }

            Transform player = playerVisual.transform;
            Vector3 desired = player.position + player.forward * 9f + player.right * 2.5f;
            Vector3 rayOrigin = desired + Vector3.up * 15f;
            if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit groundHit, 40f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) desired.y = groundHit.point.y;
            else desired.y = player.position.y;
            if (NavMesh.SamplePosition(desired, out NavMeshHit navHit, EnemyNavMeshSnapDistance, NavMesh.AllAreas)) desired = navHit.position;
            enemy.position = desired;

            Vector3 facingAwayFromPlayer = enemy.position - player.position;
            facingAwayFromPlayer.y = 0f;
            if (facingAwayFromPlayer.sqrMagnitude <= 0.001f) facingAwayFromPlayer = -player.forward;
            enemy.rotation = Quaternion.LookRotation(facingAwayFromPlayer.normalized, Vector3.up);
        }

        private static GameObject FindSceneEnemy(Scene scene)
        {
            GameObject parent = FindRootObject(scene, EnemyParentName);
            if (parent == null) return null;
            for (int i = 0; i < parent.transform.childCount; i++)
            {
                Transform child = parent.transform.GetChild(i);
                if (child.GetComponent<EnemyPerceptionDemoView>() != null) return child.gameObject;
            }
            for (int i = 0; i < parent.transform.childCount; i++)
            {
                Transform child = parent.transform.GetChild(i);
                if (IsDemoEnemyCandidate(child)) return child.gameObject;
            }
            return null;
        }

        private static void DeleteSceneEnemies(Scene scene)
        {
            GameObject parent = FindRootObject(scene, EnemyParentName);
            if (parent == null) return;
            for (int i = parent.transform.childCount - 1; i >= 0; i--)
            {
                Transform child = parent.transform.GetChild(i);
                if (IsDemoEnemyCandidate(child))
                {
                    Object.DestroyImmediate(child.gameObject);
                    EditorSceneManager.MarkSceneDirty(scene);
                }
            }
        }

        private static void RemoveDuplicateSceneEnemies(Scene scene)
        {
            GameObject parent = FindRootObject(scene, EnemyParentName);
            if (parent == null) return;
            GameObject primary = FindSceneEnemy(scene);
            if (primary == null) return;
            for (int i = parent.transform.childCount - 1; i >= 0; i--)
            {
                Transform child = parent.transform.GetChild(i);
                if (!IsDemoEnemyCandidate(child) || child.gameObject == primary) continue;
                Object.DestroyImmediate(child.gameObject);
                EditorSceneManager.MarkSceneDirty(scene);
            }
        }

        private static bool IsDemoEnemyCandidate(Transform child) => child.name == EnemyInstanceName || child.GetComponent<EnemyPerceptionDemoView>() != null || child.GetComponent<EnemyBrain>() != null;

        private static GameObject FindRootObject(Scene scene, string objectName)
        {
            foreach (GameObject root in scene.GetRootGameObjects()) if (root.name == objectName) return root;
            return null;
        }

        private static void EnsureFolderExists(string folderPath)
        {
            string[] parts = folderPath.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }
#endif
    }
}
