using System.Collections.Generic;
using UnityEngine;

namespace TacticalEcho.CameraSystem.Obstruction
{













    public sealed class CameraObstructionHandler : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [SerializeField] private Transform target;
        [SerializeField] private LayerMask obstructionMask;
        [SerializeField, Range(0.05f, 1f)] private float fadedAlpha = 0.25f;
        [SerializeField, Min(0.01f)] private float fadeBlendSharpness = 12f;
        [Tooltip("Shrinks the raycast distance so geometry right at the target (e.g. its own body) is never treated as an obstruction.")]
        [SerializeField, Min(0f)] private float targetSkinWidth = 0.35f;

        [Tooltip("Fade renderers that block the view. Turn off to isolate the fade from the collision push when profiling.")]
        [SerializeField] private bool enableRendererFade = true;

        [Header("Collision")]
        [Tooltip("Pull the camera in front of geometry between it and the pivot, instead of letting it clip through walls.")]
        [SerializeField] private bool pushCameraOutOfGeometry = true;
        [SerializeField, Min(0.01f)] private float collisionRadius = 0.22f;
        [Tooltip("Extra gap kept between the camera and the surface it stopped against.")]
        [SerializeField, Min(0f)] private float collisionSkin = 0.08f;

        private readonly RaycastHit[] hits = new RaycastHit[16];
        private readonly RaycastHit[] collisionHits = new RaycastHit[16];
        private readonly Dictionary<Renderer, float> fadeState = new();
        private readonly HashSet<Renderer> obstructingThisFrame = new();
        private readonly List<Renderer> expiredRenderers = new();
        private readonly List<Renderer> trackedRenderers = new();


        private readonly Dictionary<Collider, Renderer> rendererByCollider = new();
        private MaterialPropertyBlock propertyBlock;

        private void Awake()
        {



            if (obstructionMask.value == 0)
            {
                int mask = ~0;
                ExcludeLayer(ref mask, "Ignore Raycast");
                ExcludeLayer(ref mask, "Water");
                ExcludeLayer(ref mask, "UI");
                ExcludeLayer(ref mask, TacticalEcho.Combat.Damage.DamageHitZone.HitZoneLayerName);
                obstructionMask = mask;
            }
        }

        private static void ExcludeLayer(ref int mask, string layerName)
        {
            int layer = LayerMask.NameToLayer(layerName);
            if (layer >= 0)
            {
                mask &= ~(1 << layer);
            }
        }


        public float FadedAlpha => fadedAlpha;

        public void Configure(Transform newTarget)
        {
            target = newTarget;
        }








        public Vector3 ResolveCameraPosition(Transform pivot, Vector3 desiredPosition)
        {
            if (!pushCameraOutOfGeometry || pivot == null)
            {
                return desiredPosition;
            }

            Vector3 pivotPosition = pivot.position;
            Vector3 offset = desiredPosition - pivotPosition;
            float distance = offset.magnitude;
            if (distance <= Mathf.Epsilon)
            {
                return desiredPosition;
            }

            Vector3 direction = offset / distance;
            int hitCount = Physics.SphereCastNonAlloc(
                pivotPosition,
                collisionRadius,
                direction,
                collisionHits,
                distance,
                obstructionMask,
                QueryTriggerInteraction.Ignore);

            Transform pivotRoot = pivot.root;
            float nearest = float.PositiveInfinity;

            for (int i = 0; i < hitCount; i++)
            {
                Collider collider = collisionHits[i].collider;
                if (collider == null || collider.transform.IsChildOf(pivotRoot))
                {
                    continue;
                }




                float hitDistance = collisionHits[i].distance;
                if (hitDistance > 0f && hitDistance < nearest)
                {
                    nearest = hitDistance;
                }
            }

            if (float.IsPositiveInfinity(nearest))
            {
                return desiredPosition;
            }

            return pivotPosition + direction * Mathf.Max(0f, nearest - collisionSkin);
        }

        private void LateUpdate()
        {
            obstructingThisFrame.Clear();

            if (!enableRendererFade)
            {
                if (fadeState.Count > 0)
                {
                    ClearAllFades();
                }

                return;
            }

            if (target != null)
            {
                ScanObstructions();
            }

            ApplyFadeState(Time.unscaledDeltaTime);
        }

        private void ScanObstructions()
        {
            Vector3 offset = target.position - transform.position;
            float distance = offset.magnitude - targetSkinWidth;
            if (distance <= 0f)
            {
                return;
            }

            Vector3 direction = offset.normalized;
            int hitCount = Physics.RaycastNonAlloc(
                transform.position,
                direction,
                hits,
                distance,
                obstructionMask,
                QueryTriggerInteraction.Ignore);

            for (int i = 0; i < hitCount; i++)
            {
                Renderer hitRenderer = ResolveRenderer(hits[i].collider);
                if (hitRenderer == null)
                {
                    continue;
                }

                obstructingThisFrame.Add(hitRenderer);
                if (!fadeState.ContainsKey(hitRenderer))
                {
                    fadeState[hitRenderer] = 1f;
                }
            }
        }

        private void ApplyFadeState(float deltaTime)
        {
            if (fadeState.Count == 0)
            {
                return;
            }

            propertyBlock ??= new MaterialPropertyBlock();
            float blend = 1f - Mathf.Exp(-fadeBlendSharpness * deltaTime);

            expiredRenderers.Clear();
            trackedRenderers.Clear();
            trackedRenderers.AddRange(fadeState.Keys);

            foreach (Renderer targetRenderer in trackedRenderers)
            {
                if (targetRenderer == null)
                {
                    expiredRenderers.Add(targetRenderer);
                    continue;
                }

                bool isObstructing = obstructingThisFrame.Contains(targetRenderer);
                float targetValue = isObstructing ? fadedAlpha : 1f;
                float currentValue = Mathf.Lerp(fadeState[targetRenderer], targetValue, blend);
                fadeState[targetRenderer] = currentValue;

                if (!isObstructing && Mathf.Abs(currentValue - 1f) <= 0.01f)
                {


                    targetRenderer.SetPropertyBlock(null);
                    expiredRenderers.Add(targetRenderer);
                    continue;
                }

                ApplyAlpha(targetRenderer, currentValue);
            }

            foreach (Renderer expired in expiredRenderers)
            {
                fadeState.Remove(expired);
            }
        }

        private Renderer ResolveRenderer(Collider hitCollider)
        {
            if (hitCollider == null)
            {
                return null;
            }

            if (rendererByCollider.TryGetValue(hitCollider, out Renderer cached))
            {
                return cached;
            }

            Renderer resolved = hitCollider.GetComponentInParent<Renderer>();
            rendererByCollider[hitCollider] = resolved;
            return resolved;
        }






        private void ClearAllFades()
        {
            trackedRenderers.Clear();
            trackedRenderers.AddRange(fadeState.Keys);

            foreach (Renderer targetRenderer in trackedRenderers)
            {
                if (targetRenderer != null)
                {
                    targetRenderer.SetPropertyBlock(null);
                }
            }

            fadeState.Clear();
            trackedRenderers.Clear();
        }

        private void OnDisable()
        {
            ClearAllFades();
        }

        private void ApplyAlpha(Renderer targetRenderer, float alpha)
        {
            targetRenderer.GetPropertyBlock(propertyBlock);
            Color color = targetRenderer.sharedMaterial != null && targetRenderer.sharedMaterial.HasProperty(BaseColorId)
                ? targetRenderer.sharedMaterial.GetColor(BaseColorId)
                : Color.white;
            color.a = alpha;
            propertyBlock.SetColor(BaseColorId, color);
            targetRenderer.SetPropertyBlock(propertyBlock);
        }
    }
}
