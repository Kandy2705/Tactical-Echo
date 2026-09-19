using UnityEngine;

namespace TacticalEcho.AI.TacticalActions
{
    public struct TacticalContext
    {
        public Vector3 TargetPosition;
        public Vector3 CoverPosition;
        public float TargetDistance;
        public float PreferredRangeScore;
        public float TooCloseScore;
        public float TooFarScore;
        public float HealthRatio;
        public float AmmoRatio;
        public float Threat;
        public float Suppression;
        public bool HasTargetPosition;
        public bool HasLineOfSight;
        /// <summary>False once the seen target is dead. A corpse is not a threat and is not shot at.</summary>
        public bool TargetIsAlive;
        public bool CoverAvailable;
        public bool PathAvailable;
        public bool CanFire;
        public bool CanReload;
        public bool IsReloading;
    }
}
