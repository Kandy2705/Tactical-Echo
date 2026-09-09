using TacticalEcho.Character.Player;
using UnityEngine;

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
        [Tooltip("Offset is relative to CameraTarget. CameraTarget already sits around the upper body/head, so Y should stay small.")]
        [SerializeField] private Vector3 exploreOffset = new(0.65f, 0.15f, -3.6f);
        [SerializeField] private Vector3 aimOffset = new(0.8f, 0.08f, -2.2f);

        [Header("Look")]
        [SerializeField, Min(0.01f)] private float mouseSensitivity = 0.08f;
        [SerializeField, Min(0.01f)] private float gamepadSensitivity = 120f;
        [SerializeField] private float minPitch = -35f;
        [SerializeField] private float maxPitch = 70f;

        [Header("Follow")]
        [SerializeField, Min(0.01f)] private float followSharpness = 18f;
        [SerializeField, Min(0.01f)] private float rotationSharpness = 24f;

        private float yaw;
        private float pitch;
        private float shoulderSign = 1f;

        public CameraMode Mode { get; private set; } = CameraMode.Explore;
        public Vector3 PlanarForward => Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
        public Vector3 PlanarRight => Vector3.ProjectOnPlane(transform.right, Vector3.up).normalized;

        private void Start()
        {
            SyncLookAnglesFromTransform();
        }

        private void LateUpdate()
        {
            if (target == null)
            {
                return;
            }

            UpdateLook();
            UpdateTransform();
        }

        public void Configure(Transform newTarget, PlayerInputReader inputReader)
        {
            target = newTarget;
            input = inputReader;
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
            Vector3 offset = Mode == CameraMode.Aim ? aimOffset : exploreOffset;
            offset.x = Mathf.Abs(offset.x) * shoulderSign;

            Vector3 desiredPosition = target.position + desiredRotation * offset;

            float positionT = 1f - Mathf.Exp(-followSharpness * Time.unscaledDeltaTime);
            float rotationT = 1f - Mathf.Exp(-rotationSharpness * Time.unscaledDeltaTime);

            transform.position = Vector3.Lerp(transform.position, desiredPosition, positionT);
            transform.rotation = Quaternion.Slerp(transform.rotation, desiredRotation, rotationT);
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
