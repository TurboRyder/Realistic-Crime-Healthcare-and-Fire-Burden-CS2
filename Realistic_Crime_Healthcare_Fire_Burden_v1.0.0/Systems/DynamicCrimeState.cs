namespace RealisticCrimeSystem.Systems
{
    /// <summary>
    /// Shared state between DynamicCrimeSystem and CrimeBreakdownUISystem for debug + warning.
    /// Strict 1.6.0f1 only.
    /// </summary>
    public static class DynamicCrimeState
    {
        public static bool CircuitHalted;
        public static int CriminalCount;
        public static int Population;
        public static float LastDelta;
        public static float LastDifficulty;
        public static float AvgWellBeing;
        public static float UnemploymentRate;
        public static float AvgLeisure;
        public static int SickCount;
        public static int JailOccupancy;
        public static int JailCapacity;
        public static int PrisonOccupancy;
        public static int PrisonCapacity;
        // Reporter previous-report snapshot for trends
        public static int PrevCriminals = -1;
        public static int PrevSick = -1;
        public static int PrevJailOcc = -1;
        public static int PrevPrisonOcc = -1;
        public static float PrevUnemp = -1f;
        public static float PrevWell = -1f;
        // Overdose wave event state
        public static bool WaveActive;
        public static int WaveRunsLeft;
        public static bool TensionAlerted;
        // Retired spawner counters (kept for save-compat of nothing - statics don't
        // serialize; free to remove at a major version bump)
        public static int SpawnedTotal;
        public static int SpawnedResolvedTotal;
        public static int SpawnedActive;
        public static bool OutstandingAlerted;
        public static int WaveCooldownRuns;
        // Outcome audit cumulative counters (flag transitions across all criminals)
        public static int OutcomeArrested;
        public static int OutcomeEscaped;
        public static int OutcomeHospitalized;
        public static int OutcomeFizzled;
        public static int OutcomeRehabilitated;
        public static int CapturesTotal;
        public static int PrevCaptures = -1;
        public static int ActiveStations;
        // Escape fallout handoff: spawner increments per escape, vandalism consumes
        public static int EscapeFalloutPending;
        // Reporter snapshot of outcomes at last stats chirp (for per-period deltas)
        public static int PrevOutcomeArrested = -1;
        public static int PrevOutcomeEscaped = -1;
        public static int PrevOutcomeHospitalized = -1;
        public static int PrevOutcomeFizzled = -1;
        public static int PrevOutcomeRehabilitated = -1;
        // Capacity-warning edge triggers (reporter sets/resets, no spam)
        public static bool StrainAlerted;
        public static bool JailPressureAlerted;
        public static bool BacklogAlerted;
        // Fire outcome audit (FireOutcomeSystem writes, reporter reads)
        public static int FireActive;
        public static int FireStarted;
        public static int FireExtinguished;
        public static int FireBurnedDown;
        public static int PrevFireStarted = -1;
        public static int PrevFireExtinguished = -1;
        public static int PrevFireBurnedDown = -1;
    }
}
