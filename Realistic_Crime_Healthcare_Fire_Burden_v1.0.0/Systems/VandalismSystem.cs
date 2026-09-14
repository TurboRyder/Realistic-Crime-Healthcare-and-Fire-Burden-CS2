using Colossal.Logging;
using Game;
using Game.Buildings;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace RealisticCrimeSystem.Systems
{
    /// <summary>
    /// Escape fallout (repurposed vandalism): each escape outcome deteriorates the
    /// highest-crime building, giving escapes economic teeth via land value/rent.
    /// One decay per pending escape (capped per run). Causally honest: generic
    /// ambient decay stays off until deterioration is visibly confirmed.
    /// 1.6.0f1 only. All load-guarded.
    /// </summary>
    public partial class VandalismSystem : GameSystemBase
    {
        private ILog m_Log;
        private EntityQuery m_TargetQuery;
        private uint m_Tick;

        protected override void OnCreate()
        {
            base.OnCreate();
            m_Log = LogManager.GetLogger($"{nameof(RealisticCrimeSystem)}.{nameof(VandalismSystem)}");
            m_TargetQuery = GetEntityQuery(ComponentType.ReadOnly<CrimeProducer>(), ComponentType.ReadWrite<BuildingCondition>(), ComponentType.ReadOnly<Building>());
            m_Log.Info("VandalismSystem OnCreate - 1.6.0f1 escape fallout (decay on escape outcomes)");
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
            if (Mod.Settings != null && !Mod.Settings.EnableVandalismDecay) return;
            if (DynamicCrimeState.EscapeFalloutPending <= 0) return;
            if (m_TargetQuery.IsEmptyIgnoreFilter) return;

            bool full = Mod.Settings != null && Mod.Settings.SelectedPreset == Setting.DifficultyPreset.Full;
            int cap = full ? 3 : 2;
            int pending = math.min(DynamicCrimeState.EscapeFalloutPending, cap);

            var entities = m_TargetQuery.ToEntityArray(Allocator.Temp);
            var crimes = m_TargetQuery.ToComponentDataArray<CrimeProducer>(Allocator.Temp);
            var conds = m_TargetQuery.ToComponentDataArray<BuildingCondition>(Allocator.Temp);
            int decayed = 0;
            for (int n = 0; n < pending; n++)
            {
                // Highest-crime building with remaining condition
                int best = -1;
                float bestCrime = 0f;
                for (int i = 0; i < entities.Length; i++)
                {
                    if (conds[i].m_Condition <= 0) continue;
                    if (crimes[i].m_Crime > bestCrime) { bestCrime = crimes[i].m_Crime; best = i; }
                }
                if (best < 0) break;
                var c = conds[best];
                c.m_Condition = math.max(0, c.m_Condition - 1);
                EntityManager.SetComponentData(entities[best], c);
                conds[best] = c;
                decayed++;
                DynamicCrimeState.EscapeFalloutPending--;
            }
            entities.Dispose(); crimes.Dispose(); conds.Dispose();
            if (decayed > 0) m_Log.Info($"[Fallout] {decayed} escape(s) deteriorated high-crime buildings (pending left={DynamicCrimeState.EscapeFalloutPending})");
        }
    }
}
