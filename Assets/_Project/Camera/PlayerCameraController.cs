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
        [SerializeField] private Vector3 exploreOffset = new(0.6f, 1.7f, -3.5f);
        [SerializeField] private Vector3 aimOffset = new(0.8f, 1.6f, -2f);

        [Header("Look")]
        [SerializeField, Min(0.01f)] private float mouseSensitivity = 0.12f;
        [SerializeField, Min(0.01f)] private float gamepadSensitivity = 120f;
        [SerializeField] private float minPitch = -35f;
        [SerializeField] private float maxPitch = 70f;

        [Header("Follow")]
        [SerializeField, Min(0.01f)] private float followSharpness = 14f;
        [SerializeField, Min(0.01f)] private float rotationSharpness = 18f;

        private float yaw;
        private float pitch;
        private float shoulderSign = 1f;

        public CameraMode Mode { get; private set; } = CameraMode.Explore;
        public Vector3 PlanarForward => Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
        public Vector3 PlanarRight => Vector3.ProjectOnPlane(transform.right, Vector3.up).normalized;

        private void Start()
        {
            Vector3 angles = transform.eulerAngles;
            yaw = angles.y;
            pitch = NormalizeSignedAngle(angles.x);
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

            bool likelyMouseDelta = Mathf.Abs(look.x) > 1f || Mathf.Abs(look.y) > 1f;
            float scale = likelyMouseDelta
                ? mouseSensitivity
                : gamepadSensitivity * Time.deltaTime;

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

            float positionT = 1f - Mathf.Exp(-followSharpness * Time.deltaTime);
            float rotationT = 1f - Mathf.Exp(-rotationSharpness * Time.deltaTime);

            transform.position = Vector3.Lerp(transform.position, desiredPosition, positionT);
            transform.rotation = Quaternion.Slerp(transform.rotation, desiredRotation, rotationT);
        }

        private static float NormalizeSignedAngle(float angle)
        {
            return angle > 180f ? angle - 360f : angle;
        }
    }
}
