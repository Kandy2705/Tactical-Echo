namespace TacticalEcho.AI.TacticalActions
{
    public struct TacticalContext
    {
        public float TargetDistance;
        public float PreferredRangeScore;
        public float HealthRatio;
        public float AmmoRatio;
        public float Threat;
        public float Suppression;
        public bool HasLineOfSight;
        public bool CoverAvailable;
        public bool PathAvailable;
    }
}
