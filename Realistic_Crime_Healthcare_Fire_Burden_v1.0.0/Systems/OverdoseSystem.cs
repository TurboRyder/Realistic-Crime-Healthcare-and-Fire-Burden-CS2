using Colossal.Logging;
using Game;
using Game.Citizens;
using Game.Events;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace RealisticCrimeSystem.Systems
{
    public partial class OverdoseSystem : GameSystemBase
    {
        private ILog m_Log;
        private EntityQuery m_CriminalQuery;
        private EntityQuery m_CitizenQuery;
        private uint m_Tick;

        protected override void OnCreate()
        {
            base.OnCreate();
            m_Log = LogManager.GetLogger($"{nameof(RealisticCrimeSystem)}.{nameof(OverdoseSystem)}");
            m_CriminalQuery = GetEntityQuery(ComponentType.ReadOnly<Criminal>());
            m_CitizenQuery = GetEntityQuery(ComponentType.ReadOnly<Citizen>());
            m_Log.Info("OverdoseSystem OnCreate - 1.6.0f1 civilians only (never criminals)");
        }

        public override int GetUpdateInterval(SystemUpdatePhase phase)
        {
            if (phase == SystemUpdatePhase.GameSimulation)
            {
                // Sick-waves intensity slider scales the spawn interval (real lever:
                // 0.25x -> every 16384 ticks, 2x -> every 2048 ticks).
                float mult = Mod.Settings != null ? Mod.Settings.SickWavesMult : 1f;
                if (mult < 0.25f) mult = 0.25f;
                return (int)(4096f / mult);
            }
            return base.GetUpdateInterval(phase);
        }

        protected override void OnUpdate()
        {
            var gm = Game.SceneFlow.GameManager.instance;
            if (gm == null || gm.gameMode != GameMode.Game || gm.isGameLoading) return;
            m_Tick++;
            if (Mod.Settings != null && !Mod.Settings.EnableOverdoseEvents) return;
            int gate = Mod.Settings != null ? Mod.Settings.GetOverdoseGate() : 30;
            int criminals = m_CriminalQuery.CalculateEntityCount();
            // During an overdose wave the gate drops so civilian cases surface too
            if (DynamicCrimeState.WaveActive) gate = math.min(gate, 10);
            if (criminals < gate && !DynamicCrimeState.WaveActive) return;

            try
            {
                Entity target = Entity.Null;
                string kind = "";
                // v0.8: NEVER target criminals. Playtest proved wave criminal-targeting
                // sickens robbers mid-scene and ambulances steal them from attending police
                // (13 criminal hits -> 15 hospitalizations vs 1 arrest). Waves hit civilians only.
                if (target == Entity.Null)
                {
                    // Civilian only: sample up to 8 random citizens, pick lowest WellBeing (residential distress proxy, residential weighted)
                    var entities = m_CitizenQuery.ToEntityArray(Allocator.Temp);
                    var citizens = m_CitizenQuery.ToComponentDataArray<Citizen>(Allocator.Temp);
                    if (entities.Length > 0)
                    {
                        byte bestWell = 255;
                        for (int s = 0; s < 8; s++)
                        {
                            int idx = (int)((m_Tick * 7 + (uint)s * 131) % (uint)entities.Length);
                            var c = citizens[idx];
                            if (c.GetAge() != CitizenAge.Adult) continue;
                            if ((c.m_State & CitizenFlags.Tourist) != 0) continue;
                            if (EntityManager.HasComponent<Criminal>(entities[idx])) continue; // never divert arrestables to hospital
                            if (c.m_WellBeing < bestWell) { bestWell = c.m_WellBeing; target = entities[idx]; }
                        }
                        if (target != Entity.Null) kind = $"civilian well={bestWell}";
                    }
                    entities.Dispose(); citizens.Dispose();
                }
                if (target == Entity.Null) return;

                var ev = EntityManager.CreateEntity(typeof(Game.Common.Event), typeof(AddHealthProblem));
                EntityManager.SetComponentData(ev, new AddHealthProblem
                {
                    m_Event = ev,
                    m_Target = target,
                    m_Flags = HealthProblemFlags.Sick | HealthProblemFlags.RequireTransport
                });
                m_Log.Info($"[Overdose] spawned Sick|RequireTransport for {kind} {target.Index} (criminals={criminals}) max 1 per interval, civilians only");
            }
            catch (System.Exception ex)
            {
                m_Log.Warn($"[Overdose] spawn failed (safe): {ex.Message}");
            }
        }
    }
}
