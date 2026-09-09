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
        [SerializeField] private Transform target;
        [SerializeField] private Vector3 exploreOffset = new(0.6f, 1.7f, -3.5f);
        [SerializeField] private Vector3 aimOffset = new(0.8f, 1.6f, -2f);
        [SerializeField, Min(0.01f)] private float followSharpness = 12f;

        private float shoulderSign = 1f;

        public CameraMode Mode { get; private set; } = CameraMode.Explore;

        private void LateUpdate()
        {
            if (target == null)
            {
                return;
            }

            Vector3 offset = Mode == CameraMode.Aim ? aimOffset : exploreOffset;
            offset.x = Mathf.Abs(offset.x) * shoulderSign;
            Vector3 desiredPosition = target.TransformPoint(offset);
            float t = 1f - Mathf.Exp(-followSharpness * Time.deltaTime);
            transform.position = Vector3.Lerp(transform.position, desiredPosition, t);
            transform.rotation = Quaternion.Slerp(transform.rotation, target.rotation, t);
        }

        public void SetMode(CameraMode mode) => Mode = mode;
        public void SwitchShoulder() => shoulderSign *= -1f;
    }
}
