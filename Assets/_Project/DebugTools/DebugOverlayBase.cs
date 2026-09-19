using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TacticalEcho.DebugTools
{
    /// <summary>
    /// Shared shape for every debug overlay: an optional authored TMP target, a screen-space
    /// text built on demand when none is assigned, and one text rebuild per frame.
    /// Overlays observe runtime systems; nothing in gameplay may depend on them.
    /// </summary>
    public abstract class DebugOverlayBase : MonoBehaviour
    {
        [Header("Debug Output")]
        [Tooltip("Optional. When empty the overlay builds its own screen-space text, so it works on a bare GameObject.")]
        [SerializeField] protected TMP_Text outputText;
        [SerializeField] private bool buildOwnTextWhenMissing = true;
        [Tooltip("Anchored from the top-left of the screen.")]
        [SerializeField] private Vector2 screenOffset = new(24f, -24f);
        [SerializeField] private Vector2 panelSize = new(560f, 420f);
        [SerializeField, Min(8f)] private float fontSize = 16f;
        [SerializeField] private Color textColor = new(0.78f, 0.94f, 1f, 1f);
        [SerializeField] private int sortingOrder = 950;

        private readonly StringBuilder builder = new();

        /// <summary>Name used for the generated canvas, and as the overlay's heading.</summary>
        protected abstract string OverlayName { get; }

        /// <summary>Append the current frame's debug text. Read-only with respect to gameplay.</summary>
        protected abstract void BuildText(StringBuilder text);

        protected virtual void Awake()
        {
            EnsureOutputText();
        }

        private void LateUpdate()
        {
            if (outputText == null)
            {
                return;
            }

            builder.Clear();
            builder.Append("== ").Append(OverlayName).AppendLine(" ==");
            BuildText(builder);
            outputText.text = builder.ToString();
        }

        protected static string Seconds(float seconds)
        {
            return Mathf.Max(0f, seconds).ToString("0.00") + "s";
        }

        private void EnsureOutputText()
        {
            if (outputText != null || !buildOwnTextWhenMissing)
            {
                return;
            }

            GameObject canvasObject = new(OverlayName + "Canvas");
            canvasObject.transform.SetParent(transform, false);

            Canvas canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;

            CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            GameObject textObject = new("Text");
            textObject.transform.SetParent(canvasObject.transform, false);

            RectTransform rect = textObject.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = screenOffset;
            rect.sizeDelta = panelSize;

            TextMeshProUGUI text = textObject.AddComponent<TextMeshProUGUI>();
            if (TMP_Settings.defaultFontAsset != null)
            {
                text.font = TMP_Settings.defaultFontAsset;
            }
            else
            {
                Debug.LogWarning(
                    $"[{OverlayName}] TextMeshPro default font is missing. Import TMP Essential Resources once from " +
                    "Window > TextMeshPro > Import TMP Essential Resources.",
                    this);
            }

            text.fontSize = fontSize;
            text.color = textColor;
            text.alignment = TextAlignmentOptions.TopLeft;
            text.raycastTarget = false;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.richText = true;
            text.outlineWidth = 0.16f;
            text.outlineColor = new Color32(0, 0, 0, 210);
            text.text = string.Empty;

            outputText = text;
        }
    }
}
