using System;
using System.Collections.Generic;
using UnityEngine;

namespace TacticalEcho.Optimization
{
    /// <summary>
    /// Shared low-frequency tick budget. Systems that do not need per-frame resolution
    /// (AI perception, memory decay, tactical decisions) register here instead of each one
    /// carrying its own timer, so the cost of "AI thinking" is one interval for the whole
    /// scene and can be measured and tuned in one place.
    /// </summary>
    public sealed class TickScheduler : MonoBehaviour
    {
        private const string SharedObjectName = "TickScheduler (shared)";

        private static TickScheduler shared;

        private readonly List<Action<float>> callbacks = new();

        [Tooltip("Seconds between ticks. Every registered callback receives the real elapsed time, so accumulators stay correct when this changes.")]
        [SerializeField, Min(0.01f)] private float interval = 0.1f;

        private float timer;

        /// <summary>
        /// The scene's scheduler, created on first use. A scheduler is a global budget, so
        /// consumers share one rather than each resolving or creating their own.
        /// </summary>
        public static TickScheduler Shared
        {
            get
            {
                if (shared != null)
                {
                    return shared;
                }

                shared = FindFirstObjectByType<TickScheduler>();
                if (shared == null)
                {
                    shared = new GameObject(SharedObjectName).AddComponent<TickScheduler>();
                }

                return shared;
            }
        }

        public int CallbackCount => callbacks.Count;
        public float Interval => interval;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetSharedInstance()
        {
            // Statics survive a play-mode restart when domain reload is disabled.
            shared = null;
        }

        private void Awake()
        {
            shared ??= this;
        }

        private void OnDestroy()
        {
            if (shared == this)
            {
                shared = null;
            }
        }

        private void Update()
        {
            timer += Time.deltaTime;
            if (timer < interval)
            {
                return;
            }

            float elapsed = timer;
            timer = 0f;

            // Indexed and backwards-tolerant: a callback may unregister itself while ticking.
            for (int i = callbacks.Count - 1; i >= 0; i--)
            {
                if (i < callbacks.Count)
                {
                    callbacks[i]?.Invoke(elapsed);
                }
            }
        }

        public void Register(Action<float> callback)
        {
            if (callback != null && !callbacks.Contains(callback))
            {
                callbacks.Add(callback);
            }
        }

        public void Unregister(Action<float> callback)
        {
            callbacks.Remove(callback);
        }
    }
}
