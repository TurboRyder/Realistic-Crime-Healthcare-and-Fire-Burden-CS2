using Colossal.Logging;
using Game;
using Game.Citizens;
using Game.Common;
using Game.Tools;
using Unity.Collections;
using Unity.Entities;

namespace RealisticCrimeSystem.Systems
{
    /// <summary>
    /// Response side of the mod (v0.6+): observes flag transitions across ALL criminals
    /// (vanilla or otherwise) and classifies outcomes. Creates nothing - captures, raids,
    /// arson and crash entities were removed after playtest proved vanilla response +
    /// difficulty globals carry the loop (flag injections never converted, dispatches
    /// clogged real response, ignite/accident entities were ignored by vanilla).
    /// 1.6.0f1 only. All load-guarded.
    /// </summary>
    public partial class CrimeResponseSystem : GameSystemBase
    {
        private ILog m_Log;
        private EntityQuery m_CriminalQuery;
        private readonly System.Collections.Generic.Dictionary<Entity, CriminalFlags> m_LastFlags = new System.Collections.Generic.Dictionary<Entity, CriminalFlags>();
        private readonly System.Collections.Generic.HashSet<Entity> m_LastSick = new System.Collections.Generic.HashSet<Entity>();
        private uint m_Tick;

        protected override void OnCreate()
        {
            base.OnCreate();
            m_Log = LogManager.GetLogger($"{nameof(RealisticCrimeSystem)}.{nameof(CrimeResponseSystem)}");
            m_CriminalQuery = GetEntityQuery(new EntityQueryDesc
            {
                All = new ComponentType[] { ComponentType.ReadOnly<Criminal>() },
                None = new ComponentType[]
                {
                    ComponentType.ReadOnly<Deleted>(),
                    ComponentType.ReadOnly<Destroyed>(),
                    ComponentType.ReadOnly<Temp>(),
                },
            });
            m_Log.Info("CrimeResponseSystem OnCreate - 1.6.0f1 outcome observation (no entity creation)");
        }

        public override int GetUpdateInterval(SystemUpdatePhase phase)
        {
            if (phase == SystemUpdatePhase.GameSimulation) return 1024;
            return base.GetUpdateInterval(phase);
        }

        protected override void OnUpdate()
        {
            var gm = Game.SceneFlow.GameManager.instance;
            if (gm == null || gm.gameMode != GameMode.Game || gm.isGameLoading) return;
            m_Tick++;
            AuditTransitions();
            PruneMemory();
        }

        private void AuditTransitions()
        {
            try
            {
                var entities = m_CriminalQuery.ToEntityArray(Allocator.Temp);
                var crims = m_CriminalQuery.ToComponentDataArray<Criminal>(Allocator.Temp);
                var seen = new System.Collections.Generic.HashSet<Entity>();
                for (int i = 0; i < entities.Length; i++)
                {
                    Entity e = entities[i];
                    CriminalFlags cur = crims[i].m_Flags;
                    seen.Add(e);
                    if (!m_LastFlags.TryGetValue(e, out CriminalFlags prev))
                    {
                        m_LastFlags[e] = cur; // baseline, no counting on first sight
                        continue;
                    }
                    if (prev.Equals(cur))
                    {
                        // Still check hospitalization (independent component)
                        if (EntityManager.HasComponent<HealthProblem>(e))
                        {
                            var hp = EntityManager.GetComponentData<HealthProblem>(e).m_Flags;
                            if ((hp & HealthProblemFlags.Sick) != 0 && !m_LastSick.Contains(e))
                            {
                                m_LastSick.Add(e);
                                DynamicCrimeState.OutcomeHospitalized++;
                                m_Log.Info($"[Outcome] citizen {e.Index} hospitalized");
                            }
                        }
                        continue;
                    }
                    bool hadPipe = (prev & (CriminalFlags.Arrested | CriminalFlags.Sentenced | CriminalFlags.Prisoner)) != 0;
                    bool hasPipe = (cur & (CriminalFlags.Arrested | CriminalFlags.Sentenced | CriminalFlags.Prisoner)) != 0;
                    if (hasPipe && !hadPipe)
                    {
                        DynamicCrimeState.OutcomeArrested++;
                        m_Log.Info($"[Outcome] citizen {e.Index} arrested/sentenced");
                    }
                    m_LastFlags[e] = cur;
                }
                // Vanished entities: had flags -> escaped; served time (had pipeline) -> rehabilitated
                var gone = new System.Collections.Generic.List<Entity>();
                foreach (var kv in m_LastFlags)
                {
                    if (seen.Contains(kv.Key) || EntityManager.Exists(kv.Key)) continue;
                    gone.Add(kv.Key);
                }
                foreach (var g in gone)
                {
                    CriminalFlags prev = m_LastFlags[g];
                    m_LastFlags.Remove(g);
                    m_LastSick.Remove(g);
                    if ((prev & (CriminalFlags.Sentenced | CriminalFlags.Prisoner)) != 0)
                    {
                        DynamicCrimeState.OutcomeRehabilitated++;
                        m_Log.Info($"[Outcome] citizen {g.Index} rehabilitated (flags cleared after sentence)");
                    }
                    else
                    {
                        DynamicCrimeState.OutcomeEscaped++;
                        DynamicCrimeState.EscapeFalloutPending++;
                        m_Log.Info($"[Outcome] citizen {g.Index} escaped (vanished with flags)");
                    }
                }
                entities.Dispose(); crims.Dispose();
            }
            catch (System.Exception ex)
            {
                m_Log.Warn($"[Outcome] audit failed (safe): {ex.Message}");
            }
        }

        private void PruneMemory()
        {
            try
            {
                if (m_LastFlags.Count > 5000)
                {
                    m_LastFlags.Clear();
                    m_Log.Info("[Audit] flag memory pruned (cap)");
                }
                if (m_LastSick.Count > 2000) m_LastSick.Clear();
            }
            catch { }
        }
    }
}
