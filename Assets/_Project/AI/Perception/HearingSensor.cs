using TacticalEcho.Core.Events;
using UnityEngine;

namespace TacticalEcho.AI.Perception
{
    public sealed class HearingSensor : MonoBehaviour, ISensor
    {
        [SerializeField, Min(0f)] private float hearingMultiplier = 1f;

        private bool hasPendingNoise;
        private NoiseEventData pendingNoise;

        private void OnEnable()
        {
            NoiseEventHub.NoiseEmitted += OnNoiseEmitted;
        }

        private void OnDisable()
        {
            NoiseEventHub.NoiseEmitted -= OnNoiseEmitted;
        }

        public void TickSensor(float deltaTime)
        {
            // Event-driven sensor. Tick exists to keep all sensors behind the same contract.
        }

        public bool TryConsumeNoise(out NoiseEventData noise)
        {
            noise = pendingNoise;
            if (!hasPendingNoise)
            {
                return false;
            }

            hasPendingNoise = false;
            return true;
        }

        private void OnNoiseEmitted(NoiseEventData noise)
        {
            float audibleRadius = noise.Radius * hearingMultiplier * Mathf.Max(0f, noise.Intensity);
            if ((noise.Position - transform.position).sqrMagnitude > audibleRadius * audibleRadius)
            {
                return;
            }

            pendingNoise = noise;
            hasPendingNoise = true;
        }
    }
}
