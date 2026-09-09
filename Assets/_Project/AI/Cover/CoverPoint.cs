using UnityEngine;

namespace TacticalEcho.AI.Cover
{
    public sealed class CoverPoint : MonoBehaviour
    {
        [SerializeField] private Transform peekPoint;

        public Vector3 Position => transform.position;
        public Vector3 PeekPosition => peekPoint != null ? peekPoint.position : transform.position;
    }
}
