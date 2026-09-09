using System;
using UnityEngine;

namespace TacticalEcho.Core.Events
{
    public readonly struct NoiseEventData
    {
        public NoiseEventData(Vector3 position, float radius, float intensity, GameObject source)
        {
            Position = position;
            Radius = radius;
            Intensity = intensity;
            Source = source;
        }

        public Vector3 Position { get; }
        public float Radius { get; }
        public float Intensity { get; }
        public GameObject Source { get; }
    }

    public static class NoiseEventHub
    {
        public static event Action<NoiseEventData> NoiseEmitted;

        public static void Emit(in NoiseEventData noise)
        {
            NoiseEmitted?.Invoke(noise);
        }
    }
}
