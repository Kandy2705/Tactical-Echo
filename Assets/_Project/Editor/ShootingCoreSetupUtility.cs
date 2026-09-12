using System;
using TacticalEcho.Character.Player;
using TacticalEcho.Combat.Weapons;
using UnityEditor;
using UnityEngine;

namespace TacticalEcho.EditorTools
{
    public static class ShootingCoreSetupUtility
    {
        private const string PlayerPrefabPath = "Assets/_Project/Prefabs/Player/Player_Kaia.prefab";
        private const string RiflePrefabPath = "Assets/_ThirdParty/Weapons/Gece Studio/Rifle HK416 - Free/Prefabs/Rifle_HK416.prefab";
        private const string WeaponDefinitionPath = "Assets/_Project/Combat/Weapons/Definitions/Rifle_HK416.asset";

        [MenuItem("Tactical Echo/Setup/3. Setup Shooting Core")]
        public static void SetupShootingCore()
        {
            GameObject playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            GameObject riflePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(RiflePrefabPath);
            WeaponDefinition definition = AssetDatabase.LoadAssetAtPath<WeaponDefinition>(WeaponDefinitionPath);

            if (playerPrefab == null || riflePrefab == null || definition == null)
            {
                EditorUtility.DisplayDialog(
                    "Tactical Echo",
                    "Thiếu Player_Kaia.prefab, Rifle_HK416.prefab hoặc Rifle_HK416.asset.",
                    "OK");
                return;
            }

            GameObject root = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
            try
            {
                PlayerController playerController = root.GetComponent<PlayerController>();
                Animator animator = root.GetComponentInChildren<Animator>(true);
                if (playerController == null || animator == null)
                {
                    throw new InvalidOperationException("Player prefab thiếu PlayerController hoặc Animator.");
                }

                Transform hand = animator.isHuman
                    ? animator.GetBoneTransform(HumanBodyBones.RightHand)
                    : null;

                hand ??= FindTransformContaining(root.transform, "righthand");
                hand ??= FindTransformContaining(root.transform, "right_hand");
                hand ??= FindTransformContaining(root.transform, "hand.r");

                if (hand == null)
                {
                    throw new InvalidOperationException("Không tìm được bone RightHand của Kaia.");
                }

                Transform weaponMount = hand.Find("WeaponMount");
                if (weaponMount == null)
                {
                    GameObject mountObject = new("WeaponMount");
                    weaponMount = mountObject.transform;
                    weaponMount.SetParent(hand, false);
                }

                Transform existingRifle = weaponMount.Find("Rifle_HK416");
                GameObject rifleInstance;
                if (existingRifle != null)
                {
                    rifleInstance = existingRifle.gameObject;
                }
                else
                {
                    rifleInstance = PrefabUtility.InstantiatePrefab(riflePrefab, weaponMount) as GameObject;
                    if (rifleInstance == null)
                    {
                        throw new InvalidOperationException("Không thể instantiate Rifle_HK416.prefab.");
                    }

                    rifleInstance.name = "Rifle_HK416";
                    rifleInstance.transform.localPosition = Vector3.zero;
                    rifleInstance.transform.localRotation = Quaternion.identity;
                    rifleInstance.transform.localScale = Vector3.one;
                }

                WeaponController weaponController = rifleInstance.GetComponent<WeaponController>();
                if (weaponController == null)
                {
                    weaponController = rifleInstance.AddComponent<WeaponController>();
                }

                Transform muzzle = rifleInstance.transform.Find("Muzzle");
                if (muzzle == null)
                {
                    GameObject muzzleObject = new("Muzzle");
                    muzzle = muzzleObject.transform;
                    muzzle.SetParent(rifleInstance.transform, false);
                    muzzle.localPosition = new Vector3(0f, 0f, 0.55f);
                }

                weaponController.Configure(definition, muzzle);
                playerController.ConfigureWeapon(weaponController);

                PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                EditorUtility.DisplayDialog(
                    "Tactical Echo",
                    "Shooting Core đã được nối vào Player_Kaia.prefab.\n\n" +
                    "Chuột trái: bắn / giữ để bắn tự động\n" +
                    "Chuột phải: Aim\n" +
                    "R: Reload\n\n" +
                    "WeaponMount được đặt ở RightHand. Nếu súng chưa khớp tay, chỉ cần chỉnh local Position/Rotation của WeaponMount, không chỉnh code.",
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

        private static Transform FindTransformContaining(Transform root, string value)
        {
            string normalizedValue = value.Replace("_", string.Empty).Replace(".", string.Empty).ToLowerInvariant();

            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            {
                string normalizedName = child.name.Replace("_", string.Empty).Replace(".", string.Empty).ToLowerInvariant();
                if (normalizedName.Contains(normalizedValue))
                {
                    return child;
                }
            }

            return null;
        }
    }
}
