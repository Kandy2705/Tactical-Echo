using TMPro;
using TacticalEcho.Combat.Weapons;
using UnityEngine;
using UnityEngine.UI;

namespace TacticalEcho.UI
{
    public sealed class PlayerCombatHud : MonoBehaviour
    {
        [Header("Layout")]
        [SerializeField] private Vector2 referenceResolution = new(1920f, 1080f);
        [SerializeField] private Vector2 ammoOffset = new(-48f, 36f);
        [SerializeField, Min(12f)] private float ammoFontSize = 36f;
        [SerializeField, Min(10f)] private float stateFontSize = 20f;

        [Header("Colors")]
        [SerializeField] private Color normalAmmoColor = Color.white;
        [SerializeField] private Color emptyAmmoColor = new(1f, 0.35f, 0.25f, 1f);
        [SerializeField] private Color stateColor = new(1f, 0.85f, 0.35f, 1f);

        private WeaponController weapon;
        private bool isBound;
        private Canvas canvas;
        private TextMeshProUGUI ammoText;
        private TextMeshProUGUI stateText;

        private void Awake()
        {
            EnsureUi();
        }

        private void OnEnable()
        {
            BindWeapon();
            Refresh();
        }

        private void OnDisable()
        {
            UnbindWeapon();
        }

        private void OnDestroy()
        {
            UnbindWeapon();
            if (canvas != null)
            {
                Destroy(canvas.gameObject);
            }
        }

        public void Configure(WeaponController newWeapon)
        {
            if (weapon == newWeapon)
            {
                BindWeapon();
                Refresh();
                return;
            }

            UnbindWeapon();
            weapon = newWeapon;
            BindWeapon();
            Refresh();
        }

        private void BindWeapon()
        {
            if (!isActiveAndEnabled || weapon == null || isBound)
            {
                return;
            }

            weapon.AmmoChanged += HandleAmmoChanged;
            weapon.ReloadStarted += HandleReloadStarted;
            weapon.ReloadCompleted += HandleReloadCompleted;
            isBound = true;
        }

        private void UnbindWeapon()
        {
            if (!isBound || weapon == null)
            {
                isBound = false;
                return;
            }

            weapon.AmmoChanged -= HandleAmmoChanged;
            weapon.ReloadStarted -= HandleReloadStarted;
            weapon.ReloadCompleted -= HandleReloadCompleted;
            isBound = false;
        }

        private void HandleAmmoChanged(int magazineAmmo, int reserveAmmo)
        {
            RefreshAmmo(magazineAmmo, reserveAmmo);
            RefreshState();
        }

        private void HandleReloadStarted()
        {
            RefreshState();
        }

        private void HandleReloadCompleted()
        {
            Refresh();
        }

        private void Refresh()
        {
            EnsureUi();

            if (weapon == null)
            {
                ammoText.text = "-- / --";
                ammoText.color = normalAmmoColor;
                stateText.text = string.Empty;
                return;
            }

            RefreshAmmo(weapon.MagazineAmmo, weapon.ReserveAmmo);
            RefreshState();
        }

        private void RefreshAmmo(int magazineAmmo, int reserveAmmo)
        {
            if (ammoText == null)
            {
                return;
            }

            ammoText.text = $"<b>{magazineAmmo:00}</b> <size=70%>/ {reserveAmmo:000}</size>";
            ammoText.color = magazineAmmo <= 0 ? emptyAmmoColor : normalAmmoColor;
        }

        private void RefreshState()
        {
            if (stateText == null || weapon == null)
            {
                return;
            }

            if (weapon.IsReloading)
            {
                stateText.text = "RELOADING...";
                return;
            }

            stateText.text = weapon.MagazineAmmo <= 0 && weapon.ReserveAmmo > 0
                ? "PRESS R TO RELOAD"
                : string.Empty;
        }

        private void EnsureUi()
        {
            if (canvas != null)
            {
                return;
            }

            GameObject canvasObject = new("PlayerCombatHUD");
            canvasObject.transform.SetParent(transform, false);

            canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 900;

            CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = referenceResolution;
            scaler.matchWidthOrHeight = 0.5f;

            ammoText = CreateText(
                canvasObject.transform,
                "AmmoText",
                ammoOffset,
                new Vector2(460f, 70f),
                ammoFontSize,
                TextAlignmentOptions.BottomRight);

            stateText = CreateText(
                canvasObject.transform,
                "WeaponStateText",
                ammoOffset + new Vector2(0f, 58f),
                new Vector2(460f, 42f),
                stateFontSize,
                TextAlignmentOptions.BottomRight);
            stateText.color = stateColor;

            if (TMP_Settings.defaultFontAsset == null)
            {
                Debug.LogWarning(
                    "[PlayerCombatHud] TextMeshPro default font is missing. Import TMP Essential Resources once from Window > TextMeshPro > Import TMP Essential Resources.",
                    this);
            }
        }

        private static TextMeshProUGUI CreateText(
            Transform parent,
            string objectName,
            Vector2 anchoredPosition,
            Vector2 size,
            float fontSize,
            TextAlignmentOptions alignment)
        {
            GameObject textObject = new(objectName);
            textObject.transform.SetParent(parent, false);

            RectTransform rect = textObject.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.zero;
            rect.pivot = Vector2.zero;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;

            TextMeshProUGUI text = textObject.AddComponent<TextMeshProUGUI>();
            if (TMP_Settings.defaultFontAsset != null)
            {
                text.font = TMP_Settings.defaultFontAsset;
            }

            text.fontSize = fontSize;
            text.alignment = alignment;
            text.raycastTarget = false;
            text.enableWordWrapping = false;
            text.richText = true;
            text.outlineWidth = 0.16f;
            text.outlineColor = new Color32(0, 0, 0, 210);
            text.text = string.Empty;
            return text;
        }
    }
}
