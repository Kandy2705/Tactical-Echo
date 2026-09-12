using UnityEngine;

namespace TacticalEcho.Combat.Weapons
{
    public sealed class WeaponGripPoints : MonoBehaviour
    {
        [Header("Grip Anchors")]
        [SerializeField] private Transform rightHandGrip;
        [SerializeField] private Transform leftHandGrip;
        [SerializeField] private Transform muzzle;

        public Transform RightHandGrip => rightHandGrip;
        public Transform LeftHandGrip => leftHandGrip;
        public Transform Muzzle => muzzle;

        public void Configure(Transform rightGrip, Transform leftGrip, Transform muzzleTransform)
        {
            rightHandGrip = rightGrip;
            leftHandGrip = leftGrip;
            muzzle = muzzleTransform;
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            DrawAnchor(rightHandGrip, new Color(0.2f, 0.55f, 1f), 0.025f);
            DrawAnchor(leftHandGrip, new Color(0.25f, 1f, 0.35f), 0.025f);
            DrawAnchor(muzzle, new Color(1f, 0.25f, 0.2f), 0.02f);

            if (rightHandGrip != null && leftHandGrip != null)
            {
                Gizmos.color = new Color(0.25f, 0.85f, 1f, 0.65f);
                Gizmos.DrawLine(rightHandGrip.position, leftHandGrip.position);
            }
        }

        private static void DrawAnchor(Transform anchor, Color color, float radius)
        {
            if (anchor == null)
            {
                return;
            }

            Gizmos.color = color;
            Gizmos.DrawSphere(anchor.position, radius);
            Gizmos.DrawLine(anchor.position, anchor.position + anchor.forward * 0.12f);
        }
#endif
    }
}
