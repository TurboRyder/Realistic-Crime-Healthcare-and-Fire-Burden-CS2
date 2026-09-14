using Colossal.Logging;
using Colossal.Mathematics;
using Game;
using Game.Buildings;
using Game.Citizens;
using Game.City;
using Game.Common;
using Game.Prefabs;
using Game.Simulation;
using Game.Tools;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace RealisticCrimeSystem.Systems
{
    public partial class DynamicCrimeSystem : GameSystemBase
    {
        private ILog m_Log;
        private EntityQuery m_PoliceConfigQuery;
        private EntityQuery m_CriminalQuery;
        private EntityQuery m_CrimeProducerQuery;
        private EntityQuery m_CitizenQuery;
        private EntityQuery m_HealthQuery;
        private EntityQuery m_CrimeDataQuery;
        private EntityQuery m_FireDataQuery;
        private EntityQuery m_HappinessParamQuery;
        private CitySystem m_CitySystem;
        // Drift guard: last deltas we wrote, subtracted before re-adding each cycle
        private float m_LastCrimeDelta;
        private float m_LastPrisonDelta;
        // Sampling cache (100k-pop scaling: scan every 4th run with stride 4)
        private int m_SampleTick;
        private float m_LastWell;
        private float m_LastUnemp;
        private float m_LastLeisure;

        protected override void OnCreate()
        {
            base.OnCreate();
            m_Log = LogManager.GetLogger($"{nameof(RealisticCrimeSystem)}.{nameof(DynamicCrimeSystem)}");
            m_PoliceConfigQuery = GetEntityQuery(ComponentType.ReadOnly<PoliceConfigurationData>());
            var exclude = new EntityQueryDesc
            {
                None = new ComponentType[]
                {
                    ComponentType.ReadOnly<Deleted>(),
                    ComponentType.ReadOnly<Destroyed>(),
                    ComponentType.ReadOnly<Temp>(),
                }
            };
            m_CriminalQuery = GetEntityQuery(new EntityQueryDesc
            {
                All = new ComponentType[] { ComponentType.ReadOnly<Criminal>() },
                None = exclude.None,
            });
            m_CrimeProducerQuery = GetEntityQuery(new EntityQueryDesc
            {
                All = new ComponentType[] { ComponentType.ReadOnly<CrimeProducer>() },
                None = exclude.None,
            });
            m_CitizenQuery = GetEntityQuery(new EntityQueryDesc
            {
                All = new ComponentType[] { ComponentType.ReadOnly<Citizen>() },
                None = exclude.None,
            });
            m_HealthQuery = GetEntityQuery(new EntityQueryDesc
            {
                All = new ComponentType[] { ComponentType.ReadOnly<HealthProblem>() },
                None = exclude.None,
            });
            m_CrimeDataQuery = GetEntityQuery(ComponentType.ReadOnly<CrimeData>());
            m_FireDataQuery = GetEntityQuery(ComponentType.ReadOnly<FireData>());
            m_HappinessParamQuery = GetEntityQuery(ComponentType.ReadOnly<CitizenHappinessParameterData>());
            m_CitySystem = World.GetOrCreateSystemManaged<CitySystem>();
            RequireForUpdate(m_PoliceConfigQuery);
            m_Log.Info("DynamicCrimeSystem OnCreate - 1.6.0f1 observer + tolerance + CityModifier writer");
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
            var cfgEntity = m_PoliceConfigQuery.GetSingletonEntity();
            var cfg = EntityManager.GetComponentData<PoliceConfigurationData>(cfgEntity);
            int criminals = m_CriminalQuery.CalculateEntityCount();
            int producers = m_CrimeProducerQuery.CalculateEntityCount();

            // True citizen sampling, workforce-filtered. SCALING: full scan every 4th run
            // (1024 ticks) with 1-in-4 stride (~16x cheaper, statistically identical aggregates).
            // Cheap writes (tolerance/modifier) run every 256 ticks regardless.
            // Workforce = Adult, no Student, not Tourist. Unemployed = workforce without Worker.
            m_SampleTick++;
            float avgWell = m_LastWell, unempRate = m_LastUnemp, avgLeisure = m_LastLeisure;
            if (m_SampleTick % 4 == 0)
            {
                try
                {
                    var entities = m_CitizenQuery.ToEntityArray(Allocator.Temp);
                    var citizens = m_CitizenQuery.ToComponentDataArray<Citizen>(Allocator.Temp);
                    if (citizens.Length > 0)
                    {
                        long wellSum = 0, leisureSum = 0;
                        int sampled = 0, workforce = 0, unemp = 0;
                        for (int i = 0; i < citizens.Length; i += 4) // stride 4
                        {
                            var c = citizens[i];
                            wellSum += c.m_WellBeing;
                            leisureSum += c.m_LeisureCounter;
                            sampled++;
                            if (c.GetAge() != CitizenAge.Adult) continue;
                            if ((c.m_State & CitizenFlags.Tourist) != 0) continue;
                            if (EntityManager.HasComponent<Game.Citizens.Student>(entities[i])) continue;
                            workforce++;
                            if (!EntityManager.HasComponent<Worker>(entities[i])) unemp++;
                        }
                        if (sampled > 0)
                        {
                            avgWell = (float)wellSum / sampled;
                            avgLeisure = (float)leisureSum / sampled;
                        }
                        unempRate = workforce > 0 ? (float)unemp / workforce : 0f;
                        m_LastWell = avgWell; m_LastUnemp = unempRate; m_LastLeisure = avgLeisure;
                    }
                    entities.Dispose();
                    citizens.Dispose();
                }
                catch { }
            }

            int sick = 0;
            try { sick = m_HealthQuery.CalculateEntityCount(); } catch { }

            int pop = 0;
            int modBufLen = -1;
            try
            {
                var city = m_CitySystem.City;
                if (city != Entity.Null)
                {
                    if (EntityManager.HasComponent<Population>(city))
                        pop = EntityManager.GetComponentData<Population>(city).m_Population;
                    if (EntityManager.HasBuffer<CityModifier>(city))
                        modBufLen = EntityManager.GetBuffer<CityModifier>(city, true).Length;
                }
            }
            catch { }

            float ratio = pop > 100 ? (float)criminals / pop : 0f;
            var preset = Mod.Settings != null ? Mod.Settings.SelectedPreset.ToString() : "unknown";

            float targetTol = 1000f;
            float targetCrime = 0f;
            float targetPrison = 0f;
            float wantWelfare = 0.4f;
            float breaker = (Mod.Settings != null ? Mod.Settings.BreakerPct : 10f) / 100f;
            bool halted = ratio > breaker;
            if (!halted && Mod.Settings != null)
            {
                targetTol = Mod.Settings.GetToleranceForPreset();
                // Difficulty breathes with fundamentals: presetBase + k*(unemployment - 5%).
                Mod.Settings.GetCrimeModTerms(out float baseMod, out float k, out float clampMax);
                targetCrime = Unity.Mathematics.math.clamp(baseMod + k * (unempRate - 0.05f), 0f, clampMax);
                targetPrison = Mod.Settings.GetPrisonTimeModifier();
                wantWelfare = Mod.Settings.GetWelfareRecurrenceFactor();
            }

            if (System.Math.Abs(cfg.m_CrimeAccumulationTolerance - targetTol) > 0.1f ||
                System.Math.Abs(cfg.m_WelfareCrimeRecurrenceFactor - wantWelfare) > 0.0001f)
            {
                cfg.m_CrimeAccumulationTolerance = targetTol;
                cfg.m_WelfareCrimeRecurrenceFactor = wantWelfare;
                EntityManager.SetComponentData(cfgEntity, cfg);
                m_Log.Info($"[Write] tolerance -> {targetTol} welfareRecurrence -> {wantWelfare:F2} (halted={halted})");
            }

            // Happiness: Unhappiness slider is severity % of the vanilla slope (100 =
            // vanilla). Negligible floor stays preset-bundled. Halted reverts to vanilla.
            // Singleton rewrite from observed 1.6.0f1 baselines, self-healing like tolerance.
            try
            {
                if (!m_HappinessParamQuery.IsEmptyIgnoreFilter)
                {
                    Entity hEntity = m_HappinessParamQuery.GetSingletonEntity();
                    var hp = EntityManager.GetComponentData<CitizenHappinessParameterData>(hEntity);
                    float wantMult = 0.0004f;
                    int wantNegl = 5000;
                    int wantCap = 30;
                    if (!halted && Mod.Settings != null)
                    {
                        wantMult = 0.0004f * Mod.Settings.UnhappinessMult / 100f;
                        wantNegl = Mod.Settings.GetHappinessNegligible();
                    }
                    if (System.Math.Abs(hp.m_CrimeMultiplier - wantMult) > 0.000001f ||
                        hp.m_NegligibleCrime != wantNegl ||
                        hp.m_MaxCrimePenalty != wantCap)
                    {
                        hp.m_CrimeMultiplier = wantMult;
                        hp.m_NegligibleCrime = wantNegl;
                        hp.m_MaxCrimePenalty = wantCap;
                        EntityManager.SetComponentData(hEntity, hp);
                        m_Log.Info($"[Write] happiness crime params -> mult={wantMult:F6} negligible={wantNegl} cap={wantCap}");
                    }
                }
            }
            catch (System.Exception ex)
            {
                m_Log.Warn($"[Write] happiness params failed (safe): {ex.Message}");
            }

            // Vanilla occurrence scaling: absolute values forced from known 1.6.0f1
            // baselines each run (self-healing, no drift possible). Native popups, stats,
            // lifecycle - the parallel spawner is retired in favor of this.
            // Sliders hold ABSOLUTE values (preset snaps them on select, Tuning tab shows
            // what is live). Halted (breaker) reverts to vanilla 1.0 to drain.
            // Catch window: vanilla alarm (2,6) divided by Response, vanilla duration
            // (40,80) multiplied - attending patrols convert thwarts into arrests.
            // Baselines from observed 1.6.0f1, self-healing.
            try
            {
                float occMult = 1.0f;
                float wantAmin = 2.0f, wantAmax = 6.0f, wantDmin = 40.0f, wantDmax = 80.0f;
                if (!halted && Mod.Settings != null)
                {
                    occMult = Mod.Settings.VolumeMult;
                    float resp = Mod.Settings.ResponseMult;
                    wantAmin = 2.0f / resp; wantAmax = 6.0f / resp;
                    wantDmin = 40.0f * resp; wantDmax = 80.0f * resp;
                }
                var crimePrefabs = m_CrimeDataQuery.ToEntityArray(Allocator.Temp);
                for (int ci = 0; ci < crimePrefabs.Length; ci++)
                {
                    var cd = EntityManager.GetComponentData<CrimeData>(crimePrefabs[ci]);
                    float omin = cd.m_OccurenceProbability.min, omax = cd.m_OccurenceProbability.max;
                    // Sanity: only touch vanilla-shaped values (guards future game updates)
                    if (omin >= 0.2f && omin <= 8f && omax >= 1f && omax <= 40f)
                    {
                        float nmin = 1.0f * occMult, nmax = 5.0f * occMult;
                        float rmin = 1.0f * occMult, rmax = 7.0f * occMult;
                        if (System.Math.Abs(cd.m_OccurenceProbability.min - nmin) > 0.001f ||
                            System.Math.Abs(cd.m_OccurenceProbability.max - nmax) > 0.001f)
                        {
                            cd.m_OccurenceProbability = new Bounds1(nmin, nmax);
                            cd.m_RecurrenceProbability = new Bounds1(rmin, rmax);
                            EntityManager.SetComponentData(crimePrefabs[ci], cd);
                            m_Log.Info($"[Write] occurrence -> ({nmin:F2},{nmax:F2}) rec -> ({rmin:F2},{rmax:F2}) mult={occMult:F2}");
                        }
                    }
                    else
                    {
                        m_Log.Warn($"[Write] occurrence shape unexpected ({omin:F2},{omax:F2}) - skipped (future vanilla?)");
                    }
                    // Catch window: band-checked like occurrence (bands cover slider extremes:
                    // response 3.0 -> alarm (0.67,2.0), duration (120,240)).
                    float amin = cd.m_AlarmDelay.min, amax = cd.m_AlarmDelay.max;
                    float dmin = cd.m_CrimeDuration.min, dmax = cd.m_CrimeDuration.max;
                    if (amin >= 0.5f && amin <= 8f && amax >= 1.5f && amax <= 24f &&
                        dmin >= 10f && dmin <= 320f && dmax >= 20f && dmax <= 640f)
                    {
                        if (System.Math.Abs(amin - wantAmin) > 0.001f ||
                            System.Math.Abs(amax - wantAmax) > 0.001f ||
                            System.Math.Abs(dmin - wantDmin) > 0.01f ||
                            System.Math.Abs(dmax - wantDmax) > 0.01f)
                        {
                            cd.m_AlarmDelay = new Bounds1(wantAmin, wantAmax);
                            cd.m_CrimeDuration = new Bounds1(wantDmin, wantDmax);
                            EntityManager.SetComponentData(crimePrefabs[ci], cd);
                            m_Log.Info($"[Write] catch window -> alarm=({wantAmin:F1},{wantAmax:F1}) dur=({wantDmin:F0},{wantDmax:F0})");
                        }
                    }
                    else
                    {
                        m_Log.Warn($"[Write] catch window shape unexpected alarm=({amin:F1},{amax:F1}) dur=({dmin:F0},{dmax:F0}) - skipped");
                    }
                }
                crimePrefabs.Dispose();
            }
            catch (System.Exception ex)
            {
                m_Log.Warn($"[Write] occurrence failed (safe): {ex.Message}");
            }

            // Sentencing pressure: longer stays fill cells (preset mults).
            // Absolute values from observed 1.6.0f1 baselines (jail 0.1/1.0, prison 5/100),
            // self-healing. prisonP untouched. Units unknown - relative scaling is safe.
            try
            {
                float sentMult = 1.0f, prisMult = 1.0f;
                if (!halted && Mod.Settings != null)
                    Mod.Settings.GetSentenceMults(out sentMult, out prisMult);
                var crimePrefabs2 = m_CrimeDataQuery.ToEntityArray(Allocator.Temp);
                for (int ci = 0; ci < crimePrefabs2.Length; ci++)
                {
                    var cd = EntityManager.GetComponentData<CrimeData>(crimePrefabs2[ci]);
                    if (cd.m_JailTimeRange.min < 0.02f || cd.m_JailTimeRange.min > 1f) continue;
                    if (cd.m_JailTimeRange.max < 0.2f || cd.m_JailTimeRange.max > 5f) continue;
                    if (cd.m_PrisonTimeRange.min < 1f || cd.m_PrisonTimeRange.min > 50f) continue;
                    if (cd.m_PrisonTimeRange.max < 20f || cd.m_PrisonTimeRange.max > 500f) continue;
                    float njmin = 0.1f * sentMult, njmax = 1.0f * sentMult;
                    float npmin = 5.0f * prisMult, npmax = 100.0f * prisMult;
                    if (System.Math.Abs(cd.m_JailTimeRange.min - njmin) > 0.001f ||
                        System.Math.Abs(cd.m_PrisonTimeRange.min - npmin) > 0.01f)
                    {
                        cd.m_JailTimeRange = new Bounds1(njmin, njmax);
                        cd.m_PrisonTimeRange = new Bounds1(npmin, npmax);
                        EntityManager.SetComponentData(crimePrefabs2[ci], cd);
                        m_Log.Info($"[Write] sentencing -> jail=({njmin:F2},{njmax:F2}) prison=({npmin:F1},{npmax:F1})");
                    }
                }
                crimePrefabs2.Dispose();
            }
            catch (System.Exception ex)
            {
                m_Log.Warn($"[Write] sentencing failed (safe): {ex.Message}");
            }

            // Fire pressure: scale the Building-target fire prefab only (absolute values
            // from observed 1.6.0f1 baselines, self-healing like occurrence). Forest and
            // None-targeted prefabs untouched. Both start-probability (5x) and spread (3x).
            // Sanity band covers our own writes so retunes never stall silently.
            try
            {
                float fireFreqMult = 1.0f, fireSpreadMult = 1.0f;
                if (!halted && Mod.Settings != null)
                {
                    fireFreqMult = Mod.Settings.FireMult;
                    fireSpreadMult = Mod.Settings.GetFireSpread();
                }
                var firePrefabs = m_FireDataQuery.ToEntityArray(Allocator.Temp);
                for (int fi = 0; fi < firePrefabs.Length; fi++)
                {
                    var fd = EntityManager.GetComponentData<FireData>(firePrefabs[fi]);
                    if (fd.m_RandomTargetType.ToString() != "Building") continue;
                    if (fd.m_StartProbability < 0.05f || fd.m_StartProbability > 5.0f) continue;
                    float nstart = 0.1f * fireFreqMult, nspread = 0.3f * fireSpreadMult;
                    if (System.Math.Abs(fd.m_StartProbability - nstart) > 0.0001f ||
                        System.Math.Abs(fd.m_SpreadProbability - nspread) > 0.0001f)
                    {
                        fd.m_StartProbability = nstart;
                        fd.m_SpreadProbability = nspread;
                        EntityManager.SetComponentData(firePrefabs[fi], fd);
                        m_Log.Info($"[Write] fire Building prefab -> startP={nstart:F4} spreadP={nspread:F4} mult={fireFreqMult:F2}/{fireSpreadMult:F2}");
                    }
                }
                firePrefabs.Dispose();
            }
            catch (System.Exception ex)
            {
                m_Log.Warn($"[Write] fire failed (safe): {ex.Message}");
            }

            // CityModifier writer with drift guard (buffer rebuilt by vanilla each cycle).
            // x = absolute channel (untouched), y = relative channel (ours added on top of baseline).
            float crimeModY = 0f, prisonModY = 0f;
            try
            {
                var city = m_CitySystem.City;
                if (city != Entity.Null && EntityManager.HasBuffer<CityModifier>(city))
                {
                    var buf = EntityManager.GetBuffer<CityModifier>(city);
                    crimeModY = WriteModifier(buf, CityModifierType.CrimeAccumulation, targetCrime, ref m_LastCrimeDelta, ref m_LastCrimeBaseline);
                    prisonModY = WriteModifier(buf, CityModifierType.PrisonTime, targetPrison, ref m_LastPrisonDelta, ref m_LastPrisonBaseline);
                }
            }
            catch (System.Exception ex)
            {
                m_Log.Warn($"[Write] CityModifier failed (safe): {ex.Message}");
            }

            m_Log.Info($"[Observe] preset={preset} tol={cfg.m_CrimeAccumulationTolerance} coverF={cfg.m_CrimePoliceCoverageFactor} maxAcc={cfg.m_MaxCrimeAccumulation} criminals={criminals} pop={pop} ratio={ratio:P2} producers={producers} crimeMod={crimeModY:F3}(tgt={targetCrime:F3}) prisonMod={prisonModY:F3} modBufLen={modBufLen} well={avgWell:F0} unemp={unempRate:P1} leisure={avgLeisure:F0} sick={sick}");

            DynamicCrimeState.CriminalCount = criminals;
            DynamicCrimeState.Population = pop;
            bool wasHalted = DynamicCrimeState.CircuitHalted;
            DynamicCrimeState.CircuitHalted = halted;
            DynamicCrimeState.LastDelta = targetTol;
            DynamicCrimeState.AvgWellBeing = avgWell;
            DynamicCrimeState.UnemploymentRate = unempRate;
            DynamicCrimeState.AvgLeisure = avgLeisure;
            DynamicCrimeState.SickCount = sick;
            if (halted)
            {
                m_Log.Warn($"[CIRCUIT] criminals {criminals}/{pop} ({ratio:P1}) >{breaker:P0} - modifiers reverted to 0 to drain");
                if (!wasHalted && Bridge.CustomChirpsBridge.IsAvailable)
                    Bridge.CustomChirpsBridge.PostChirp($"Circuit breaker tripped: {criminals} criminals ({ratio:P0} of pop). Difficulty paused to let jails drain.", Bridge.DepartmentAccountBridge.Police, Entity.Null, "City Crime Bureau");
            }
        }

        private float m_LastCrimeBaseline;
        private float m_LastPrisonBaseline;

        private float WriteModifier(DynamicBuffer<CityModifier> buf, CityModifierType type, float target, ref float lastWritten, ref float lastBaseline)
        {
            int idx = (int)type;
            // Pad short buffers (policy-free cities) instead of silently skipping
            while (buf.Length <= idx)
                buf.Add(new CityModifier { m_Delta = new float2(0f, 0f) });
            var cur = buf[idx].m_Delta;
            float baselineY;
            if (System.Math.Abs(cur.y - (lastBaseline + lastWritten)) < 0.001f)
            {
                // Our previous write persisted: adjust from stored baseline (no stacking)
                baselineY = lastBaseline;
            }
            else
            {
                // Vanilla rebuilt (or external change): re-baseline from current value
                baselineY = cur.y;
            }
            float newY = baselineY + target;
            buf[idx] = new CityModifier { m_Delta = new float2(cur.x, newY) };
            lastBaseline = baselineY;
            lastWritten = target;
            return newY;
        }
    }
}
