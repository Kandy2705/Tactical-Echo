using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TacticalEcho.EditorTools
{
    public static class KaiaWorldBlendMaterialUtility
    {
        private const string SourceMaterialFolder = "Assets/_ThirdParty/Characters/Kaia Thorn/Materials";
        private const string OutputMaterialFolder = "Assets/_Project/Art/Characters/Kaia/Materials";
        private const string KaiaPrefabPath = "Assets/_ThirdParty/Characters/Kaia Thorn/Kaia Thorn.prefab";
        private const string PlayerPrefabPath = "Assets/_Project/Prefabs/Player/Player_Kaia.prefab";
        private const string WorldBlendShaderName = "SimpleURPToonLitOutlineExample";

        [MenuItem("Tactical Echo/Art/Apply Kaia World Blend Materials")]
        public static void ApplyWorldBlendMaterials()
        {
            Shader shader = Shader.Find(WorldBlendShaderName);
            if (shader == null)
            {
                EditorUtility.DisplayDialog(
                    "Tactical Echo",
                    $"Không tìm thấy shader '{WorldBlendShaderName}'.\nHãy kiểm tra package SimplestarGame trước.",
                    "OK");
                return;
            }

            EnsureFolder(OutputMaterialFolder);

            var materialMap = new Dictionary<Material, Material>();
            int createdCount = 0;
            int reusedCount = 0;

            string[] materialGuids = AssetDatabase.FindAssets("t:Material", new[] { SourceMaterialFolder });
            foreach (string guid in materialGuids)
            {
                string sourcePath = AssetDatabase.GUIDToAssetPath(guid);
                Material source = AssetDatabase.LoadAssetAtPath<Material>(sourcePath);
                if (source == null)
                {
                    continue;
                }

                Material target = BuildOrUpdateMaterial(source, shader, out bool created);
                if (target == null)
                {
                    continue;
                }

                materialMap[source] = target;
                if (created)
                {
                    createdCount++;
                }
                else
                {
                    reusedCount++;
                }
            }

            if (materialMap.Count == 0)
            {
                EditorUtility.DisplayDialog(
                    "Tactical Echo",
                    $"Không tìm thấy material Kaia trong:\n{SourceMaterialFolder}",
                    "OK");
                return;
            }

            int changedRenderers = 0;
            changedRenderers += ApplyToPrefabIfPresent(KaiaPrefabPath, materialMap);
            changedRenderers += ApplyToPrefabIfPresent(PlayerPrefabPath, materialMap);
            changedRenderers += ApplyToOpenScenes(materialMap);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog(
                "Tactical Echo",
                "Đã áp dụng bộ material World Blend cho Kaia.\n\n" +
                $"Material mới: {createdCount}\n" +
                $"Material cập nhật/tái sử dụng: {reusedCount}\n" +
                $"Renderer đã đổi material: {changedRenderers}\n\n" +
                "Material gốc trong _ThirdParty vẫn được giữ nguyên.",
                "OK");
        }

        private static Material BuildOrUpdateMaterial(Material source, Shader shader, out bool created)
        {
            string targetFileName = SanitizeFileName(source.name) + "_WorldBlend.mat";
            string targetPath = $"{OutputMaterialFolder}/{targetFileName}";
            Material target = AssetDatabase.LoadAssetAtPath<Material>(targetPath);

            created = target == null;
            if (created)
            {
                target = new Material(shader)
                {
                    name = source.name + "_WorldBlend"
                };
                AssetDatabase.CreateAsset(target, targetPath);
            }
            else if (target.shader != shader)
            {
                target.shader = shader;
            }

            Texture baseTexture = GetFirstTexture(source, "_BaseMap", "_MainTex");
            Color baseColor = GetFirstColor(source, Color.white, "_BaseColor", "_Color");

            if (baseTexture != null)
            {
                target.SetTexture("_BaseMap", baseTexture);
                target.SetTexture("_MainTex", baseTexture);
                target.SetTexture("_ShadeMap", baseTexture);
            }

            target.SetColor("_BaseColor", baseColor);
            target.SetColor("_ShadeColor", BuildShadeColor(baseColor));

            bool alphaClip = ShouldUseAlphaClipping(source);
            target.SetFloat("_UseAlphaClipping", alphaClip ? 1f : 0f);
            target.SetFloat("_Cutoff", alphaClip ? ReadCutoff(source) : 0.01f);

            // Softer toon response so the VRoid character receives the same scene lighting
            // without looking like a flat anime cutout inside the PBR Viking environment.
            target.SetColor("_IndirectLightConstColor", new Color(0.42f, 0.42f, 0.42f, 1f));
            target.SetFloat("_IndirectLightMultiplier", 0.32f);
            target.SetFloat("_DirectLightMultiplier", 0.9f);
            target.SetFloat("_CelShadeMidPoint", 0.08f);
            target.SetFloat("_CelShadeSoftness", 0.62f);
            target.SetFloat("_ReceiveShadowMappingAmount", 0.9f);

            // No anime outline for Tactical Echo. This is the biggest visual change that
            // helps Kaia sit inside the semi-realistic Viking Village art direction.
            target.SetFloat("_OutlineWidth", 0f);
            target.SetColor("_OutlineColor", new Color(0.16f, 0.16f, 0.16f, 1f));
            target.SetFloat("_UseEmission", 0f);

            // Kaia hair/clothes frequently use double-sided cards. Keep culling disabled
            // so those meshes do not lose backfaces after the material conversion.
            target.SetFloat("_Cull", 0f);

            EditorUtility.SetDirty(target);
            return target;
        }

        private static int ApplyToPrefabIfPresent(string prefabPath, IReadOnlyDictionary<Material, Material> materialMap)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) == null)
            {
                return 0;
            }

            GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                int changed = ReplaceMaterials(root, materialMap);
                if (changed > 0)
                {
                    PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                }
                return changed;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static int ApplyToOpenScenes(IReadOnlyDictionary<Material, Material> materialMap)
        {
            int changed = 0;
            for (int sceneIndex = 0; sceneIndex < SceneManager.sceneCount; sceneIndex++)
            {
                Scene scene = SceneManager.GetSceneAt(sceneIndex);
                if (!scene.isLoaded)
                {
                    continue;
                }

                int sceneChanged = 0;
                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    sceneChanged += ReplaceMaterials(root, materialMap);
                }

                if (sceneChanged > 0)
                {
                    changed += sceneChanged;
                    EditorSceneManager.MarkSceneDirty(scene);
                }
            }

            return changed;
        }

        private static int ReplaceMaterials(GameObject root, IReadOnlyDictionary<Material, Material> materialMap)
        {
            int changedRenderers = 0;
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);

            foreach (Renderer renderer in renderers)
            {
                Material[] materials = renderer.sharedMaterials;
                bool changed = false;

                for (int i = 0; i < materials.Length; i++)
                {
                    Material current = materials[i];
                    if (current == null)
                    {
                        continue;
                    }

                    if (materialMap.TryGetValue(current, out Material replacement) && replacement != null)
                    {
                        materials[i] = replacement;
                        changed = true;
                    }
                }

                if (!changed)
                {
                    continue;
                }

                renderer.sharedMaterials = materials;
                EditorUtility.SetDirty(renderer);
                changedRenderers++;
            }

            return changedRenderers;
        }

        private static Texture GetFirstTexture(Material source, params string[] propertyNames)
        {
            foreach (string propertyName in propertyNames)
            {
                if (source.HasProperty(propertyName))
                {
                    Texture texture = source.GetTexture(propertyName);
                    if (texture != null)
                    {
                        return texture;
                    }
                }
            }

            return null;
        }

        private static Color GetFirstColor(Material source, Color fallback, params string[] propertyNames)
        {
            foreach (string propertyName in propertyNames)
            {
                if (source.HasProperty(propertyName))
                {
                    return source.GetColor(propertyName);
                }
            }

            return fallback;
        }

        private static bool ShouldUseAlphaClipping(Material source)
        {
            string name = source.name.ToLowerInvariant();

            if (name.Contains("eyehighlight"))
            {
                // Eye highlights are usually transparent overlays. Keeping them out of the
                // cutout conversion avoids harsh square/blocked highlights.
                return false;
            }

            if (source.IsKeywordEnabled("_ALPHATEST_ON"))
            {
                return true;
            }

            if (source.HasProperty("_BlendMode") && Mathf.RoundToInt(source.GetFloat("_BlendMode")) == 1)
            {
                return true;
            }

            return name.Contains("hair") || name.Contains("face") || name.Contains("eyeline") || name.Contains("brow");
        }

        private static float ReadCutoff(Material source)
        {
            return source.HasProperty("_Cutoff")
                ? Mathf.Clamp(source.GetFloat("_Cutoff"), 0.05f, 0.95f)
                : 0.5f;
        }

        private static Color BuildShadeColor(Color baseColor)
        {
            // Preserve hue but darken/desaturate just enough to fit the environment.
            Color.RGBToHSV(baseColor, out float h, out float s, out float v);
            s *= 0.82f;
            v *= 0.62f;
            Color shade = Color.HSVToRGB(h, s, v);
            shade.a = baseColor.a;
            return shade;
        }

        private static string SanitizeFileName(string value)
        {
            foreach (char invalid in Path.GetInvalidFileNameChars())
            {
                value = value.Replace(invalid, '_');
            }

            return value.Replace(" (Instance)", string.Empty).Trim();
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
