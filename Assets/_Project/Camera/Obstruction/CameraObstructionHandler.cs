using System.Collections.Generic;
using UnityEngine;

namespace TacticalEcho.CameraSystem.Obstruction
{
    /// <summary>
    /// Detects geometry between the camera and its target and fades those renderers down to
    /// <see cref="fadedAlpha"/> instead of letting them hide the player. Renderer fade ownership
    /// lives here per architecture.
    /// Note: fading only has a visible effect on materials whose Surface Type is
    /// Transparent/Fade - an Opaque URP Lit/Unlit material ignores alpha by design, so obstructing
    /// geometry must use a transparent-capable material for this to be visible.
    /// Note: PrimeTween is reserved for presentation smoothing per architecture, but only its
    /// Editor installer is currently present under Assets/Plugins/PrimeTween (the actual runtime
    /// package has not been installed in this project yet). This uses the same manual
    /// exponential-blend already used by PlayerCameraController/PlayerController and can be
    /// swapped for Tween.Custom once PrimeTween is installed.
    /// </summary>
    public sealed class CameraObstructionHandler : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [SerializeField] private Transform target;
        [SerializeField] private LayerMask obstructionMask;
        [SerializeField, Range(0.05f, 1f)] private float fadedAlpha = 0.25f;
        [SerializeField, Min(0.01f)] private float fadeBlendSharpness = 12f;
        [Tooltip("Shrinks the raycast distance so geometry right at the target (e.g. its own body) is never treated as an obstruction.")]
        [SerializeField, Min(0f)] private float targetSkinWidth = 0.35f;

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
        private MaterialPropertyBlock propertyBlock;

        private void Awake()
        {
            // An unconfigured LayerMask serialises as 0, which would silently disable both the
            // fade and the collision push. Fall back to "solid world geometry": everything
            // except the layers that must never block or fade for a camera.
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

        /// <summary>
        /// Returns where the camera may actually sit: the desired position, or a point in front
        /// of the first geometry between the pivot and it. The camera controller owns the
        /// position and calls this; this type owns what counts as an obstruction.
        /// Colliders belonging to the pivot's own hierarchy are ignored, so the character the
        /// camera is following never pushes it.
        /// </summary>
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

                // A zero distance means the cast started already overlapping; that surface
                // cannot tell us where to stop, so it is skipped rather than collapsing the
                // camera onto the pivot.
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
                Renderer hitRenderer = hits[i].collider.GetComponentInParent<Renderer>();
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
                    // Fully recovered and no longer obstructing: clear the override instead of
                    // holding a MaterialPropertyBlock on it forever.
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
