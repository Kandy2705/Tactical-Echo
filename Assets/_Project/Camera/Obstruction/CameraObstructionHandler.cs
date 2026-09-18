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

        private readonly RaycastHit[] hits = new RaycastHit[16];
        private readonly Dictionary<Renderer, float> fadeState = new();
        private readonly HashSet<Renderer> obstructingThisFrame = new();
        private readonly List<Renderer> expiredRenderers = new();
        private readonly List<Renderer> trackedRenderers = new();
        private MaterialPropertyBlock propertyBlock;

        public float FadedAlpha => fadedAlpha;

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
