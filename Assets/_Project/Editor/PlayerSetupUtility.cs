using System;
using System.IO;
using System.Linq;
using TacticalEcho.AnimationSystem.Runtime;
using TacticalEcho.CameraSystem;
using TacticalEcho.Character.Player;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TacticalEcho.EditorTools
{
    public static class PlayerSetupUtility
    {
        private const string KaiaPrefabPath = "Assets/_ThirdParty/Characters/Kaia Thorn/Kaia Thorn.prefab";
        private const string InputAssetPath = "Assets/_Project/Input/TacticalEcho_InputActions.inputactions";
        private const string InputReferenceFolder = "Assets/_Project/Input/References";
        private const string ControllerFolder = "Assets/_Project/Animation/Controllers";
        private const string AnimatorControllerPath = ControllerFolder + "/Player_Kaia_Locomotion.controller";
        private const string PlayerPrefabFolder = "Assets/_Project/Prefabs/Player";
        private const string PlayerPrefabPath = PlayerPrefabFolder + "/Player_Kaia.prefab";

        private const string IdleClipPath = "Assets/_ThirdParty/Animations/Kevin Iglesias/Human Animations/Animations/Female/Idles/HumanF@Idle01.fbx";
        private const string WalkClipPath = "Assets/_ThirdParty/Animations/Kevin Iglesias/Human Animations/Animations/Female/Movement/Walk/HumanF@Walk01_Forward.fbx";
        private const string RunClipPath = "Assets/_ThirdParty/Animations/Kevin Iglesias/Human Animations/Animations/Female/Movement/Run/HumanF@Run01_Forward.fbx";

        [MenuItem("Tactical Echo/Setup/1. Build or Refresh Kaia Player Prefab")]
        public static void BuildPlayerPrefab()
        {
            GameObject kaiaPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(KaiaPrefabPath);
            if (kaiaPrefab == null)
            {
                EditorUtility.DisplayDialog(
                    "Tactical Echo",
                    $"Không tìm thấy Kaia Thorn tại:\n{KaiaPrefabPath}",
                    "OK");
                return;
            }

            InputActionAsset inputAsset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputAssetPath);
            if (inputAsset == null)
            {
                EditorUtility.DisplayDialog(
                    "Tactical Echo",
                    $"Không tìm thấy Input Actions tại:\n{InputAssetPath}",
                    "OK");
                return;
            }

            EnsureFolder(InputReferenceFolder);
            EnsureFolder(ControllerFolder);
            EnsureFolder(PlayerPrefabFolder);

            AnimatorController locomotionController = BuildLocomotionController();
            if (locomotionController == null)
            {
                return;
            }

            InputActionReference moveReference = CreateInputReference(inputAsset, "Move", "Move");
            InputActionReference lookReference = CreateInputReference(inputAsset, "Look", "Look");
            InputActionReference sprintReference = CreateInputReference(inputAsset, "Sprint", "Sprint");
            InputActionReference fireReference = CreateInputReference(inputAsset, "Attack", "Fire");

            GameObject playerRoot = new("Player");
            try
            {
                playerRoot.tag = "Player";

                CharacterController characterController = playerRoot.AddComponent<CharacterController>();
                PlayerInputReader inputReader = playerRoot.AddComponent<PlayerInputReader>();
                PlayerVisualController visualController = playerRoot.AddComponent<PlayerVisualController>();
                PlayerAnimationController animationController = playerRoot.AddComponent<PlayerAnimationController>();
                PlayerController playerController = playerRoot.AddComponent<PlayerController>();

                Transform visualRoot = CreateChild(playerRoot.transform, "Visual");
                GameObject kaiaInstance = PrefabUtility.InstantiatePrefab(kaiaPrefab) as GameObject;
                if (kaiaInstance == null)
                {
                    throw new InvalidOperationException("Không thể instantiate Kaia Thorn prefab.");
                }

                kaiaInstance.name = "Kaia Thorn";
                kaiaInstance.transform.SetParent(visualRoot, false);
                kaiaInstance.transform.localPosition = Vector3.zero;
                kaiaInstance.transform.localRotation = Quaternion.identity;
                kaiaInstance.transform.localScale = Vector3.one;

                Animator animator = kaiaInstance.GetComponentInChildren<Animator>(true);
                if (animator == null)
                {
                    throw new InvalidOperationException("Kaia Thorn prefab không có Animator.");
                }

                animator.runtimeAnimatorController = locomotionController;
                animator.applyRootMotion = false;

                Bounds bounds = CalculateBounds(visualRoot.gameObject);
                ConfigureCharacterController(characterController, playerRoot.transform, bounds);

                float localBottom = playerRoot.transform.InverseTransformPoint(bounds.min).y;
                float characterHeight = Mathf.Max(1f, bounds.size.y);

                Transform cameraTarget = CreateChild(playerRoot.transform, "CameraTarget");
                cameraTarget.localPosition = new Vector3(0f, localBottom + characterHeight * 0.88f, 0f);

                Transform aimOrigin = CreateChild(playerRoot.transform, "AimOrigin");
                aimOrigin.localPosition = new Vector3(0f, localBottom + characterHeight * 0.82f, 0.12f);

                inputReader.Configure(
                    moveReference,
                    lookReference,
                    sprintReference,
                    fireReference,
                    null,
                    null);

                visualController.Configure(visualRoot, animator);
                animationController.Configure(visualController);
                playerController.ConfigureCoreReferences(inputReader, null, aimOrigin, animationController);

                PrefabUtility.SaveAsPrefabAsset(playerRoot, PlayerPrefabPath);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);

                EditorUtility.DisplayDialog(
                    "Tactical Echo",
                    "Đã tạo Player_Kaia.prefab, Animator locomotion và InputActionReference cho Move/Look/Sprint/Fire.",
                    "OK");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorUtility.DisplayDialog("Tactical Echo", exception.Message, "OK");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(playerRoot);
            }
        }

        [MenuItem("Tactical Echo/Setup/2. Place Kaia Player In Current Scene")]
        public static void PlacePlayerInScene()
        {
            GameObject playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            if (playerPrefab == null)
            {
                EditorUtility.DisplayDialog(
                    "Tactical Echo",
                    "Chưa có Player_Kaia.prefab. Hãy chạy bước 1 trước.",
                    "OK");
                return;
            }

            PlayerController existingPlayer = UnityEngine.Object.FindFirstObjectByType<PlayerController>();
            if (existingPlayer != null)
            {
                Selection.activeGameObject = existingPlayer.gameObject;
                EditorUtility.DisplayDialog(
                    "Tactical Echo",
                    "Scene đã có PlayerController. Mình đã chọn object đó thay vì tạo thêm một Player khác.",
                    "OK");
                return;
            }

            Vector3 spawnPosition = Selection.activeTransform != null
                ? Selection.activeTransform.position
                : Vector3.zero;

            GameObject player = PrefabUtility.InstantiatePrefab(playerPrefab) as GameObject;
            if (player == null)
            {
                EditorUtility.DisplayDialog("Tactical Echo", "Không thể instantiate Player_Kaia.prefab.", "OK");
                return;
            }

            Undo.RegisterCreatedObjectUndo(player, "Place Tactical Echo Player");
            player.name = "Player";
            player.transform.position = spawnPosition;

            PlayerInputReader inputReader = player.GetComponent<PlayerInputReader>();
            PlayerController playerController = player.GetComponent<PlayerController>();
            Transform cameraTarget = player.transform.Find("CameraTarget");

            Camera sceneCamera = Camera.main != null
                ? Camera.main
                : UnityEngine.Object.FindFirstObjectByType<Camera>();

            if (sceneCamera == null)
            {
                GameObject cameraObject = new("Main Camera");
                Undo.RegisterCreatedObjectUndo(cameraObject, "Create Tactical Echo Camera");
                sceneCamera = cameraObject.AddComponent<Camera>();
                cameraObject.AddComponent<AudioListener>();
                cameraObject.tag = "MainCamera";
            }

            PlayerCameraController cameraController = sceneCamera.GetComponent<PlayerCameraController>();
            if (cameraController == null)
            {
                cameraController = Undo.AddComponent<PlayerCameraController>(sceneCamera.gameObject);
            }

            cameraController.Configure(cameraTarget, inputReader);
            playerController.ConfigureCamera(cameraController, sceneCamera.transform);

            if (cameraTarget != null)
            {
                sceneCamera.transform.position = cameraTarget.position + new Vector3(0.6f, 0.3f, -3.5f);
                Vector3 lookDirection = cameraTarget.position - sceneCamera.transform.position;
                if (lookDirection.sqrMagnitude > 0.001f)
                {
                    sceneCamera.transform.rotation = Quaternion.LookRotation(lookDirection.normalized, Vector3.up);
                }
            }

            Selection.activeGameObject = player;
            EditorSceneManager.MarkSceneDirty(player.scene);

            EditorUtility.DisplayDialog(
                "Tactical Echo",
                "Đã đặt Player vào scene và nối Main Camera. Di chuyển Player tới vị trí spawn bạn muốn rồi Play để test WASD + chuột + Shift.",
                "OK");
        }

        private static AnimatorController BuildLocomotionController()
        {
            EnsureLooping(IdleClipPath);
            EnsureLooping(WalkClipPath);
            EnsureLooping(RunClipPath);

            AnimationClip idle = LoadAnimationClip(IdleClipPath);
            AnimationClip walk = LoadAnimationClip(WalkClipPath);
            AnimationClip run = LoadAnimationClip(RunClipPath);

            if (idle == null || walk == null || run == null)
            {
                EditorUtility.DisplayDialog(
                    "Tactical Echo",
                    "Không tìm đủ Idle/Walk/Run animation từ Human Soldier Animations FREE.",
                    "OK");
                return null;
            }

            if (AssetDatabase.LoadAssetAtPath<AnimatorController>(AnimatorControllerPath) != null)
            {
                AssetDatabase.DeleteAsset(AnimatorControllerPath);
            }

            AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(AnimatorControllerPath);
            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            controller.AddParameter("MoveX", AnimatorControllerParameterType.Float);
            controller.AddParameter("MoveY", AnimatorControllerParameterType.Float);
            controller.AddParameter("Sprinting", AnimatorControllerParameterType.Bool);
            controller.AddParameter("Aim", AnimatorControllerParameterType.Bool);
            controller.AddParameter("Fire", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Reload", AnimatorControllerParameterType.Trigger);

            AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;
            AnimatorState locomotionState = stateMachine.AddState("Locomotion");

            BlendTree blendTree = new()
            {
                name = "Locomotion Blend Tree",
                blendType = BlendTreeType.Simple1D,
                blendParameter = "Speed",
                useAutomaticThresholds = false
            };

            AssetDatabase.AddObjectToAsset(blendTree, controller);
            blendTree.AddChild(idle, 0f);
            blendTree.AddChild(walk, 0.65f);
            blendTree.AddChild(run, 1f);

            locomotionState.motion = blendTree;
            stateMachine.defaultState = locomotionState;

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            return controller;
        }

        private static InputActionReference CreateInputReference(
            InputActionAsset inputAsset,
            string actionName,
            string outputName)
        {
            InputAction action = inputAsset.FindAction($"Player/{actionName}", true);
            string assetPath = $"{InputReferenceFolder}/{outputName}.asset";

            if (AssetDatabase.LoadAssetAtPath<InputActionReference>(assetPath) != null)
            {
                AssetDatabase.DeleteAsset(assetPath);
            }

            InputActionReference reference = InputActionReference.Create(action);
            reference.name = outputName;
            AssetDatabase.CreateAsset(reference, assetPath);
            return reference;
        }

        private static AnimationClip LoadAnimationClip(string assetPath)
        {
            return AssetDatabase.LoadAllAssetsAtPath(assetPath)
                .OfType<AnimationClip>()
                .FirstOrDefault(clip => !clip.name.StartsWith("__preview__", StringComparison.Ordinal));
        }

        private static void EnsureLooping(string assetPath)
        {
            if (AssetImporter.GetAtPath(assetPath) is not ModelImporter importer)
            {
                return;
            }

            ModelImporterClipAnimation[] clips = importer.clipAnimations;
            if (clips == null || clips.Length == 0)
            {
                clips = importer.defaultClipAnimations;
            }

            bool changed = false;
            foreach (ModelImporterClipAnimation clip in clips)
            {
                if (!clip.loopTime)
                {
                    clip.loopTime = true;
                    changed = true;
                }
            }

            if (!changed)
            {
                return;
            }

            importer.clipAnimations = clips;
            importer.SaveAndReimport();
        }

        private static void ConfigureCharacterController(
            CharacterController controller,
            Transform playerRoot,
            Bounds worldBounds)
        {
            float height = Mathf.Max(1f, worldBounds.size.y);
            float bottom = playerRoot.InverseTransformPoint(worldBounds.min).y;

            controller.height = height;
            controller.radius = Mathf.Clamp(height * 0.18f, 0.25f, 0.4f);
            controller.center = new Vector3(0f, bottom + height * 0.5f, 0f);
            controller.stepOffset = Mathf.Min(0.3f, height * 0.2f);
            controller.skinWidth = 0.03f;
        }

        private static Bounds CalculateBounds(GameObject root)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
            {
                return new Bounds(root.transform.position + Vector3.up, new Vector3(0.6f, 2f, 0.6f));
            }

            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }

            return bounds;
        }

        private static Transform CreateChild(Transform parent, string name)
        {
            GameObject child = new(name);
            child.transform.SetParent(parent, false);
            return child.transform;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            string folderName = Path.GetFileName(path);

            if (!string.IsNullOrEmpty(parent))
            {
                EnsureFolder(parent);
                AssetDatabase.CreateFolder(parent, folderName);
            }
        }
    }
}
