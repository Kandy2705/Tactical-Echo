using System;
using System.Collections.Generic;
using UnityEngine;

namespace TacticalEcho.Optimization
{






    public sealed class TickScheduler : MonoBehaviour
    {
        private const string SharedObjectName = "TickScheduler (shared)";

        private static TickScheduler shared;

        private readonly List<Action<float>> callbacks = new();

        [Tooltip("Seconds between ticks. Every registered callback receives the real elapsed time, so accumulators stay correct when this changes.")]
        [SerializeField, Min(0.01f)] private float interval = 0.1f;

        private float timer;





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
