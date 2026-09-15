using TacticalEcho.Combat.Damage;
using UnityEngine;
using UnityEngine.Rendering;

namespace TacticalEcho.Combat.Impacts
{
    public enum ImpactSurfaceType
    {
        Default,
        Concrete,
        Wood,
        Metal,
        Flesh
    }

    /// <summary>
    /// Optional authoring hook for surfaces that need a specific impact response.
    /// Without this component, damageable targets are treated as Flesh and everything else as Default.
    /// </summary>
    public sealed class SurfaceTypeOverride : MonoBehaviour
    {
        [SerializeField] private ImpactSurfaceType surfaceType = ImpactSurfaceType.Default;

        public ImpactSurfaceType SurfaceType => surfaceType;
    }

    /// <summary>
    /// Centralized hit presentation for weapons. Pools are created lazily on the first matching hit,
    /// so cloned/disabled weapons do not populate the Hierarchy with unused runtime objects.
    /// </summary>
    public sealed class SurfaceImpactSystem : MonoBehaviour
    {
        private const int BulletDecalPoolSize = 48;
        private const int BloodEmitterPoolSize = 8;
        private const int BulletTextureSize = 64;

        private static SurfaceImpactSystem instance;

        private BulletDecal[] bulletDecals;
        private int nextBulletDecal;
        private Mesh bulletDecalMesh;
        private Material bulletDecalMaterial;
        private Texture2D bulletDecalTexture;

        private BloodEmitter[] bloodEmitters;
        private int nextBloodEmitter;
        private Material bloodMaterial;

        private sealed class BulletDecal
        {
            public GameObject GameObject;
            public Transform Transform;
        }

        private sealed class BloodEmitter
        {
            public GameObject GameObject;
            public ParticleSystem Particles;
        }

        public static void HandleHit(in RaycastHit hit, bool damageApplied)
        {
            if (hit.collider == null)
            {
                return;
            }

            GetOrCreate().PlayImpact(hit, damageApplied);
        }

        private static SurfaceImpactSystem GetOrCreate()
        {
            if (instance != null)
            {
                return instance;
            }

            instance = Object.FindFirstObjectByType<SurfaceImpactSystem>();
            if (instance != null)
            {
                return instance;
            }

            GameObject root = new("RuntimeSurfaceImpacts");
            root.hideFlags = HideFlags.HideInHierarchy | HideFlags.DontSave;
            instance = root.AddComponent<SurfaceImpactSystem>();
            return instance;
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
        }

        private void OnDestroy()
        {
            if (instance == this)
            {
                instance = null;
            }

            if (bulletDecalMesh != null)
            {
                Destroy(bulletDecalMesh);
            }

            if (bulletDecalMaterial != null)
            {
                Destroy(bulletDecalMaterial);
            }

            if (bulletDecalTexture != null)
            {
                Destroy(bulletDecalTexture);
            }

            if (bloodMaterial != null)
            {
                Destroy(bloodMaterial);
            }
        }

        private void PlayImpact(in RaycastHit hit, bool damageApplied)
        {
            ImpactSurfaceType surfaceType = ResolveSurfaceType(hit.collider, damageApplied);

            if (surfaceType == ImpactSurfaceType.Flesh)
            {
                PlayBlood(hit);
                return;
            }

            PlaceBulletDecal(hit, surfaceType);
        }

        private static ImpactSurfaceType ResolveSurfaceType(Collider collider, bool damageApplied)
        {
            SurfaceTypeOverride surfaceOverride = collider.GetComponentInParent<SurfaceTypeOverride>();
            if (surfaceOverride != null && surfaceOverride.SurfaceType != ImpactSurfaceType.Default)
            {
                return surfaceOverride.SurfaceType;
            }

            IDamageable damageable = collider.GetComponentInParent<IDamageable>();
            return damageApplied || damageable != null
                ? ImpactSurfaceType.Flesh
                : ImpactSurfaceType.Default;
        }

        private void PlaceBulletDecal(in RaycastHit hit, ImpactSurfaceType surfaceType)
        {
            EnsureBulletDecalPool();
            if (bulletDecals == null || bulletDecals.Length == 0)
            {
                return;
            }

            BulletDecal decal = bulletDecals[nextBulletDecal];
            nextBulletDecal = (nextBulletDecal + 1) % bulletDecals.Length;

            float size = surfaceType == ImpactSurfaceType.Metal
                ? Random.Range(0.055f, 0.075f)
                : Random.Range(0.075f, 0.11f);

            decal.Transform.position = hit.point + hit.normal * 0.004f;
            decal.Transform.rotation = Quaternion.LookRotation(hit.normal)
                                     * Quaternion.AngleAxis(Random.Range(0f, 360f), Vector3.forward);
            decal.Transform.localScale = Vector3.one * size;
            decal.GameObject.SetActive(true);
        }

        private void EnsureBulletDecalPool()
        {
            if (bulletDecals != null)
            {
                return;
            }

            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null)
            {
                shader = Shader.Find("Universal Render Pipeline/Unlit");
            }

            if (shader == null)
            {
                bulletDecals = System.Array.Empty<BulletDecal>();
                return;
            }

            bulletDecalTexture = CreateBulletHoleTexture();
            bulletDecalMesh = CreateQuadMesh();
            bulletDecalMaterial = new Material(shader)
            {
                name = "Runtime Bullet Hole Material",
                mainTexture = bulletDecalTexture,
                color = Color.white,
                hideFlags = HideFlags.DontSave
            };

            ConfigureTransparentMaterial(bulletDecalMaterial);

            bulletDecals = new BulletDecal[BulletDecalPoolSize];
            for (int i = 0; i < bulletDecals.Length; i++)
            {
                GameObject decalObject = new($"BulletDecal_{i:00}");
                decalObject.hideFlags = HideFlags.HideInHierarchy | HideFlags.DontSave;
                decalObject.transform.SetParent(transform, false);

                MeshFilter filter = decalObject.AddComponent<MeshFilter>();
                filter.sharedMesh = bulletDecalMesh;

                MeshRenderer renderer = decalObject.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = bulletDecalMaterial;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                renderer.lightProbeUsage = LightProbeUsage.Off;
                renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;

                decalObject.SetActive(false);
                bulletDecals[i] = new BulletDecal
                {
                    GameObject = decalObject,
                    Transform = decalObject.transform
                };
            }
        }

        private void PlayBlood(in RaycastHit hit)
        {
            EnsureBloodPool();
            if (bloodEmitters == null || bloodEmitters.Length == 0)
            {
                return;
            }

            BloodEmitter emitter = bloodEmitters[nextBloodEmitter];
            nextBloodEmitter = (nextBloodEmitter + 1) % bloodEmitters.Length;

            emitter.GameObject.transform.position = hit.point + hit.normal * 0.015f;
            emitter.GameObject.transform.rotation = Quaternion.LookRotation(hit.normal, Vector3.up);

            ParticleSystem particles = emitter.Particles;
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            particles.Play(true);
            particles.Emit(Random.Range(11, 17));
        }

        private void EnsureBloodPool()
        {
            if (bloodEmitters != null)
            {
                return;
            }

            Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null)
            {
                shader = Shader.Find("Particles/Standard Unlit");
            }
            if (shader == null)
            {
                shader = Shader.Find("Sprites/Default");
            }

            if (shader != null)
            {
                bloodMaterial = new Material(shader)
                {
                    name = "Runtime Blood Particle Material",
                    hideFlags = HideFlags.DontSave
                };
            }

            bloodEmitters = new BloodEmitter[BloodEmitterPoolSize];
            for (int i = 0; i < bloodEmitters.Length; i++)
            {
                GameObject emitterObject = new($"BloodImpact_{i:00}");
                emitterObject.hideFlags = HideFlags.HideInHierarchy | HideFlags.DontSave;
                emitterObject.transform.SetParent(transform, false);

                ParticleSystem particles = emitterObject.AddComponent<ParticleSystem>();
                ParticleSystem.MainModule main = particles.main;
                main.playOnAwake = false;
                main.loop = false;
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                main.maxParticles = 40;
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.24f, 0.48f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(2.2f, 4.8f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.018f, 0.055f);
                main.gravityModifier = 1.15f;
                main.startColor = new ParticleSystem.MinMaxGradient(
                    new Color(0.32f, 0.005f, 0.005f, 1f),
                    new Color(0.72f, 0.025f, 0.02f, 1f));

                ParticleSystem.EmissionModule emission = particles.emission;
                emission.enabled = false;

                ParticleSystem.ShapeModule shape = particles.shape;
                shape.enabled = true;
                shape.shapeType = ParticleSystemShapeType.Cone;
                shape.angle = 24f;
                shape.radius = 0.012f;
                shape.length = 0.08f;

                ParticleSystem.SizeOverLifetimeModule sizeOverLifetime = particles.sizeOverLifetime;
                sizeOverLifetime.enabled = true;
                sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(
                    1f,
                    new AnimationCurve(
                        new Keyframe(0f, 0.45f),
                        new Keyframe(0.18f, 1f),
                        new Keyframe(1f, 0.15f)));

                ParticleSystemRenderer renderer = particles.GetComponent<ParticleSystemRenderer>();
                renderer.renderMode = ParticleSystemRenderMode.Billboard;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                if (bloodMaterial != null)
                {
                    renderer.sharedMaterial = bloodMaterial;
                }

                particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                bloodEmitters[i] = new BloodEmitter
                {
                    GameObject = emitterObject,
                    Particles = particles
                };
            }
        }

        private static void ConfigureTransparentMaterial(Material material)
        {
            if (material == null)
            {
                return;
            }

            if (material.HasProperty("_Surface"))
            {
                material.SetFloat("_Surface", 1f);
            }
            if (material.HasProperty("_ZWrite"))
            {
                material.SetFloat("_ZWrite", 0f);
            }

            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int)RenderQueue.Transparent;
        }

        private static Mesh CreateQuadMesh()
        {
            Mesh mesh = new()
            {
                name = "Runtime Bullet Decal Quad",
                hideFlags = HideFlags.DontSave,
                vertices = new[]
                {
                    new Vector3(-0.5f, -0.5f, 0f),
                    new Vector3(0.5f, -0.5f, 0f),
                    new Vector3(-0.5f, 0.5f, 0f),
                    new Vector3(0.5f, 0.5f, 0f)
                },
                uv = new[]
                {
                    new Vector2(0f, 0f),
                    new Vector2(1f, 0f),
                    new Vector2(0f, 1f),
                    new Vector2(1f, 1f)
                },
                triangles = new[] { 0, 1, 2, 2, 1, 3 }
            };

            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Texture2D CreateBulletHoleTexture()
        {
            Texture2D texture = new(
                BulletTextureSize,
                BulletTextureSize,
                TextureFormat.RGBA32,
                false,
                true)
            {
                name = "Runtime Bullet Hole Texture",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.DontSave
            };

            Color32[] pixels = new Color32[BulletTextureSize * BulletTextureSize];
            Vector2 center = new((BulletTextureSize - 1) * 0.5f, (BulletTextureSize - 1) * 0.5f);
            float maxRadius = BulletTextureSize * 0.5f;

            for (int y = 0; y < BulletTextureSize; y++)
            {
                for (int x = 0; x < BulletTextureSize; x++)
                {
                    Vector2 delta = new Vector2(x, y) - center;
                    float radius = delta.magnitude / maxRadius;
                    float angle = Mathf.Atan2(delta.y, delta.x);

                    float core = 1f - Mathf.SmoothStep(0.10f, 0.20f, radius);
                    float scorch = Mathf.Clamp01(1f - radius / 0.48f) * 0.55f;

                    float cracks = 0f;
                    if (radius > 0.12f && radius < 0.58f)
                    {
                        float spoke = Mathf.Abs(Mathf.Sin(angle * 6f + radius * 10f));
                        cracks = Mathf.Clamp01((0.12f - spoke) * 8f) * (1f - radius / 0.62f) * 0.7f;
                    }

                    float alpha = Mathf.Clamp01(Mathf.Max(core, Mathf.Max(scorch, cracks)));
                    byte a = (byte)Mathf.RoundToInt(alpha * 230f);
                    byte shade = (byte)Mathf.RoundToInt(Mathf.Lerp(10f, 40f, Mathf.Clamp01(radius * 1.8f)));
                    pixels[y * BulletTextureSize + x] = new Color32(shade, shade, shade, a);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }
    }
}
