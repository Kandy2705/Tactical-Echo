using UnityEngine;

namespace TacticalEcho.AI.Cover
{
    public sealed class CoverPoint : MonoBehaviour
    {
        [SerializeField] private Transform peekPoint;

        public Vector3 Position => transform.position;
        public Vector3 PeekPosition => peekPoint != null ? peekPoint.position : transform.position;

        public void ConfigurePeekPoint(Transform newPeekPoint)
        {
            peekPoint = newPeekPoint;
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.4f, 1f, 0.5f, 0.9f);
            Gizmos.DrawWireCube(Position, new Vector3(0.6f, 1.2f, 0.6f));
            Gizmos.DrawLine(Position, PeekPosition);
        }
#endif
    }
}
