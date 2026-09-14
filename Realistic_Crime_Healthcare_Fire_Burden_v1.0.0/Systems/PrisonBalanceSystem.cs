using Colossal.Logging;
using Game;
using Game.Buildings;
using Game.Citizens;
using Game.Common;
using Game.Prefabs;
using Game.Tools;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace RealisticCrimeSystem.Systems
{
    /// <summary>
    /// Jail/prison occupancy + capacity observer (1.6.0f1).
    /// Capacity mirrors PoliceInfoviewUISystem: per placed ACTIVE building (non-zero
    /// Efficiency), prefab data merged with installed upgrades via UpgradeUtils.CombineStats.
    /// Demolished/inactive buildings are excluded by construction.
    /// </summary>
    public partial class PrisonBalanceSystem : GameSystemBase
    {
        private static int s_DiagLogged;
        private ILog m_Log;
        private EntityQuery m_StationQuery;
        private EntityQuery m_PrisonQuery;
        private EntityQuery m_CriminalQuery;

        protected override void OnCreate()
        {
            base.OnCreate();
            m_Log = LogManager.GetLogger($"{nameof(RealisticCrimeSystem)}.{nameof(PrisonBalanceSystem)}");
            var exclude = new ComponentType[]
            {
                ComponentType.ReadOnly<Deleted>(),
                ComponentType.ReadOnly<Destroyed>(),
                ComponentType.ReadOnly<Temp>(),
            };
            m_StationQuery = GetEntityQuery(new EntityQueryDesc
            {
                All = new ComponentType[]
                {
                    ComponentType.ReadOnly<Game.Buildings.PoliceStation>(),
                    ComponentType.ReadOnly<PrefabRef>(),
                    ComponentType.ReadOnly<Game.Buildings.Building>(),
                },
                None = exclude,
            });
            m_PrisonQuery = GetEntityQuery(new EntityQueryDesc
            {
                All = new ComponentType[]
                {
                    ComponentType.ReadOnly<Game.Buildings.Prison>(),
                    ComponentType.ReadOnly<PrefabRef>(),
                },
                None = exclude,
            });
            m_CriminalQuery = GetEntityQuery(ComponentType.ReadOnly<Criminal>());
            m_Log.Info("PrisonBalanceSystem OnCreate - 1.6.0f1 per-instance capacity (infoview method)");
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

            int criminals = m_CriminalQuery.CalculateEntityCount();
            int jailOcc = 0, prisonOcc = 0, jailCap = 0, prisonCap = 0;
            int stations = 0, prisons = 0;
            try
            {
                var stEntities = m_StationQuery.ToEntityArray(Allocator.Temp);
                for (int i = 0; i < stEntities.Length; i++)
                {
                    var e = stEntities[i];
                    float eff = GetBuildingEfficiency(e);
                    if (eff <= 0f)
                        continue; // inactive (e.g. deactivated) - matches infoview active check
                    stations++;
                    if (EntityManager.HasBuffer<Occupant>(e))
                        jailOcc += EntityManager.GetBuffer<Occupant>(e, true).Length;
                    var prefab = EntityManager.GetComponentData<PrefabRef>(e).m_Prefab;
                    if (prefab != Entity.Null && EntityManager.HasComponent<PoliceStationData>(prefab))
                    {
                        var data = EntityManager.GetComponentData<PoliceStationData>(prefab);
                        int baseCap = data.m_JailCapacity;
                        int upgCount = EntityManager.HasBuffer<InstalledUpgrade>(e) ? EntityManager.GetBuffer<InstalledUpgrade>(e, true).Length : 0;
                        if (upgCount > 0)
                            UpgradeUtils.CombineStats<PoliceStationData>(EntityManager, ref data, EntityManager.GetBuffer<InstalledUpgrade>(e));
                        jailCap += data.m_JailCapacity;
                        if (s_DiagLogged < 40)
                        {
                            bool hasBuilding = EntityManager.HasComponent<Game.Buildings.Building>(e);
                            m_Log.Info($"[CapDiag] station ent={e.Index} eff={eff:F2} prefab={prefab.Index} baseCap={baseCap} upgrades={upgCount} combinedCap={data.m_JailCapacity} hasBuilding={hasBuilding}");
                            s_DiagLogged++;
                        }
                    }
                    else if (s_DiagLogged < 40)
                    {
                        m_Log.Info($"[CapDiag] station ent={e.Index} eff={eff:F2} prefab={(prefab == Entity.Null ? "Null" : prefab.Index.ToString())} NO PoliceStationData");
                        s_DiagLogged++;
                    }
                }
                stEntities.Dispose();

                var prEntities = m_PrisonQuery.ToEntityArray(Allocator.Temp);
                for (int i = 0; i < prEntities.Length; i++)
                {
                    var e = prEntities[i];
                    if (GetBuildingEfficiency(e) <= 0f)
                        continue;
                    prisons++;
                    if (EntityManager.HasBuffer<Occupant>(e))
                        prisonOcc += EntityManager.GetBuffer<Occupant>(e, true).Length;
                    var prefab = EntityManager.GetComponentData<PrefabRef>(e).m_Prefab;
                    if (prefab != Entity.Null && EntityManager.HasComponent<PrisonData>(prefab))
                    {
                        var data = EntityManager.GetComponentData<PrisonData>(prefab);
                        if (EntityManager.HasBuffer<InstalledUpgrade>(e))
                            UpgradeUtils.CombineStats<PrisonData>(EntityManager, ref data, EntityManager.GetBuffer<InstalledUpgrade>(e));
                        prisonCap += data.m_PrisonerCapacity;
                    }
                }
                prEntities.Dispose();
            }
            catch (System.Exception ex)
            {
                m_Log.Warn($"[PrisonObserve] failed (safe): {ex.Message}");
            }

            DynamicCrimeState.JailOccupancy = jailOcc;
            DynamicCrimeState.JailCapacity = jailCap;
            DynamicCrimeState.ActiveStations = stations;
            DynamicCrimeState.PrisonOccupancy = prisonOcc;
            DynamicCrimeState.PrisonCapacity = prisonCap;
            m_Log.Info($"[PrisonObserve] stations={stations} prisons={prisons} criminals={criminals} jail={jailOcc}/{jailCap} prison={prisonOcc}/{prisonCap}");
        }

        // Manual efficiency product (avoids BuildingUtils Span<> overload unavailable on net48).
        // Mirrors infoview active check: non-zero overall efficiency.
        private float GetBuildingEfficiency(Entity e)
        {
            if (!EntityManager.HasBuffer<Efficiency>(e)) return 0f;
            var buf = EntityManager.GetBuffer<Efficiency>(e, true);
            if (buf.Length == 0) return 1f;
            float eff = 1f;
            for (int i = 0; i < buf.Length; i++) eff *= buf[i].m_Efficiency;
            return eff;
        }
    }
}
