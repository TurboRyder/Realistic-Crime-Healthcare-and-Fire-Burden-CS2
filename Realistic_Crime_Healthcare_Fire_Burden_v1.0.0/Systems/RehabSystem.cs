using Colossal.Logging;
using Game;
using Game.Citizens;
using Unity.Collections;
using Unity.Entities;

namespace RealisticCrimeSystem.Systems
{
    public partial class RehabSystem : GameSystemBase
    {
        private ILog m_Log;
        private EntityQuery m_HealthQuery;

        protected override void OnCreate()
        {
            base.OnCreate();
            m_Log = LogManager.GetLogger($"{nameof(RealisticCrimeSystem)}.{nameof(RehabSystem)}");
            m_HealthQuery = GetEntityQuery(ComponentType.ReadWrite<HealthProblem>());
            m_Log.Info("RehabSystem OnCreate - 1.6.0f1 overdose-profile + 5% daily seekers");
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
            if (Mod.Settings != null && !Mod.Settings.EnableRehab) return;
            if (m_HealthQuery.IsEmptyIgnoreFilter) return;
            try
            {
                float retention = Mod.Settings != null ? Mod.Settings.GetRehabRetention() : 0.35f;
                var entities = m_HealthQuery.ToEntityArray(Allocator.Temp);
                var problems = m_HealthQuery.ToComponentDataArray<HealthProblem>(Allocator.Temp);
                int extended = 0, odProfile = 0, daily = 0;
                for (int i = 0; i < entities.Length; i++)
                {
                    var hp = problems[i];
                    if ((hp.m_Flags & HealthProblemFlags.Sick) == 0) continue;
                    bool isCriminal = EntityManager.HasComponent<Criminal>(entities[i]);
                    float thresh = isCriminal ? retention : 0.05f; // overdose-profile vs daily seekers
                    if (((uint)entities[i].Index * 2654435761u & 0xFF) >= (thresh * 255)) continue;
                    if (hp.m_Timer > 245) continue;
                    hp.m_Timer = (byte)(hp.m_Timer + 10);
                    EntityManager.SetComponentData(entities[i], hp);
                    extended++;
                    if (isCriminal) odProfile++; else daily++;
                    if (extended >= 2) break;
                }
                entities.Dispose(); problems.Dispose();
                if (extended > 0) m_Log.Info($"[Rehab] extended {extended} ({odProfile} overdose-profile + {daily} daily seekers, retention={retention:P0})");
            }
            catch (System.Exception ex)
            {
                m_Log.Warn($"[Rehab] failed (safe): {ex.Message}");
            }
        }
    }
}
