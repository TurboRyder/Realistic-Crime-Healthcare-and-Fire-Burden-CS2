using Colossal.Logging;
using Game;
using Game.Buildings;
using Game.Common;
using Game.Events;
using Game.Tools;
using Unity.Collections;
using Unity.Entities;

namespace RealisticCrimeSystem.Systems
{
    /// <summary>
    /// Fire outcome audit (v1.0): baselines the set of OnFire entities each run and
    /// classifies vanishments. Assumes OnFire is building-sided (mirrors CrimeVictim /
    /// HealthProblem patterns; a first-run diag names the layout and warns loudly if
    /// it is event-sided instead). Building still standing without Destroyed/Condemned
    /// = extinguished; otherwise burnt down (includes player-demolished-while-burning,
    /// indistinguishable and rare - disclosed, not fixed). Creates nothing, deletes
    /// nothing. 1.6.0f1 only. All load-guarded.
    /// </summary>
    public partial class FireOutcomeSystem : GameSystemBase
    {
        private ILog m_Log;
        private EntityQuery m_FireQuery;
        private readonly System.Collections.Generic.HashSet<Entity> m_Burning = new System.Collections.Generic.HashSet<Entity>();
        private bool m_DiagDone;
        private bool m_Baselined;
        private uint m_Tick;

        protected override void OnCreate()
        {
            base.OnCreate();
            m_Log = LogManager.GetLogger($"{nameof(RealisticCrimeSystem)}.{nameof(FireOutcomeSystem)}");
            m_FireQuery = GetEntityQuery(new EntityQueryDesc
            {
                All = new ComponentType[] { ComponentType.ReadOnly<OnFire>() },
                None = new ComponentType[]
                {
                    ComponentType.ReadOnly<Deleted>(),
                    ComponentType.ReadOnly<Destroyed>(),
                    ComponentType.ReadOnly<Temp>(),
                },
            });
            m_Log.Info("FireOutcomeSystem OnCreate - 1.6.0f1 fire outcome audit (no entity creation)");
        }

        public override int GetUpdateInterval(SystemUpdatePhase phase)
        {
            if (phase == SystemUpdatePhase.GameSimulation) return 512;
            return base.GetUpdateInterval(phase);
        }

        protected override void OnUpdate()
        {
            var gm = Game.SceneFlow.GameManager.instance;
            if (gm == null || gm.gameMode != GameMode.Game || gm.isGameLoading) return;
            m_Tick++;
            AuditFires();
            PruneMemory();
        }

        private void AuditFires()
        {
            try
            {
                var entities = m_FireQuery.ToEntityArray(Allocator.Temp);
                if (!m_DiagDone && entities.Length > 0)
                {
                    m_DiagDone = true;
                    int onBuilding = 0;
                    for (int i = 0; i < entities.Length; i++)
                        if (EntityManager.HasComponent<Building>(entities[i])) onBuilding++;
                    m_Log.Info($"[FireDiag] OnFire entities={entities.Length} onBuilding={onBuilding} (audit assumes building-sided)");
                    if (onBuilding * 2 < entities.Length)
                        m_Log.Warn("[FireDiag] OnFire is mostly NOT building-sided - outcome classification needs the event-sided path!");
                }
                var seen = new System.Collections.Generic.HashSet<Entity>();
                for (int i = 0; i < entities.Length; i++)
                {
                    Entity e = entities[i];
                    seen.Add(e);
                    if (m_Burning.Add(e) && m_Baselined)
                    {
                        DynamicCrimeState.FireStarted++;
                        m_Log.Info($"[FireOutcome] fire started ent={e.Index}");
                    }
                }
                if (!m_Baselined)
                {
                    m_Baselined = true; // first sight is baseline, never counted
                    m_Log.Info($"[FireOutcome] baselined {seen.Count} pre-existing fires (not counted)");
                }
                var gone = new System.Collections.Generic.List<Entity>();
                foreach (var b in m_Burning)
                {
                    if (seen.Contains(b)) continue;
                    gone.Add(b);
                }
                foreach (var g in gone)
                {
                    m_Burning.Remove(g);
                    // Building-sided: component removed while standing = extinguished.
                    // Entity gone/destroyed/condemned = burnt down (or demolished mid-fire).
                    bool standing = EntityManager.Exists(g) &&
                        !EntityManager.HasComponent<Destroyed>(g) &&
                        !EntityManager.HasComponent<Condemned>(g);
                    if (standing)
                    {
                        DynamicCrimeState.FireExtinguished++;
                        m_Log.Info($"[FireOutcome] fire extinguished ent={g.Index}");
                    }
                    else
                    {
                        DynamicCrimeState.FireBurnedDown++;
                        m_Log.Info($"[FireOutcome] building burnt down ent={g.Index}");
                    }
                }
                DynamicCrimeState.FireActive = seen.Count;
                entities.Dispose();
            }
            catch (System.Exception ex)
            {
                m_Log.Warn($"[FireOutcome] audit failed (safe): {ex.Message}");
            }
        }

        private void PruneMemory()
        {
            try
            {
                if (m_Burning.Count > 2000)
                {
                    m_Burning.Clear();
                    m_Log.Info("[FireOutcome] burning memory pruned (cap)");
                }
            }
            catch { }
        }
    }
}
