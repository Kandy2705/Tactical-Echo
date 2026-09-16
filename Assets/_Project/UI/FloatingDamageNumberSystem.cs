using TacticalEcho.Combat.Damage;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TacticalEcho.UI
{
    public static class FloatingDamageNumberSystem
    {
        private const int PoolSize = 24;
        private const float Lifetime = 0.85f;
        private const float RiseSpeed = 78f;

        private static readonly Color NormalDamageColor = new(1f, 0.82f, 0.08f, 1f);
        private static readonly Color CriticalDamageColor = new(1f, 0.24f, 0.03f, 1f);

        private sealed class Entry
        {
            public GameObject GameObject;
            public RectTransform RectTransform;
            public TextMeshProUGUI Text;
            public float EndTime;
            public Vector2 Velocity;
            public Color BaseColor;
        }

        private static Entry[] pool;
        private static int nextIndex;
        private static Camera cachedCamera;
        private static Canvas canvas;
        private static RectTransform canvasRect;

        public static void Show(in DamageFeedback feedback)
        {
            EnsurePool();
            if (pool == null || pool.Length == 0 || canvasRect == null)
            {
                return;
            }

            if (cachedCamera == null)
            {
                cachedCamera = Camera.main;
            }

            if (cachedCamera == null)
            {
                return;
            }

            Vector3 screenPoint = cachedCamera.WorldToScreenPoint(feedback.Point);
            if (screenPoint.z <= 0f)
            {
                return;
            }

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    canvasRect,
                    screenPoint,
                    null,
                    out Vector2 localPoint))
            {
                return;
            }

            Entry entry = pool[nextIndex];
            nextIndex = (nextIndex + 1) % pool.Length;

            int shownDamage = Mathf.Max(1, Mathf.RoundToInt(feedback.Amount));
            bool critical = feedback.IsCritical;

            entry.Text.text = shownDamage.ToString();
            entry.Text.fontSize = critical ? 46f : 34f;
            entry.Text.fontStyle = FontStyles.Bold;
            entry.BaseColor = critical ? CriticalDamageColor : NormalDamageColor;
            ApplyTextColor(entry.Text, entry.BaseColor, 1f);
            entry.Text.outlineColor = new Color32(0, 0, 0, 230);
            entry.Text.outlineWidth = critical ? 0.24f : 0.18f;

            Vector2 randomOffset = new(
                Random.Range(-42f, 42f),
                Random.Range(-12f, 30f));

            entry.RectTransform.anchoredPosition = localPoint + randomOffset;
            entry.RectTransform.localScale = Vector3.one * (critical ? 1.08f : Random.Range(0.94f, 1.04f));
            entry.Velocity = new Vector2(Random.Range(-18f, 18f), RiseSpeed + Random.Range(-8f, 14f));
            entry.EndTime = Time.unscaledTime + Lifetime;
            entry.GameObject.SetActive(true);

            FloatingDamageNumberUpdater.EnsureExists();
        }

        internal static void Tick()
        {
            if (pool == null)
            {
                return;
            }

            float now = Time.unscaledTime;
            float deltaTime = Time.unscaledDeltaTime;

            foreach (Entry entry in pool)
            {
                if (entry == null || entry.GameObject == null || !entry.GameObject.activeSelf)
                {
                    continue;
                }

                float remaining = entry.EndTime - now;
                if (remaining <= 0f)
                {
                    entry.GameObject.SetActive(false);
                    continue;
                }

                entry.RectTransform.anchoredPosition += entry.Velocity * deltaTime;
                entry.Velocity.x = Mathf.MoveTowards(entry.Velocity.x, 0f, 25f * deltaTime);

                float normalized = Mathf.Clamp01(remaining / Lifetime);
                float alpha = normalized < 0.45f
                    ? normalized / 0.45f
                    : 1f;

                ApplyTextColor(entry.Text, entry.BaseColor, alpha);

                float popScale = 1f + Mathf.Sin((1f - normalized) * Mathf.PI) * 0.08f;
                entry.RectTransform.localScale = Vector3.one * popScale;
            }
        }

        private static void ApplyTextColor(TextMeshProUGUI text, Color faceColor, float alpha)
        {
            if (text == null)
            {
                return;
            }

            // TMP materials can keep their own white Face Color, so changing only
            // TMP_Text.color is not always enough. Set both the material face tint
            // and the vertex tint explicitly; alpha remains on the vertex tint so
            // the number can still fade without mutating the shared font asset.
            Color32 face = faceColor;
            face.a = 255;
            text.faceColor = face;

            Color vertexColor = Color.white;
            vertexColor.a = Mathf.Clamp01(alpha);
            text.color = vertexColor;
            text.enableVertexGradient = false;
            text.overrideColorTags = true;
            text.SetVerticesDirty();
        }

        private static void EnsurePool()
        {
            if (pool != null
                && pool.Length == PoolSize
                && pool[0] != null
                && pool[0].GameObject != null
                && canvasRect != null)
            {
                return;
            }

            pool = new Entry[PoolSize];
            nextIndex = 0;
            cachedCamera = null;

            GameObject canvasObject = new("FloatingDamageCanvas", typeof(RectTransform));
            canvasObject.hideFlags = HideFlags.HideInHierarchy | HideFlags.DontSave;

            canvasRect = canvasObject.GetComponent<RectTransform>();
            canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 2200;

            CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            for (int i = 0; i < PoolSize; i++)
            {
                GameObject go = new($"FloatingDamage_{i:00}", typeof(RectTransform));
                go.hideFlags = HideFlags.HideInHierarchy | HideFlags.DontSave;
                go.transform.SetParent(canvasObject.transform, false);

                RectTransform rect = go.GetComponent<RectTransform>();
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = new Vector2(180f, 80f);

                TextMeshProUGUI text = go.AddComponent<TextMeshProUGUI>();
                text.alignment = TextAlignmentOptions.Center;
                text.textWrappingMode = TextWrappingModes.NoWrap;
                text.richText = false;
                text.raycastTarget = false;
                text.text = string.Empty;
                text.fontStyle = FontStyles.Bold;
                ApplyTextColor(text, NormalDamageColor, 1f);

                go.SetActive(false);
                pool[i] = new Entry
                {
                    GameObject = go,
                    RectTransform = rect,
                    Text = text,
                    EndTime = 0f,
                    Velocity = Vector2.zero,
                    BaseColor = NormalDamageColor
                };
            }
        }

        private sealed class FloatingDamageNumberUpdater : MonoBehaviour
        {
            private static FloatingDamageNumberUpdater instance;

            public static void EnsureExists()
            {
                if (instance != null)
                {
                    return;
                }

                GameObject updater = new("FloatingDamageNumberUpdater");
                updater.hideFlags = HideFlags.HideInHierarchy | HideFlags.DontSave;
                instance = updater.AddComponent<FloatingDamageNumberUpdater>();
            }

            private void Update()
            {
                Tick();
            }
        }
    }
}
