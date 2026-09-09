using System;
using System.Collections.Generic;
using UnityEngine;

namespace TacticalEcho.Optimization
{
    public sealed class TickScheduler : MonoBehaviour
    {
        private readonly List<Action<float>> callbacks = new();
        [SerializeField, Min(0.01f)] private float interval = 0.1f;

        private float timer;

        private void Update()
        {
            timer += Time.deltaTime;
            if (timer < interval)
            {
                return;
            }

            float elapsed = timer;
            timer = 0f;

            for (int i = 0; i < callbacks.Count; i++)
            {
                callbacks[i]?.Invoke(elapsed);
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
