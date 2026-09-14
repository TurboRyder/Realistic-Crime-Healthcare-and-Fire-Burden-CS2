using Colossal.Logging;
using Game;
using Game.Prefabs;
using Unity.Collections;
using Unity.Entities;

namespace RealisticCrimeSystem.Systems
{
    /// <summary>
    /// Fire baseline observer (one-shot): logs every FireData prefab's values.
    /// Actual scaling lives in DynamicCrimeSystem (preset x Fire slider).
    /// 1.6.0f1 only. All load-guarded.
    /// </summary>
    public partial class FirePressureSystem : GameSystemBase
    {
        private ILog m_Log;
        private EntityQuery m_FireDataQuery;
        private bool m_Logged;

        protected override void OnCreate()
        {
            base.OnCreate();
            m_Log = LogManager.GetLogger($"{nameof(RealisticCrimeSystem)}.{nameof(FirePressureSystem)}");
            m_FireDataQuery = GetEntityQuery(ComponentType.ReadOnly<FireData>());
            m_Log.Info("FirePressureSystem OnCreate - 1.6.0f1 baseline observation (no writes yet)");
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
            if (m_Logged) return;
            if (Mod.Settings != null && !Mod.Settings.EnableFirePressure) { m_Logged = true; return; }
            try
            {
                var entities = m_FireDataQuery.ToEntityArray(Allocator.Temp);
                if (entities.Length == 0) { entities.Dispose(); return; }
                for (int i = 0; i < entities.Length; i++)
                {
                    var d = EntityManager.GetComponentData<FireData>(entities[i]);
                    m_Log.Info($"[FireData] prefab ent={entities[i].Index} target={d.m_RandomTargetType} startP={d.m_StartProbability:F4} startI={d.m_StartIntensity:F2} esc={d.m_EscalationRate:F4} spreadP={d.m_SpreadProbability:F4} spreadR={d.m_SpreadRange:F1}");
                }
                m_Log.Info($"[FireData] {entities.Length} fire prefabs observed, no writes this phase");
                entities.Dispose();
            }
            catch (System.Exception ex)
            {
                m_Log.Warn($"[FireData] observe failed (safe): {ex.Message}");
            }
            m_Logged = true;
        }
    }
}
