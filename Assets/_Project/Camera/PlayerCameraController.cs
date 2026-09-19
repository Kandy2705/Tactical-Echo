using TacticalEcho.Character.Player;
using TacticalEcho.CameraSystem.Obstruction;
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
        [Tooltip("Optional. Owns obstruction fade and the camera-collision push. Resolved from this GameObject when empty.")]
        [SerializeField] private CameraObstructionHandler obstruction;
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

        [Header("Recoil")]
        [SerializeField, Min(0f)] private float recoilPitchDegrees = 0.75f;
        [SerializeField, Min(0f)] private float recoilYawDegrees = 0.28f;
        [SerializeField, Min(0.01f)] private float recoilReturnSharpness = 9f;
        [SerializeField, Min(0.01f)] private float recoilKickSharpness = 32f;
        [SerializeField, Min(0.1f)] private float recoilVisibilityMultiplier = 1.65f;

        [Header("Crosshair")]
        [SerializeField, Min(1f)] private float crosshairLength = 8f;
        [SerializeField, Min(0f)] private float exploreCrosshairGap = 9f;
        [SerializeField, Min(0f)] private float aimCrosshairGap = 4f;
        [SerializeField, Min(1f)] private float crosshairThickness = 2f;
        [SerializeField, Min(0f)] private float crosshairKickPerShot = 7f;
        [SerializeField, Min(0.01f)] private float crosshairKickRecovery = 24f;
        [SerializeField, Min(0f)] private float maxCrosshairKick = 20f;
        [SerializeField, Min(0.1f)] private float crosshairMaxDistance = 150f;
        [SerializeField] private LayerMask crosshairMask = ~0;
        [SerializeField] private Color crosshairColor = Color.white;
        [SerializeField] private Color targetCrosshairColor = new(1f, 0.25f, 0.2f, 1f);

        [Header("Hit Marker")]
        [SerializeField, Min(0.03f)] private float hitMarkerDuration = 0.12f;
        [SerializeField, Min(2f)] private float hitMarkerLength = 10f;
        [SerializeField, Min(0f)] private float hitMarkerGap = 6f;
        [SerializeField, Min(1f)] private float hitMarkerThickness = 2f;
        [SerializeField] private Color hitMarkerColor = Color.white;

        private float yaw;
        private float pitch;
        private float shoulderSign = 1f;
        private Vector3 currentOffset;
        private Camera gameplayCamera;

        private Vector2 recoilTarget;
        private Vector2 recoilCurrent;
        private float crosshairKick;

        private Canvas crosshairCanvas;
        private Image[] crosshairParts;
        private Image[] hitMarkerParts;
        private float hitMarkerEndTime;

        public CameraMode Mode { get; private set; } = CameraMode.Explore;
        public Vector3 PlanarForward => Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
        public Vector3 PlanarRight => Vector3.ProjectOnPlane(transform.right, Vector3.up).normalized;

        private void Awake()
        {
            gameplayCamera = GetComponent<Camera>();
            currentOffset = exploreOffset;

            if (obstruction == null)
            {
                obstruction = GetComponent<CameraObstructionHandler>();
            }

            // The handler needs to know what the camera is looking at for both its fade scan
            // and its collision cast; the camera controller is the one that knows.
            obstruction?.Configure(target);

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
                UpdateHitMarker();
                return;
            }

            UpdateLook();
            UpdateRecoil();
            UpdateTransform();
            UpdateCrosshair();
            UpdateHitMarker();
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

            // Keep the obstruction handler pointed at whatever the camera now follows.
            obstruction?.Configure(target);
        }

        public void SetMode(CameraMode mode)
        {
            Mode = mode;
        }

        public void AddRecoil(float strength = 1f)
        {
            if (strength <= 0f)
            {
                return;
            }

            float visibleStrength = Mathf.Max(0.5f, strength) * recoilVisibilityMultiplier;
            recoilTarget.y += recoilPitchDegrees * visibleStrength;
            recoilTarget.x += UnityEngine.Random.Range(-recoilYawDegrees, recoilYawDegrees) * visibleStrength;
            crosshairKick = Mathf.Min(maxCrosshairKick, crosshairKick + crosshairKickPerShot * visibleStrength);
        }

        public void ShowHitMarker()
        {
            if (hitMarkerParts == null)
            {
                return;
            }

            hitMarkerEndTime = Time.unscaledTime + hitMarkerDuration;
            foreach (Image part in hitMarkerParts)
            {
                if (part == null)
                {
                    continue;
                }

                part.color = hitMarkerColor;
                part.gameObject.SetActive(true);
            }
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

        private void UpdateRecoil()
        {
            float kickT = 1f - Mathf.Exp(-recoilKickSharpness * Time.unscaledDeltaTime);
            recoilCurrent = Vector2.Lerp(recoilCurrent, recoilTarget, kickT);

            float returnT = 1f - Mathf.Exp(-recoilReturnSharpness * Time.unscaledDeltaTime);
            recoilTarget = Vector2.Lerp(recoilTarget, Vector2.zero, returnT);
            crosshairKick = Mathf.MoveTowards(crosshairKick, 0f, crosshairKickRecovery * Time.unscaledDeltaTime);
        }

        private void UpdateTransform()
        {
            Quaternion desiredRotation = Quaternion.Euler(
                Mathf.Clamp(pitch - recoilCurrent.y, minPitch, maxPitch),
                yaw + recoilCurrent.x,
                0f);

            Vector3 targetOffset = Mode == CameraMode.Aim ? aimOffset : exploreOffset;
            targetOffset.x = Mathf.Abs(targetOffset.x) * shoulderSign;

            float aimBlend = 1f - Mathf.Exp(-aimBlendSharpness * Time.unscaledDeltaTime);
            currentOffset = Vector3.Lerp(currentOffset, targetOffset, aimBlend);

            Vector3 desiredPosition = target.position + desiredRotation * currentOffset;

            // Obstruction ownership lives in CameraObstructionHandler; positioning lives here.
            // Resolving before the smoothing lerp means the camera slides along a wall instead
            // of snapping once it is already inside it.
            if (obstruction != null)
            {
                desiredPosition = obstruction.ResolveCameraPosition(target, desiredPosition);
            }

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

            // Always visible: hip-fire still needs a clear center point.
            if (!crosshairCanvas.gameObject.activeSelf)
            {
                crosshairCanvas.gameObject.SetActive(true);
            }

            UpdateCrosshairLayout();

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

        private void UpdateCrosshairLayout()
        {
            if (crosshairParts == null || crosshairParts.Length < 5)
            {
                return;
            }

            float baseGap = Mode == CameraMode.Aim ? aimCrosshairGap : exploreCrosshairGap;
            float recoilGap = recoilCurrent.magnitude * 2f;
            float gap = baseGap + crosshairKick + recoilGap;
            float halfLength = crosshairLength * 0.5f;
            float offset = gap + halfLength;

            SetCrosshairPosition(crosshairParts[0], new Vector2(-offset, 0f));
            SetCrosshairPosition(crosshairParts[1], new Vector2(offset, 0f));
            SetCrosshairPosition(crosshairParts[2], new Vector2(0f, offset));
            SetCrosshairPosition(crosshairParts[3], new Vector2(0f, -offset));
            SetCrosshairPosition(crosshairParts[4], Vector2.zero);
        }

        private static void SetCrosshairPosition(Image image, Vector2 position)
        {
            if (image != null)
            {
                image.rectTransform.anchoredPosition = position;
            }
        }

        private void UpdateHitMarker()
        {
            if (hitMarkerParts == null || Time.unscaledTime < hitMarkerEndTime)
            {
                return;
            }

            foreach (Image part in hitMarkerParts)
            {
                if (part != null && part.gameObject.activeSelf)
                {
                    part.gameObject.SetActive(false);
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
            crosshairParts[0] = CreateCrosshairPart(canvasObject.transform, "Left", new Vector2(crosshairLength, crosshairThickness), Vector2.zero);
            crosshairParts[1] = CreateCrosshairPart(canvasObject.transform, "Right", new Vector2(crosshairLength, crosshairThickness), Vector2.zero);
            crosshairParts[2] = CreateCrosshairPart(canvasObject.transform, "Top", new Vector2(crosshairThickness, crosshairLength), Vector2.zero);
            crosshairParts[3] = CreateCrosshairPart(canvasObject.transform, "Bottom", new Vector2(crosshairThickness, crosshairLength), Vector2.zero);
            crosshairParts[4] = CreateCrosshairPart(canvasObject.transform, "Center", new Vector2(crosshairThickness + 1f, crosshairThickness + 1f), Vector2.zero);

            CreateHitMarker(canvasObject.transform);
            UpdateCrosshairLayout();
            crosshairCanvas.gameObject.SetActive(true);
        }

        private void CreateHitMarker(Transform parent)
        {
            float offset = hitMarkerGap + hitMarkerLength * 0.5f;
            hitMarkerParts = new Image[4];
            hitMarkerParts[0] = CreateHitMarkerPart(parent, "HitTopLeft", new Vector2(-offset, offset), -45f);
            hitMarkerParts[1] = CreateHitMarkerPart(parent, "HitTopRight", new Vector2(offset, offset), 45f);
            hitMarkerParts[2] = CreateHitMarkerPart(parent, "HitBottomLeft", new Vector2(-offset, -offset), 45f);
            hitMarkerParts[3] = CreateHitMarkerPart(parent, "HitBottomRight", new Vector2(offset, -offset), -45f);

            foreach (Image part in hitMarkerParts)
            {
                part.gameObject.SetActive(false);
            }
        }

        private Image CreateHitMarkerPart(Transform parent, string name, Vector2 anchoredPosition, float rotationZ)
        {
            Image image = CreateCrosshairPart(
                parent,
                name,
                new Vector2(hitMarkerLength, hitMarkerThickness),
                anchoredPosition);

            image.rectTransform.localRotation = Quaternion.Euler(0f, 0f, rotationZ);
            image.color = hitMarkerColor;
            return image;
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
