using TacticalEcho.Character.Player;
using TacticalEcho.Combat.Damage;
using UnityEngine;
using UnityEngine.UI;

namespace TacticalEcho.CameraSystem
{
    public enum CameraMode
    {
        Explore,
        Aim
    }

    public sealed class PlayerCameraController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Transform target;
        [SerializeField] private PlayerInputReader input;

        [Header("Offsets")]
        [Tooltip("Offset is relative to CameraTarget. Positive X places the camera over the right shoulder.")]
        [SerializeField] private Vector3 exploreOffset = new(0.65f, 0.15f, -3.6f);
        [SerializeField] private Vector3 aimOffset = new(0.55f, 0.08f, -1.45f);

        [Header("Field Of View")]
        [SerializeField, Range(30f, 90f)] private float exploreFieldOfView = 60f;
        [SerializeField, Range(30f, 90f)] private float aimFieldOfView = 45f;
        [SerializeField, Min(0.01f)] private float aimBlendSharpness = 14f;

        [Header("Look")]
        [SerializeField, Min(0.01f)] private float mouseSensitivity = 0.08f;
        [SerializeField, Min(0.01f)] private float gamepadSensitivity = 120f;
        [SerializeField] private float minPitch = -35f;
        [SerializeField] private float maxPitch = 70f;

        [Header("Follow")]
        [SerializeField, Min(0.01f)] private float followSharpness = 18f;
        [SerializeField, Min(0.01f)] private float rotationSharpness = 24f;

        [Header("Crosshair")]
        [SerializeField] private bool showCrosshairOnlyWhileAiming = true;
        [SerializeField, Min(1f)] private float crosshairLength = 8f;
        [SerializeField, Min(0f)] private float crosshairGap = 5f;
        [SerializeField, Min(1f)] private float crosshairThickness = 2f;
        [SerializeField, Min(0.1f)] private float crosshairMaxDistance = 150f;
        [SerializeField] private LayerMask crosshairMask = ~0;
        [SerializeField] private Color crosshairColor = Color.white;
        [SerializeField] private Color targetCrosshairColor = new(1f, 0.25f, 0.2f, 1f);

        private float yaw;
        private float pitch;
        private float shoulderSign = 1f;
        private Vector3 currentOffset;
        private Camera gameplayCamera;

        private Canvas crosshairCanvas;
        private Image[] crosshairParts;

        public CameraMode Mode { get; private set; } = CameraMode.Explore;
        public Vector3 PlanarForward => Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
        public Vector3 PlanarRight => Vector3.ProjectOnPlane(transform.right, Vector3.up).normalized;

        private void Awake()
        {
            gameplayCamera = GetComponent<Camera>();
            currentOffset = exploreOffset;
            CreateCrosshair();
        }

        private void Start()
        {
            SyncLookAnglesFromTransform();

            if (gameplayCamera != null)
            {
                gameplayCamera.fieldOfView = exploreFieldOfView;
            }
        }

        private void LateUpdate()
        {
            if (target == null)
            {
                UpdateCrosshair();
                return;
            }

            UpdateLook();
            UpdateTransform();
            UpdateCrosshair();
        }

        private void OnDestroy()
        {
            if (crosshairCanvas != null)
            {
                Destroy(crosshairCanvas.gameObject);
            }
        }

        public void Configure(Transform newTarget, PlayerInputReader inputReader)
        {
            target = newTarget;
            input = inputReader;
            currentOffset = Mode == CameraMode.Aim ? aimOffset : exploreOffset;
            SyncLookAnglesFromTransform();
        }

        public void SetMode(CameraMode mode)
        {
            Mode = mode;
        }

        public void SwitchShoulder()
        {
            shoulderSign *= -1f;
        }

        private void UpdateLook()
        {
            if (input == null)
            {
                return;
            }

            Vector2 look = input.Look;
            if (look.sqrMagnitude <= 0f)
            {
                return;
            }

            float scale = input.IsPointerLook
                ? mouseSensitivity
                : gamepadSensitivity * Time.unscaledDeltaTime;

            yaw += look.x * scale;
            pitch -= look.y * scale;
            pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
        }

        private void UpdateTransform()
        {
            Quaternion desiredRotation = Quaternion.Euler(pitch, yaw, 0f);
            Vector3 targetOffset = Mode == CameraMode.Aim ? aimOffset : exploreOffset;
            targetOffset.x = Mathf.Abs(targetOffset.x) * shoulderSign;

            float aimBlend = 1f - Mathf.Exp(-aimBlendSharpness * Time.unscaledDeltaTime);
            currentOffset = Vector3.Lerp(currentOffset, targetOffset, aimBlend);

            Vector3 desiredPosition = target.position + desiredRotation * currentOffset;

            float positionT = 1f - Mathf.Exp(-followSharpness * Time.unscaledDeltaTime);
            float rotationT = 1f - Mathf.Exp(-rotationSharpness * Time.unscaledDeltaTime);

            transform.position = Vector3.Lerp(transform.position, desiredPosition, positionT);
            transform.rotation = Quaternion.Slerp(transform.rotation, desiredRotation, rotationT);

            if (gameplayCamera != null)
            {
                float targetFov = Mode == CameraMode.Aim ? aimFieldOfView : exploreFieldOfView;
                gameplayCamera.fieldOfView = Mathf.Lerp(gameplayCamera.fieldOfView, targetFov, aimBlend);
            }
        }

        private void UpdateCrosshair()
        {
            if (crosshairCanvas == null || crosshairParts == null)
            {
                return;
            }

            bool visible = !showCrosshairOnlyWhileAiming || Mode == CameraMode.Aim;
            if (crosshairCanvas.gameObject.activeSelf != visible)
            {
                crosshairCanvas.gameObject.SetActive(visible);
            }

            if (!visible)
            {
                return;
            }

            bool isDamageableTarget = false;
            if (Physics.Raycast(
                    transform.position,
                    transform.forward,
                    out RaycastHit hit,
                    crosshairMaxDistance,
                    crosshairMask,
                    QueryTriggerInteraction.Ignore))
            {
                IDamageable damageable = hit.collider.GetComponentInParent<IDamageable>();
                isDamageableTarget = damageable != null && damageable.IsAlive;
            }

            Color color = isDamageableTarget ? targetCrosshairColor : crosshairColor;
            foreach (Image part in crosshairParts)
            {
                if (part != null)
                {
                    part.color = color;
                }
            }
        }

        private void CreateCrosshair()
        {
            if (crosshairCanvas != null)
            {
                return;
            }

            GameObject canvasObject = new("AimCrosshairCanvas");
            canvasObject.transform.SetParent(transform, false);

            crosshairCanvas = canvasObject.AddComponent<Canvas>();
            crosshairCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            crosshairCanvas.sortingOrder = 1000;

            CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            crosshairParts = new Image[5];
            crosshairParts[0] = CreateCrosshairPart(canvasObject.transform, "Left", new Vector2(crosshairLength, crosshairThickness), new Vector2(-(crosshairGap + crosshairLength * 0.5f), 0f));
            crosshairParts[1] = CreateCrosshairPart(canvasObject.transform, "Right", new Vector2(crosshairLength, crosshairThickness), new Vector2(crosshairGap + crosshairLength * 0.5f, 0f));
            crosshairParts[2] = CreateCrosshairPart(canvasObject.transform, "Top", new Vector2(crosshairThickness, crosshairLength), new Vector2(0f, crosshairGap + crosshairLength * 0.5f));
            crosshairParts[3] = CreateCrosshairPart(canvasObject.transform, "Bottom", new Vector2(crosshairThickness, crosshairLength), new Vector2(0f, -(crosshairGap + crosshairLength * 0.5f)));
            crosshairParts[4] = CreateCrosshairPart(canvasObject.transform, "Center", new Vector2(crosshairThickness + 1f, crosshairThickness + 1f), Vector2.zero);

            crosshairCanvas.gameObject.SetActive(!showCrosshairOnlyWhileAiming || Mode == CameraMode.Aim);
        }

        private static Image CreateCrosshairPart(Transform parent, string name, Vector2 size, Vector2 anchoredPosition)
        {
            GameObject partObject = new(name);
            partObject.transform.SetParent(parent, false);

            RectTransform rectTransform = partObject.AddComponent<RectTransform>();
            rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.sizeDelta = size;
            rectTransform.anchoredPosition = anchoredPosition;

            Image image = partObject.AddComponent<Image>();
            image.raycastTarget = false;
            image.color = Color.white;
            return image;
        }

        private void SyncLookAnglesFromTransform()
        {
            Vector3 angles = transform.eulerAngles;
            yaw = angles.y;
            pitch = Mathf.Clamp(NormalizeSignedAngle(angles.x), minPitch, maxPitch);
        }

        private static float NormalizeSignedAngle(float angle)
        {
            return angle > 180f ? angle - 360f : angle;
        }
    }
}
