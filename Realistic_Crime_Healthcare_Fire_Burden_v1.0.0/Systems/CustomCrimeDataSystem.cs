using Colossal.Logging;
using Colossal.Mathematics;
using Game;
using Game.Prefabs;
using Unity.Collections;
using Unity.Entities;

namespace RealisticCrimeSystem.Systems
{
    public partial class CustomCrimeDataSystem : GameSystemBase
    {
        private ILog m_Log;
        private EntityQuery m_CrimeDataQuery;
        private bool m_Done = false;

        protected override void OnCreate()
        {
            base.OnCreate();
            m_Log = LogManager.GetLogger($"{nameof(RealisticCrimeSystem)}.{nameof(CustomCrimeDataSystem)}");
            m_CrimeDataQuery = GetEntityQuery(ComponentType.ReadOnly<CrimeData>());
            m_Log.Info("CustomCrimeDataSystem OnCreate - 1.6.0f1 Option B clones (Domestic/Computer/Vehicle)");
        }

        public override int GetUpdateInterval(SystemUpdatePhase phase)
        {
            if (phase == SystemUpdatePhase.GameSimulation) return 512;
            return base.GetUpdateInterval(phase);
        }

        protected override void OnUpdate()
        {
            if (m_Done) return;
            var gm = Game.SceneFlow.GameManager.instance;
            if (gm == null || gm.gameMode != GameMode.Game || gm.isGameLoading) return;
            if (Mod.Settings != null && !Mod.Settings.EnableOptionBPrefabClones)
            {
                m_Log.Info("[CrimeData] Option B disabled by settings, skipping all clones");
                m_Done = true;
                return;
            }
            if (m_CrimeDataQuery.IsEmptyIgnoreFilter) return;

            try
            {
                var entities = m_CrimeDataQuery.ToEntityArray(Allocator.Temp);
                if (entities.Length == 0) { entities.Dispose(); return; }
                // DIAGNOSTIC for v0.4 behavioral spawner: dump vanilla prefab composition + values (one-shot)
                try
                {
                    var baseEntity = entities[0];
                    var types = EntityManager.GetComponentTypes(baseEntity);
                    for (int t = 0; t < types.Length; t++)
                        m_Log.Info($"[CrimeDataDiag] comp[{t}] = {types[t].ToString()}");
                    types.Dispose();
                    var d = EntityManager.GetComponentData<CrimeData>(baseEntity);
                    m_Log.Info($"[CrimeDataDiag] type={d.m_CrimeType} target={d.m_RandomTargetType} occ=({d.m_OccurenceProbability.min:F4},{d.m_OccurenceProbability.max:F4}) rec=({d.m_RecurrenceProbability.min:F4},{d.m_RecurrenceProbability.max:F4}) alarm=({d.m_AlarmDelay.min:F2},{d.m_AlarmDelay.max:F2}) dur=({d.m_CrimeDuration.min:F2},{d.m_CrimeDuration.max:F2}) jail=({d.m_JailTimeRange.min:F1},{d.m_JailTimeRange.max:F1}) prison=({d.m_PrisonTimeRange.min:F1},{d.m_PrisonTimeRange.max:F1}) prisonP={d.m_PrisonProbability:F3} hasEventData={EntityManager.HasComponent<EventData>(baseEntity)}");
                    // Archetype composition via managed types (for spawner fallback path)
                    try
                    {
                        var ed = EntityManager.GetComponentData<EventData>(baseEntity);
                        var archTypes = ed.m_Archetype.GetComponentTypes();
                        for (int a = 0; a < archTypes.Length; a++)
                        {
                            string tn;
                            try { tn = archTypes[a].GetManagedType()?.FullName ?? "unmanaged"; }
                            catch { tn = "unknown"; }
                            m_Log.Info($"[CrimeDataDiag] archetype[{a}] = {tn}");
                        }
                        m_Log.Info($"[CrimeDataDiag] concurrentLimit={ed.m_ConcurrentLimit}");
                    }
                    catch (System.Exception ex3)
                    {
                        m_Log.Warn($"[CrimeDataDiag] archetype dump failed (safe): {ex3.Message}");
                    }
                }
                catch (System.Exception ex2)
                {
                    m_Log.Warn($"[CrimeDataDiag] failed (safe): {ex2.Message}");
                }
                // SAVE-DIAG (v1.0): old Instantiate clones used to persist in saves and break
                // deserialization (Unknown prefab ID [Missing] CTD on load). We deliberately do
                // NOT destroy extras: entity order is not guaranteed, so "keep first" could keep
                // a clone and destroy a legitimate vanilla/DLC CrimeData. Extras are reported for
                // manual review instead; the occurrence loop handles N prefabs safely.
                if (entities.Length > 1)
                {
                    m_Log.Warn($"[CrimeData] {entities.Length} CrimeData entities present (expected 1 vanilla). Extras left untouched - review manually if this persists.");
                }
                else
                {
                    m_Log.Info("[CrimeData] 1 vanilla CrimeData present, clean. Prefab cloning stays DISABLED (caused save CTD).");
                }
                entities.Dispose();
            }
            catch (System.Exception ex)
            {
                m_Log.Warn($"[CrimeData] Clone failed (safe): {ex.Message}");
            }
            m_Done = true;
        }
    }
}
