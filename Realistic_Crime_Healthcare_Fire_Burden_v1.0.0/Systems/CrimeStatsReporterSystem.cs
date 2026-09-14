using Colossal.Logging;
using Game;
using Game.Buildings;
using Game.Citizens;
using Game.City;
using Game.Common;
using Game.Prefabs;
using Game.Simulation;
using Game.Tools;
using RealisticCrimeSystem.Bridge;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace RealisticCrimeSystem.Systems
{
    /// <summary>
    /// Periodic city crime stats + event chirps via Custom Chirps (soft dependency).
    /// Stats cadence ~ every N Dynamic-rate runs (≈6 game-hours on Standard).
    /// Threshold alerts (tension, jail full, circuit) + overdose wave start/end are edge-triggered.
    /// 1.6.0f1 only. All posts skip silently when Custom Chirps is absent.
    /// </summary>
    public partial class CrimeStatsReporterSystem : GameSystemBase
    {
        private const string kSender = "City Crime Bureau";

        private ILog m_Log;
        private EntityQuery m_CriminalQuery;
        private EntityQuery m_CrimeProducerQuery;
        private EntityQuery m_HealthQuery;
        private EntityQuery m_PoliceConfigQuery;
        private EntityQuery m_HospitalQuery;
        private EntityQuery m_PoliceCarQuery;
        private CitySystem m_CitySystem;
        private int m_Runs;
        private int m_LastDigestAtLarge = -1;

        protected override void OnCreate()
        {
            base.OnCreate();
            m_Log = LogManager.GetLogger($"{nameof(RealisticCrimeSystem)}.{nameof(CrimeStatsReporterSystem)}");
            m_CriminalQuery = GetEntityQuery(ComponentType.ReadOnly<Criminal>());
            m_CrimeProducerQuery = GetEntityQuery(ComponentType.ReadOnly<CrimeProducer>());
            m_HealthQuery = GetEntityQuery(ComponentType.ReadOnly<HealthProblem>());
            m_PoliceConfigQuery = GetEntityQuery(ComponentType.ReadOnly<PoliceConfigurationData>());
            m_HospitalQuery = GetEntityQuery(ComponentType.ReadOnly<Game.Buildings.Hospital>());
            m_PoliceCarQuery = GetEntityQuery(new EntityQueryDesc
            {
                All = new ComponentType[] { ComponentType.ReadOnly<Game.Vehicles.PoliceCar>() },
                None = new ComponentType[]
                {
                    ComponentType.ReadOnly<Deleted>(),
                    ComponentType.ReadOnly<Destroyed>(),
                    ComponentType.ReadOnly<Temp>(),
                },
            });
            m_CitySystem = World.GetOrCreateSystemManaged<CitySystem>();
            m_Log.Info("CrimeStatsReporterSystem OnCreate - 1.6.0f1 stats + wave chirps (soft Custom Chirps)");
        }

        public override int GetUpdateInterval(SystemUpdatePhase phase)
        {
            if (phase == SystemUpdatePhase.GameSimulation) return 256;
            return base.GetUpdateInterval(phase);
        }

        protected override void OnUpdate()
        {
            var gm = Game.SceneFlow.GameManager.instance;
            if (gm == null || gm.gameMode != GameMode.Game || gm.isGameLoading) return;
            if (Mod.Settings != null && !Mod.Settings.EnableCrimeChirps) return;
            m_Runs++;

            int criminals = m_CriminalQuery.CalculateEntityCount();
            // Match healthcare overview: sick/injured citizens only (exclude Dead/Trapped/etc.)
            int sick = CountSickInjured();
            SplitCriminals(out int atLargeNow, out _, out _, out _);
            int pop = 0;
            try
            {
                var city = m_CitySystem.City;
                if (city != Entity.Null && EntityManager.HasComponent<Population>(city))
                    pop = EntityManager.GetComponentData<Population>(city).m_Population;
            }
            catch { }
            float ratio = pop > 100 ? (float)criminals / pop : 0f;

            // Overdose wave lifecycle (gentle defaults; hard dep none)
            // Thresholds calibrated to filtered sick/injured counts (excl. Dead etc.)
            bool waveOn = Mod.Settings == null || Mod.Settings.EnableOverdoseWave;
            float waveThresh = (Mod.Settings != null && Mod.Settings.SelectedPreset == Setting.DifficultyPreset.Balanced) ? 190f : 150f;
            if (DynamicCrimeState.WaveCooldownRuns > 0) DynamicCrimeState.WaveCooldownRuns--;
            if (waveOn)
            {
                if (!DynamicCrimeState.WaveActive && DynamicCrimeState.WaveCooldownRuns <= 0 && sick > waveThresh && DynamicCrimeState.PrevSick >= 0 && sick > DynamicCrimeState.PrevSick)
                {
                    DynamicCrimeState.WaveActive = true;
                    DynamicCrimeState.WaveRunsLeft = 300; // ~1 game-day of reporter runs
                    Entity bracing = FindBracingHospital(out int bracingPatients);
                    Post($"Health alert: sick wave suspected - {sick} patients in care and rising. Bracing facility: {{LINK_1}} ({bracingPatients} patients).", DepartmentAccountBridge.Healthcare, bracing);
                    m_Log.Info($"[Wave] started sick={sick} thresh={waveThresh} bracingPatients={bracingPatients}");
                }
                else if (DynamicCrimeState.WaveActive)
                {
                    DynamicCrimeState.WaveRunsLeft--;
                    if (DynamicCrimeState.WaveRunsLeft <= 0 || sick < waveThresh * 0.7f)
                    {
                        DynamicCrimeState.WaveActive = false;
                        DynamicCrimeState.WaveCooldownRuns = 200; // no back-to-back waves (sick ratchet guard)
                        // No easing chirp (spam cut): the end shows in the next stats line instead.
                        m_Log.Info($"[Wave] ended sick={sick}");
                    }
                }
            }

            // Outstanding-crimes alert: at-large pool size (edge-triggered).
            // Ratios live near 0.1%, so thresholds are in that band (the old 3%/2%
            // values could never fire - fixed).
            if (atLargeNow >= 40 && !DynamicCrimeState.OutstandingAlerted)
            {
                DynamicCrimeState.OutstandingAlerted = true;
                Entity hotspotAlert = FindHotspot(out float hotC, out float hotAvg);
                Post($"Multiple crimes awaiting police response: {atLargeNow} criminals at large. Hotspot: {{LINK_1}}.", DepartmentAccountBridge.Police, hotspotAlert);
                m_Log.Info($"[Alert] outstanding at-large={atLargeNow}");
            }
            else if (atLargeNow < 25 && DynamicCrimeState.OutstandingAlerted)
            {
                DynamicCrimeState.OutstandingAlerted = false;
            }

            // New-crime digest: jump-triggered when the at-large pool grows quickly
            if (m_LastDigestAtLarge < 0)
            {
                m_LastDigestAtLarge = atLargeNow; // baseline, no chirp on first sight
            }
            else if (atLargeNow - m_LastDigestAtLarge >= 5)
            {
                Entity digestHotspot = FindHotspot(out float dHotC, out float dHotAvg);
                Post($"Crime spree: {atLargeNow - m_LastDigestAtLarge} new criminals at large (now {atLargeNow}). Hotspot: {{LINK_1}}.", DepartmentAccountBridge.Police, digestHotspot);
                m_Log.Info($"[Digest] +{atLargeNow - m_LastDigestAtLarge} at large (now {atLargeNow})");
                m_LastDigestAtLarge = atLargeNow;
            }
            else if (atLargeNow < m_LastDigestAtLarge)
            {
                m_LastDigestAtLarge = atLargeNow; // pool drained, re-baseline down
            }

            // Tension rising (edge-triggered, resets below 0.05%)
            if (ratio >= 0.001f && criminals > DynamicCrimeState.PrevCriminals && !DynamicCrimeState.TensionAlerted && DynamicCrimeState.PrevCriminals >= 0)
            {
                DynamicCrimeState.TensionAlerted = true;
                Post($"Tension rising: {criminals} known criminals ({ratio:P1} of pop), up from {DynamicCrimeState.PrevCriminals}. Extra patrols advised.", DepartmentAccountBridge.Police, Entity.Null);
                m_Log.Info($"[Alert] tension rising ratio={ratio:P2}");
            }
            else if (ratio < 0.0005f && DynamicCrimeState.TensionAlerted)
            {
                DynamicCrimeState.TensionAlerted = false;
            }

            // Jail full (edge-triggered)
            int jailOcc = DynamicCrimeState.JailOccupancy, jailCap = DynamicCrimeState.JailCapacity;
            if (jailCap > 0 && jailOcc >= jailCap && DynamicCrimeState.PrevJailOcc >= 0 && DynamicCrimeState.PrevJailOcc < jailCap)
            {
                Post($"Jails full: {jailOcc}/{jailCap} cells occupied. Build holding capacity or a prison to keep arrests flowing.", DepartmentAccountBridge.Police, Entity.Null);
                m_Log.Info("[Alert] jail full");
            }

            // Capacity warnings (edge-triggered with reset bands - informative, no spam).
            // Police strain: at-large criminals per active patrol car.
            try
            {
                int cars = m_PoliceCarQuery.CalculateEntityCount();
                if (cars > 0)
                {
                    if (atLargeNow >= 6 * cars && !DynamicCrimeState.StrainAlerted)
                    {
                        DynamicCrimeState.StrainAlerted = true;
                        Post($"Police overstretched: {atLargeNow} criminals at large for {cars} active patrol cars. Add stations or cars.", DepartmentAccountBridge.Police, Entity.Null);
                        m_Log.Info($"[Alert] police strain atLarge={atLargeNow} cars={cars}");
                    }
                    else if (atLargeNow < 4 * cars && DynamicCrimeState.StrainAlerted)
                    {
                        DynamicCrimeState.StrainAlerted = false;
                    }
                }
            }
            catch { }
            // Jail pressure at 80% occupancy.
            if (jailCap > 0)
            {
                if (jailOcc * 5 >= jailCap * 4 && !DynamicCrimeState.JailPressureAlerted)
                {
                    DynamicCrimeState.JailPressureAlerted = true;
                    Post($"Jails filling: {jailOcc}/{jailCap} cells occupied. Plan more holding capacity.", DepartmentAccountBridge.Police, Entity.Null);
                    m_Log.Info("[Alert] jail pressure 80%");
                }
                else if (jailOcc * 5 < jailCap * 3 && DynamicCrimeState.JailPressureAlerted)
                {
                    DynamicCrimeState.JailPressureAlerted = false;
                }
            }
            // Prison transport backlog: sentenced but not yet imprisoned.
            SplitCriminals(out _, out _, out int sentencedNow, out int imprisonedNow);
            int backlog = sentencedNow - imprisonedNow;
            if (backlog >= 5 && !DynamicCrimeState.BacklogAlerted)
            {
                DynamicCrimeState.BacklogAlerted = true;
                Post($"Prison transport backlog: {backlog} sentenced awaiting prison vans. Check prison access and van capacity.", DepartmentAccountBridge.Police, Entity.Null);
                m_Log.Info($"[Alert] prison backlog={backlog}");
            }
            else if (backlog < 3 && DynamicCrimeState.BacklogAlerted)
            {
                DynamicCrimeState.BacklogAlerted = false;
            }

            // Periodic stats chirp
            int every = 100;
            if (Mod.Settings != null)
                every = Mod.Settings.ChirpCadence == Setting.Cadence.Frequent ? 50 : Mod.Settings.ChirpCadence == Setting.Cadence.Rare ? 200 : 100;
            if (m_Runs % every != 0) return;

            Entity hotspot = FindHotspot(out float hotCrime, out float avgProb);
            SplitCriminals(out int atLarge, out int jailed, out int sentenced, out int imprisoned);
            m_LastDigestAtLarge = atLarge; // stats chirp re-baselines the digest (avoids double-reporting)
            string trendC = Trend(criminals, DynamicCrimeState.PrevCriminals);
            string trendS = Trend(sick, DynamicCrimeState.PrevSick);
            string preset = Mod.Settings != null ? Mod.Settings.SelectedPreset.ToString() : "unknown";
            string outcomeLine = OutcomesLine();
            string fireLine = FireLine();
            string waveStatus = DynamicCrimeState.WaveActive ? "Sick wave ACTIVE. " : "";
            string msg = $"City crime report ({preset}): {criminals} criminals {trendC} (at large {atLarge}, jailed {jailed}, sentenced {sentenced} incl. {imprisoned} imprisoned), " +
                $"avg crime probability {avgProb:P0}, {sick} sick/injured citizens {trendS}, {waveStatus}" +
                $"jails {jailOcc}/{jailCap}, prisons {DynamicCrimeState.PrisonOccupancy}/{DynamicCrimeState.PrisonCapacity}, " +
                $"unemployment {DynamicCrimeState.UnemploymentRate:P0}, well-being {DynamicCrimeState.AvgWellBeing:F0}. {outcomeLine} {fireLine}" +
                $"Hotspot: {{LINK_1}}.";
            Post(msg, DepartmentAccountBridge.Police, hotspot);

            DynamicCrimeState.PrevCriminals = criminals;
            DynamicCrimeState.PrevSick = sick;
            DynamicCrimeState.PrevJailOcc = jailOcc;
            DynamicCrimeState.PrevPrisonOcc = DynamicCrimeState.PrisonOccupancy;
            DynamicCrimeState.PrevUnemp = DynamicCrimeState.UnemploymentRate;
            DynamicCrimeState.PrevWell = DynamicCrimeState.AvgWellBeing;
            m_Log.Info($"[Chirp] stats posted run={m_Runs} every={every}");
        }

        // Combined outcome summary: per-period deltas so one chirp carries the full
        // debug picture (more info, fewer messages). First chirp shows totals.
        private string OutcomesLine()
        {
            int a = DynamicCrimeState.OutcomeArrested, e = DynamicCrimeState.OutcomeEscaped;
            int h = DynamicCrimeState.OutcomeHospitalized, r = DynamicCrimeState.OutcomeRehabilitated;
            string line;
            if (DynamicCrimeState.PrevOutcomeArrested < 0)
                line = $"Outcomes total: {a} arrested, {e} escaped, {h} hospitalized, {r} rehabilitated.";
            else
                line = $"Outcomes since last report: +{a - DynamicCrimeState.PrevOutcomeArrested} arrested, " +
                    $"+{e - DynamicCrimeState.PrevOutcomeEscaped} escaped, +{h - DynamicCrimeState.PrevOutcomeHospitalized} hospitalized, " +
                    $"+{r - DynamicCrimeState.PrevOutcomeRehabilitated} rehabilitated.";
            DynamicCrimeState.PrevOutcomeArrested = a;
            DynamicCrimeState.PrevOutcomeEscaped = e;
            DynamicCrimeState.PrevOutcomeHospitalized = h;
            DynamicCrimeState.PrevOutcomeRehabilitated = r;
            return line;
        }

        // Fire outcomes over time (mirrors OutcomesLine): per-period deltas so the
        // stats chirp carries extinguished/burnt-down without dedicated messages.
        private string FireLine()
        {
            int active = DynamicCrimeState.FireActive;
            int s = DynamicCrimeState.FireStarted, e = DynamicCrimeState.FireExtinguished, b = DynamicCrimeState.FireBurnedDown;
            string line;
            if (DynamicCrimeState.PrevFireStarted < 0)
                line = $"Fires: {active} active ({s} started, {e} extinguished, {b} burnt down total).";
            else
                line = $"Fires: {active} active (+{s - DynamicCrimeState.PrevFireStarted} started, " +
                    $"+{e - DynamicCrimeState.PrevFireExtinguished} extinguished, +{b - DynamicCrimeState.PrevFireBurnedDown} burnt down since last report).";
            DynamicCrimeState.PrevFireStarted = s;
            DynamicCrimeState.PrevFireExtinguished = e;
            DynamicCrimeState.PrevFireBurnedDown = b;
            return line;
        }

        private Entity FindHotspot(out float crime, out float avgProb)
        {
            crime = 0f;
            avgProb = 0f;
            try
            {
                float maxAcc = 25000f;
                if (!m_PoliceConfigQuery.IsEmptyIgnoreFilter)
                    maxAcc = EntityManager.GetComponentData<PoliceConfigurationData>(m_PoliceConfigQuery.GetSingletonEntity()).m_MaxCrimeAccumulation;
                var entities = m_CrimeProducerQuery.ToEntityArray(Allocator.Temp);
                var producers = m_CrimeProducerQuery.ToComponentDataArray<CrimeProducer>(Allocator.Temp);
                Entity best = Entity.Null;
                double sum = 0;
                for (int i = 0; i < producers.Length; i++)
                {
                    sum += producers[i].m_Crime;
                    if (producers[i].m_Crime > crime) { crime = producers[i].m_Crime; best = entities[i]; }
                }
                if (producers.Length > 0 && maxAcc > 0f)
                    avgProb = (float)(sum / producers.Length / maxAcc);
                entities.Dispose(); producers.Dispose();
                return best;
            }
            catch { return Entity.Null; }
        }

        // Split mirrors the police infoview: sentenced counts everyone carrying the
        // Sentenced flag (including the imprisoned subset); jailed is Arrested-only.
        // Small residual deltas vs the live infoview are sample-timing skew (counted
        // at chirp tick vs viewed live), not a bug.
        private void SplitCriminals(out int atLarge, out int jailed, out int sentenced, out int imprisoned)
        {
            atLarge = 0; jailed = 0; sentenced = 0; imprisoned = 0;
            try
            {
                var crims = m_CriminalQuery.ToComponentDataArray<Criminal>(Allocator.Temp);
                for (int i = 0; i < crims.Length; i++)
                {
                    var f = crims[i].m_Flags;
                    bool p = (f & CriminalFlags.Prisoner) != 0;
                    bool s = (f & CriminalFlags.Sentenced) != 0;
                    bool a = (f & CriminalFlags.Arrested) != 0;
                    if (p) imprisoned++;
                    if (s) sentenced++;
                    if (a && !s && !p) jailed++;
                    if (!a && !s && !p) atLarge++;
                }
                crims.Dispose();
            }
            catch { }
        }

        // Healthcare-overview-compatible count: Sick or Injured, never Dead.
        private int CountSickInjured()
        {
            int n = 0;
            try
            {
                var problems = m_HealthQuery.ToComponentDataArray<HealthProblem>(Allocator.Temp);
                for (int i = 0; i < problems.Length; i++)
                {
                    var f = problems[i].m_Flags;
                    if ((f & HealthProblemFlags.Dead) != 0) continue;
                    if (((f & HealthProblemFlags.Sick) != 0) || ((f & HealthProblemFlags.Injured) != 0)) n++;
                }
                problems.Dispose();
            }
            catch { }
            return n;
        }

        private Entity FindBracingHospital(out int patients)
        {
            patients = 0;
            Entity best = Entity.Null;
            try
            {
                var entities = m_HospitalQuery.ToEntityArray(Allocator.Temp);
                for (int i = 0; i < entities.Length; i++)
                {
                    if (!EntityManager.HasBuffer<Patient>(entities[i])) continue;
                    int n = EntityManager.GetBuffer<Patient>(entities[i], true).Length;
                    if (n > patients) { patients = n; best = entities[i]; }
                }
                entities.Dispose();
            }
            catch { }
            return best;
        }

        private static string Trend(int cur, int prev)
        {
            if (prev < 0) return "";
            if (cur > prev) return "▲";
            if (cur < prev) return "▼";
            return "→";
        }

        private void Post(string text, DepartmentAccountBridge dept, Entity target)
        {
            if (!CustomChirpsBridge.IsAvailable)
            {
                m_Log.Info($"[Chirp-skipped] no Custom Chirps: {text}");
                return;
            }
            if (CustomChirpsBridge.PostChirp(text, dept, target, kSender))
                m_Log.Info($"[Chirp] posted ({dept}): {text}");
            else
                m_Log.Warn("[Chirp] post failed");
        }
    }
}
