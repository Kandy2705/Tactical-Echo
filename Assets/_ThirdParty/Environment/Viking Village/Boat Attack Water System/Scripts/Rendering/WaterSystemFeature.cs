using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace WaterSystem
{
    public class WaterSystemFeature : ScriptableRendererFeature
    {
        private sealed class WaterFxPass : ScriptableRenderPass
        {
            private const string RenderWaterFxTag = "Render Water FX";
            private static readonly int WaterFxMap = Shader.PropertyToID("_WaterFXMap");

            private readonly ProfilingSampler profilingSampler = new(RenderWaterFxTag);
            private readonly ShaderTagId waterFxShaderTag = new("WaterFX");
            private readonly Color clearColor = new(0f, 0.5f, 0.5f, 0.5f);
            private FilteringSettings filteringSettings = new(RenderQueueRange.transparent);
            private RTHandle waterFx;

            public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
            {
                RenderTextureDescriptor descriptor = renderingData.cameraData.cameraTargetDescriptor;
                descriptor.depthBufferBits = 0;
                descriptor.width = Mathf.Max(1, descriptor.width / 2);
                descriptor.height = Mathf.Max(1, descriptor.height / 2);
                descriptor.msaaSamples = 1;
                descriptor.colorFormat = RenderTextureFormat.Default;

                RenderingUtils.ReAllocateIfNeeded(
                    ref waterFx,
                    descriptor,
                    FilterMode.Bilinear,
                    TextureWrapMode.Clamp,
                    name: "_WaterFXMap");

                ConfigureTarget(waterFx);
                ConfigureClear(ClearFlag.Color, clearColor);
            }

            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                if (waterFx == null)
                {
                    return;
                }

                CommandBuffer cmd = CommandBufferPool.Get();
                using (new ProfilingScope(cmd, profilingSampler))
                {
                    context.ExecuteCommandBuffer(cmd);
                    cmd.Clear();

                    DrawingSettings drawSettings = CreateDrawingSettings(
                        waterFxShaderTag,
                        ref renderingData,
                        SortingCriteria.CommonTransparent);

                    context.DrawRenderers(
                        renderingData.cullResults,
                        ref drawSettings,
                        ref filteringSettings);

                    cmd.SetGlobalTexture(WaterFxMap, waterFx.nameID);
                }

                context.ExecuteCommandBuffer(cmd);
                CommandBufferPool.Release(cmd);
            }

            public void Dispose()
            {
                waterFx?.Release();
                waterFx = null;
            }
        }

        private sealed class WaterCausticsPass : ScriptableRenderPass
        {
            private const string RenderWaterCausticsTag = "Render Water Caustics";
            private readonly ProfilingSampler profilingSampler = new(RenderWaterCausticsTag);
            private static Mesh mesh;

            public Material WaterCausticMaterial { get; set; }

            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                Camera camera = renderingData.cameraData.camera;
                if (camera.cameraType == CameraType.Preview || WaterCausticMaterial == null)
                {
                    return;
                }

                CommandBuffer cmd = CommandBufferPool.Get();
                using (new ProfilingScope(cmd, profilingSampler))
                {
                    Matrix4x4 sunMatrix = RenderSettings.sun != null
                        ? RenderSettings.sun.transform.localToWorldMatrix
                        : Matrix4x4.TRS(Vector3.zero, Quaternion.Euler(-45f, 45f, 0f), Vector3.one);

                    WaterCausticMaterial.SetMatrix("_MainLightDir", sunMatrix);

                    if (mesh == null)
                    {
                        mesh = GenerateCausticsMesh(1000f);
                    }

                    Vector3 position = camera.transform.position;
                    position.y = 0f;
                    Matrix4x4 matrix = Matrix4x4.TRS(position, Quaternion.identity, Vector3.one);
                    cmd.DrawMesh(mesh, matrix, WaterCausticMaterial, 0, 0);
                }

                context.ExecuteCommandBuffer(cmd);
                CommandBufferPool.Release(cmd);
            }
        }

        [System.Serializable]
        public class WaterSystemSettings
        {
            [Header("Caustics Settings")]
            [Range(0.1f, 1f)]
            public float causticScale = 0.25f;

            public float causticBlendDistance = 3f;

            [Header("Advanced Settings")]
            public DebugMode debug = DebugMode.Disabled;

            public enum DebugMode
            {
                Disabled,
                WaterEffects,
                Caustics
            }
        }

        [SerializeField] private WaterSystemSettings settings = new();
        [HideInInspector, SerializeField] private Shader causticShader;
        [HideInInspector, SerializeField] private Texture2D causticTexture;

        private WaterFxPass waterFxPass;
        private WaterCausticsPass causticsPass;
        private Material causticMaterial;

        private static readonly int SrcBlend = Shader.PropertyToID("_SrcBlend");
        private static readonly int DstBlend = Shader.PropertyToID("_DstBlend");
        private static readonly int Size = Shader.PropertyToID("_Size");
        private static readonly int CausticTexture = Shader.PropertyToID("_CausticMap");
        private static readonly int BlendDistance = Shader.PropertyToID("_BlendDistance");

        public override void Create()
        {
            waterFxPass = new WaterFxPass
            {
                renderPassEvent = RenderPassEvent.BeforeRenderingOpaques
            };

            causticsPass = new WaterCausticsPass();

            causticShader = causticShader != null
                ? causticShader
                : Shader.Find("Hidden/BoatAttack/Caustics");

            if (causticShader == null)
            {
                return;
            }

            CoreUtils.Destroy(causticMaterial);
            causticMaterial = CoreUtils.CreateEngineMaterial(causticShader);
            causticMaterial.SetFloat(BlendDistance, settings.causticBlendDistance);

            if (causticTexture == null)
            {
#if UNITY_EDITOR
                causticTexture = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(
                    "Packages/com.verasl.water-system/Textures/WaterSurface_single.tif");
#endif
            }

            causticMaterial.SetTexture(CausticTexture, causticTexture);

            switch (settings.debug)
            {
                case WaterSystemSettings.DebugMode.Caustics:
                    causticMaterial.SetFloat(SrcBlend, 1f);
                    causticMaterial.SetFloat(DstBlend, 0f);
                    causticMaterial.EnableKeyword("_DEBUG");
                    causticsPass.renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing;
                    break;

                case WaterSystemSettings.DebugMode.WaterEffects:
                    causticsPass.renderPassEvent = RenderPassEvent.AfterRenderingSkybox;
                    break;

                default:
                    causticMaterial.SetFloat(SrcBlend, 2f);
                    causticMaterial.SetFloat(DstBlend, 0f);
                    causticMaterial.DisableKeyword("_DEBUG");
                    causticsPass.renderPassEvent = (RenderPassEvent)((int)RenderPassEvent.AfterRenderingSkybox + 1);
                    break;
            }

            causticMaterial.SetFloat(Size, settings.causticScale);
            causticsPass.WaterCausticMaterial = causticMaterial;
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (waterFxPass != null)
            {
                renderer.EnqueuePass(waterFxPass);
            }

            if (causticsPass != null)
            {
                renderer.EnqueuePass(causticsPass);
            }
        }

        protected override void Dispose(bool disposing)
        {
            waterFxPass?.Dispose();
            waterFxPass = null;
            causticsPass = null;

            CoreUtils.Destroy(causticMaterial);
            causticMaterial = null;
        }

        private static Mesh GenerateCausticsMesh(float size)
        {
            Mesh generatedMesh = new();
            float halfSize = size * 0.5f;

            generatedMesh.vertices = new[]
            {
                new Vector3(-halfSize, 0f, -halfSize),
                new Vector3(halfSize, 0f, -halfSize),
                new Vector3(-halfSize, 0f, halfSize),
                new Vector3(halfSize, 0f, halfSize)
            };

            generatedMesh.triangles = new[]
            {
                0, 2, 1,
                2, 3, 1
            };

            generatedMesh.RecalculateBounds();
            return generatedMesh;
        }
    }
}
