using Colossal.Logging;
using Game;
using Game.Modding;
using Game.SceneFlow;
using Game.Simulation;
using Colossal.IO.AssetDatabase;

namespace RealisticCrimeSystem
{
    public class Mod : IMod
    {
        public static ILog log = LogManager.GetLogger($"{nameof(RealisticCrimeSystem)}.{nameof(Mod)}").SetShowsErrorsInUI(false);
        public static Setting Settings;
        private Setting m_Setting;

        public void OnLoad(UpdateSystem updateSystem)
        {
            log.Info(nameof(OnLoad));
            if (GameManager.instance.modManager.TryGetExecutableAsset(this, out var asset))
                log.Info($"Current mod asset at {asset.path}");

            m_Setting = new Setting(this);
            m_Setting.RegisterInOptionsUI();
            GameManager.instance.localizationManager.AddSource("en-US", new LocaleEN(m_Setting));
            AssetDatabase.global.LoadSettings(nameof(RealisticCrimeSystem), m_Setting, new Setting(this));
            Settings = m_Setting;
            log.Info($"Realistic Crime, Healthcare and Fire Burden 1.0.0 (1.6.0f1) loaded. Preset: {m_Setting.SelectedPreset}");

            updateSystem.UpdateAt<Systems.DynamicCrimeSystem>(SystemUpdatePhase.GameSimulation);
            updateSystem.UpdateAt<Systems.PrisonBalanceSystem>(SystemUpdatePhase.GameSimulation);
            updateSystem.UpdateAt<Systems.VandalismSystem>(SystemUpdatePhase.GameSimulation);
            updateSystem.UpdateAt<Systems.CustomCrimeDataSystem>(SystemUpdatePhase.GameSimulation);
            updateSystem.UpdateAt<Systems.RehabSystem>(SystemUpdatePhase.GameSimulation);
            updateSystem.UpdateAt<Systems.OverdoseSystem>(SystemUpdatePhase.GameSimulation);
            updateSystem.UpdateAt<Systems.CrimeStatsReporterSystem>(SystemUpdatePhase.GameSimulation);
            updateSystem.UpdateAt<Systems.CrimeResponseSystem>(SystemUpdatePhase.GameSimulation);
            updateSystem.UpdateAt<Systems.FirePressureSystem>(SystemUpdatePhase.GameSimulation);
            updateSystem.UpdateAt<Systems.FireOutcomeSystem>(SystemUpdatePhase.GameSimulation);
            // Our CityModifier writes must land after the vanilla rebuild each cycle
            updateSystem.UpdateAfter<Systems.DynamicCrimeSystem, CityModifierUpdateSystem>(SystemUpdatePhase.GameSimulation);
            log.Info("Registered 10 systems at GameSimulation phase");
        }

        public void OnDispose()
        {
            log.Info(nameof(OnDispose));
            if (m_Setting != null)
            {
                m_Setting.UnregisterInOptionsUI();
                m_Setting = null;
                Settings = null;
            }
        }
    }
}
