using UnityEngine;

namespace TacticalEcho.AI.Memory
{
    public sealed class EnemyMemory : MonoBehaviour
    {
        [SerializeField, Min(0.01f)] private float memoryDuration = 8f;

        public bool HasSeenPosition { get; private set; }
        public bool HasHeardPosition { get; private set; }
        public Vector3 LastSeenPosition { get; private set; }
        public Vector3 LastHeardPosition { get; private set; }
        public float LastSeenTime { get; private set; }
        public float LastHeardTime { get; private set; }
        public float Confidence { get; private set; }

        public bool HasKnownPosition => HasSeenPosition || HasHeardPosition;
        public Vector3 LastKnownPosition => LastSeenTime >= LastHeardTime ? LastSeenPosition : LastHeardPosition;

        public void RememberSeen(Vector3 position)
        {
            HasSeenPosition = true;
            LastSeenPosition = position;
            LastSeenTime = Time.time;
            Confidence = 1f;
        }

        public void RememberHeard(Vector3 position, float intensity)
        {
            HasHeardPosition = true;
            LastHeardPosition = position;
            LastHeardTime = Time.time;
            Confidence = Mathf.Max(Confidence, Mathf.Clamp01(intensity));
        }

        public void TickMemory()
        {
            float newestTime = Mathf.Max(LastSeenTime, LastHeardTime);
            float age = Time.time - newestTime;
            Confidence = Mathf.Clamp01(1f - age / memoryDuration);

            if (Confidence > 0f)
            {
                return;
            }

            HasSeenPosition = false;
            HasHeardPosition = false;
        }
    }
}
