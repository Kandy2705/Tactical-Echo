using System;
using System.Linq;
using TacticalEcho.AnimationSystem.Runtime;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace TacticalEcho.EditorTools
{
    public static class RifleAnimationSetupUtility
    {
        private const string ControllerPath = "Assets/_Project/Animation/Controllers/Player_Kaia_Locomotion.controller";
        private const string MaskFolder = "Assets/_Project/Animation/Masks";
        private const string MaskPath = MaskFolder + "/Player_UpperBody_Rifle.mask";
        private const string PlayerPrefabPath = "Assets/_Project/Prefabs/Player/Player_Kaia.prefab";

        private const string AimClipPath = "Assets/_ThirdParty/Animations/Kevin Iglesias/Human Animations/Animations/Female/Combat/AssaultRifle/HumanF@AssaultRifle_Aim01.fbx";
        private const string FireClipPath = "Assets/_ThirdParty/Animations/Kevin Iglesias/Human Animations/Animations/Female/Combat/AssaultRifle/HumanF@AssaultRifle_Aim01_Shoot01.fbx";
        private const string ReloadClipPath = "Assets/_ThirdParty/Animations/Kevin Iglesias/Human Animations/Animations/Female/Combat/AssaultRifle/HumanF@AssaultRifle_Reload01.fbx";

        private const string UpperBodyLayerName = "Upper Body";

        [MenuItem("Tactical Echo/Setup/4. Setup Rifle Animations")]
        public static void SetupRifleAnimations()
        {
            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller == null)
            {
                EditorUtility.DisplayDialog("Tactical Echo", "Không tìm thấy Player_Kaia_Locomotion.controller.", "OK");
                return;
            }

            AnimationClip aimClip = LoadClip(AimClipPath);
            AnimationClip fireClip = LoadClip(FireClipPath);
            AnimationClip reloadClip = LoadClip(ReloadClipPath);

            if (aimClip == null || fireClip == null || reloadClip == null)
            {
                EditorUtility.DisplayDialog(
                    "Tactical Echo",
                    "Thiếu một trong các clip Assault Rifle: Aim / Shoot / Reload.",
                    "OK");
                return;
            }

            try
            {
                EnsureFolder(MaskFolder);
                AvatarMask mask = BuildOrUpdateUpperBodyMask();
                BuildUpperBodyLayer(controller, mask, aimClip, fireClip, reloadClip);
                EnsurePlayerUsesController(controller);

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                EditorUtility.DisplayDialog(
                    "Tactical Echo",
                    "Đã setup Rifle Animation.\n\n" +
                    "- Upper Body layer\n" +
                    "- Aim pose\n" +
                    "- Fire animation\n" +
                    "- Reload animation\n" +
                    "- Avatar Mask thân trên\n" +
                    "- IK Pass cho tay trái\n\n" +
                    "Play Mode để test: chuột phải Aim, chuột trái bắn, R reload.",
                    "OK");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorUtility.DisplayDialog("Tactical Echo", exception.Message, "OK");
            }
        }

        private static AvatarMask BuildOrUpdateUpperBodyMask()
        {
            AvatarMask mask = AssetDatabase.LoadAssetAtPath<AvatarMask>(MaskPath);
            if (mask == null)
            {
                mask = new AvatarMask { name = "Player_UpperBody_Rifle" };
                AssetDatabase.CreateAsset(mask, MaskPath);
            }

            for (int i = 0; i < (int)AvatarMaskBodyPart.LastBodyPart; i++)
            {
                mask.SetHumanoidBodyPartActive((AvatarMaskBodyPart)i, false);
            }

            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.Body, true);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.Head, true);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftArm, true);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightArm, true);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftFingers, true);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightFingers, true);

            EditorUtility.SetDirty(mask);
            return mask;
        }

        private static void BuildUpperBodyLayer(
            AnimatorController controller,
            AvatarMask mask,
            AnimationClip aimClip,
            AnimationClip fireClip,
            AnimationClip reloadClip)
        {
            AnimatorControllerLayer[] layers = controller.layers;
            int existingIndex = Array.FindIndex(layers, layer => layer.name == UpperBodyLayerName);

            AnimatorControllerLayer layer;
            if (existingIndex >= 0)
            {
                layer = layers[existingIndex];
            }
            else
            {
                layer = new AnimatorControllerLayer
                {
                    name = UpperBodyLayerName,
                    defaultWeight = 1f,
                    blendingMode = AnimatorLayerBlendingMode.Override,
                    avatarMask = mask,
                    stateMachine = new AnimatorStateMachine { name = UpperBodyLayerName }
                };

                AssetDatabase.AddObjectToAsset(layer.stateMachine, controller);
                Array.Resize(ref layers, layers.Length + 1);
                layers[^1] = layer;
                controller.layers = layers;
                existingIndex = layers.Length - 1;
            }

            layer.avatarMask = mask;
            layer.defaultWeight = 1f;
            layer.blendingMode = AnimatorLayerBlendingMode.Override;
            layer.iKPass = true;

            AnimatorStateMachine stateMachine = layer.stateMachine;
            ClearStateMachine(stateMachine);

            AnimatorState aimState = stateMachine.AddState("Rifle Aim", new Vector3(260f, 80f));
            AnimatorState fireState = stateMachine.AddState("Rifle Fire", new Vector3(520f, 20f));
            AnimatorState reloadState = stateMachine.AddState("Rifle Reload", new Vector3(520f, 160f));

            aimState.motion = aimClip;
            fireState.motion = fireClip;
            reloadState.motion = reloadClip;

            aimState.writeDefaultValues = true;
            fireState.writeDefaultValues = true;
            reloadState.writeDefaultValues = true;

            stateMachine.defaultState = aimState;

            AnimatorStateTransition fireTransition = stateMachine.AddAnyStateTransition(fireState);
            fireTransition.hasExitTime = false;
            fireTransition.hasFixedDuration = true;
            fireTransition.duration = 0.03f;
            fireTransition.canTransitionToSelf = false;
            fireTransition.AddCondition(AnimatorConditionMode.If, 0f, "Fire");

            AnimatorStateTransition fireReturn = fireState.AddTransition(aimState);
            fireReturn.hasExitTime = true;
            fireReturn.exitTime = 0.82f;
            fireReturn.hasFixedDuration = true;
            fireReturn.duration = 0.05f;

            AnimatorStateTransition reloadTransition = stateMachine.AddAnyStateTransition(reloadState);
            reloadTransition.hasExitTime = false;
            reloadTransition.hasFixedDuration = true;
            reloadTransition.duration = 0.06f;
            reloadTransition.canTransitionToSelf = false;
            reloadTransition.AddCondition(AnimatorConditionMode.If, 0f, "Reload");

            AnimatorStateTransition reloadReturn = reloadState.AddTransition(aimState);
            reloadReturn.hasExitTime = true;
            reloadReturn.exitTime = 0.95f;
            reloadReturn.hasFixedDuration = true;
            reloadReturn.duration = 0.08f;

            layers = controller.layers;
            layers[existingIndex] = layer;
            controller.layers = layers;
            EditorUtility.SetDirty(controller);
        }

        private static void ClearStateMachine(AnimatorStateMachine stateMachine)
        {
            foreach (ChildAnimatorState childState in stateMachine.states.ToArray())
            {
                stateMachine.RemoveState(childState.state);
            }

            foreach (ChildAnimatorStateMachine childMachine in stateMachine.stateMachines.ToArray())
            {
                stateMachine.RemoveStateMachine(childMachine.stateMachine);
            }

            foreach (AnimatorStateTransition transition in stateMachine.anyStateTransitions.ToArray())
            {
                stateMachine.RemoveAnyStateTransition(transition);
            }
        }

        private static void EnsurePlayerUsesController(AnimatorController controller)
        {
            GameObject playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            if (playerPrefab == null)
            {
                return;
            }

            GameObject root = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
            try
            {
                Animator animator = root.GetComponentInChildren<Animator>(true);
                if (animator == null)
                {
                    throw new InvalidOperationException("Player_Kaia.prefab không có Animator.");
                }

                animator.runtimeAnimatorController = controller;
                animator.applyRootMotion = false;

                PlayerAnimationController playerAnimation = root.GetComponent<PlayerAnimationController>();
                if (playerAnimation == null)
                {
                    playerAnimation = root.AddComponent<PlayerAnimationController>();
                }

                PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static AnimationClip LoadClip(string path)
        {
            return AssetDatabase.LoadAllAssetsAtPath(path)
                .OfType<AnimationClip>()
                .FirstOrDefault(clip => !clip.name.StartsWith("__preview__", StringComparison.Ordinal));
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            string parent = System.IO.Path.GetDirectoryName(path)?.Replace('\\', '/');
            string folderName = System.IO.Path.GetFileName(path);

            if (!string.IsNullOrEmpty(parent))
            {
                EnsureFolder(parent);
                AssetDatabase.CreateFolder(parent, folderName);
            }
        }
    }
}
