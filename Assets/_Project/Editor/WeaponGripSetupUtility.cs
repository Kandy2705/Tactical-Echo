using System;
using TacticalEcho.AnimationSystem.Runtime;
using TacticalEcho.Combat.Weapons;
using UnityEditor;
using UnityEngine;

namespace TacticalEcho.EditorTools
{
    public static class WeaponGripSetupUtility
    {
        private const string PlayerPrefabPath = "Assets/_Project/Prefabs/Player/Player_Kaia.prefab";

        [MenuItem("Tactical Echo/Setup/5. Setup Weapon Grip IK")]
        public static void SetupWeaponGripIK()
        {
            GameObject playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            if (playerPrefab == null)
            {
                EditorUtility.DisplayDialog("Tactical Echo", "Không tìm thấy Player_Kaia.prefab.", "OK");
                return;
            }

            GameObject root = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
            try
            {
                Animator animator = root.GetComponentInChildren<Animator>(true);
                if (animator == null || !animator.isHuman)
                {
                    throw new InvalidOperationException("Kaia cần Animator Humanoid để dùng Hand IK.");
                }

                Transform rightHand = animator.GetBoneTransform(HumanBodyBones.RightHand);
                Transform leftHand = animator.GetBoneTransform(HumanBodyBones.LeftHand);
                if (rightHand == null || leftHand == null)
                {
                    throw new InvalidOperationException("Không tìm được bone RightHand/LeftHand của Kaia.");
                }

                Transform weaponMount = rightHand.Find("WeaponMount");
                if (weaponMount == null)
                {
                    throw new InvalidOperationException("Không tìm thấy WeaponMount dưới RightHand.");
                }

                Transform rifle = weaponMount.Find("Rifle_HK416");
                if (rifle == null)
                {
                    throw new InvalidOperationException("Không tìm thấy Rifle_HK416 dưới WeaponMount.");
                }

                Transform rightGrip = EnsureChild(rifle, "RightHandGrip");
                Transform leftGrip = EnsureChild(rifle, "LeftHandGrip");
                Transform muzzle = rifle.Find("Muzzle");

                if (muzzle == null)
                {
                    muzzle = EnsureChild(rifle, "Muzzle");
                    muzzle.localPosition = new Vector3(0f, 0f, 0.55f);
                }

                // Right grip is a calibration/reference anchor for the pistol grip.
                // It is initialized at the current right hand pose; the rifle itself remains parented
                // to RightHand through WeaponMount, so there is no two-way transform fight at runtime.
                rightGrip.SetPositionAndRotation(rightHand.position, rightHand.rotation);

                // Left grip starts at the current left hand pose and is then kept as a point on the rifle.
                // Runtime IK will pull the left hand back to this anchor after animation evaluation.
                leftGrip.SetPositionAndRotation(leftHand.position, leftHand.rotation);

                WeaponGripPoints gripPoints = rifle.GetComponent<WeaponGripPoints>();
                if (gripPoints == null)
                {
                    gripPoints = rifle.gameObject.AddComponent<WeaponGripPoints>();
                }
                gripPoints.Configure(rightGrip, leftGrip, muzzle);

                WeaponHandIKController ikController = animator.GetComponent<WeaponHandIKController>();
                if (ikController == null)
                {
                    ikController = animator.gameObject.AddComponent<WeaponHandIKController>();
                }
                ikController.Configure(gripPoints, true);

                PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                EditorUtility.DisplayDialog(
                    "Tactical Echo",
                    "Đã tạo hệ Weapon Grip IK.\n\n" +
                    "Rifle_HK416\n" +
                    "├── RightHandGrip (điểm A)\n" +
                    "├── LeftHandGrip  (điểm B)\n" +
                    "└── Muzzle\n\n" +
                    "Tay phải vẫn là tay điều khiển súng. Tay trái được IK kéo tới LeftHandGrip để tránh rung/giằng transform.\n\n" +
                    "Sau khi chạy Play, nếu tay trái chưa nằm đúng handguard thì chỉ chỉnh Local Position/Rotation của LeftHandGrip.",
                    "OK");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorUtility.DisplayDialog("Tactical Echo", exception.Message, "OK");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static Transform EnsureChild(Transform parent, string childName)
        {
            Transform child = parent.Find(childName);
            if (child != null)
            {
                return child;
            }

            GameObject childObject = new(childName);
            child = childObject.transform;
            child.SetParent(parent, false);
            return child;
        }
    }
}
