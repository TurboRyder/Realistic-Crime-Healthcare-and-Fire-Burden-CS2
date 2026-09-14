using System.Collections.Generic;
using Colossal;
using Colossal.IO.AssetDatabase;

namespace RealisticCrimeSystem
{
    public class LocaleEN : Colossal.IDictionarySource
    {
        private readonly Setting m_Setting;
        public LocaleEN(Setting setting) { m_Setting = setting; }

        public IEnumerable<KeyValuePair<string, string>> ReadEntries(IList<Colossal.IDictionaryEntryError> errors, Dictionary<string, int> indexCounts)
        {
            return new Dictionary<string, string>
            {
                { m_Setting.GetSettingsLocaleID(), "Realistic Crime, Healthcare and Fire Burden" },
                { m_Setting.GetOptionTabLocaleID(Setting.kSection), "Main" },
                { m_Setting.GetOptionGroupLocaleID(Setting.kPresetGroup), "Preset" },
                { m_Setting.GetOptionGroupLocaleID(Setting.kTuningGroup), "Tuning" },
                { m_Setting.GetOptionGroupLocaleID(Setting.kEventsGroup), "Crime Events" },
                { m_Setting.GetOptionGroupLocaleID(Setting.kResponseGroup), "Response" },

                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.SelectedPreset)), "Difficulty Preset" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.SelectedPreset)), "A preset is a bundle of Tuning values: selecting one moves every slider to that preset's numbers. Balanced keeps vanilla rates, Medium runs about 70% of Full, Full is the complete challenge. After hand-tuning sliders, switch to another preset and back (or Reset to defaults) to re-apply a preset." },
                { m_Setting.GetEnumValueLocaleID(Setting.DifficultyPreset.Balanced), "Balanced" },
                { m_Setting.GetEnumValueLocaleID(Setting.DifficultyPreset.Medium), "Medium" },
                { m_Setting.GetEnumValueLocaleID(Setting.DifficultyPreset.Full), "Full" },

                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.VolumeMult)), "Crime volume" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.VolumeMult)), "Crime occurrence and recurrence multiplier. Vanilla is 1.0; Full sets 4.0." },
                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.ResponseMult)), "Police response" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.ResponseMult)), "Catch-window scale vs vanilla: alarms raise faster and crimes last longer above 1.0, so patrols catch more criminals. Full sets 2.0." },
                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.SickWavesMult)), "Sick waves intensity" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.SickWavesMult)), "How often sick-wave medical events spawn, loading ambulances and hospital beds. Civilians only - never diverts criminals from arrest." },
                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.FireMult)), "Fire frequency" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.FireMult)), "Building fire outbreak multiplier. Vanilla is 1; Full sets 20 through real vanilla fires." },
                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.UnhappinessMult)), "Unhappiness impact" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.UnhappinessMult)), "Crime's well-being impact as % of the vanilla strength. 100 is vanilla; Full sets 50." },
                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.BreakerPct)), "Circuit breaker (%)" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.BreakerPct)), "If criminals exceed this share of population, difficulty writes pause until jails drain. Safety valve against runaway crime." },

                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.EnableOverdoseEvents)), "Sick-wave medical events" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.EnableOverdoseEvents)), "Sick citizens spawn needing ambulances and hospital beds. Adds realistic healthcare load." },

                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.EnableFirePressure)), "Fire pressure" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.EnableFirePressure)), "Scales vanilla fire outbreak rate per preset, pressuring the fire department through real fires." },

                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.EnableOverdoseWave)), "Sick-wave surge events" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.EnableOverdoseWave)), "Occasional sickness surges: cases rise for about a game-day with a warning chirp, then ease off." },

                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.EnableVandalismDecay)), "Escape fallout" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.EnableVandalismDecay)), "Escapes deteriorate the worst high-crime areas, hitting land value over time." },

                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.EnableRehab)), "Rehab retention" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.EnableRehab)), "A share of sick-wave patients stay longer in hospital care, loading beds realistically." },

                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.EnableOptionBPrefabClones)), "Legacy clone diagnostic" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.EnableOptionBPrefabClones)), "Logs a one-shot diagnostic of CrimeData prefabs and reports (never deletes) unexpected extras. Keep on." },

                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.EnableCrimeChirps)), "Crime report chirps (Custom Chirps)" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.EnableCrimeChirps)), "Posts City Crime Bureau reports and alerts to the Chirper when Custom Chirps is installed. No effect without it." },

                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.ChirpCadence)), "Chirp cadence" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.ChirpCadence)), "How often stats reports post: Frequent ~3 game-hours, Standard ~6, Rare ~12. Alerts always post immediately." },
                { m_Setting.GetEnumValueLocaleID(Setting.Cadence.Frequent), "Frequent (~3 game-hours)" },
                { m_Setting.GetEnumValueLocaleID(Setting.Cadence.Standard), "Standard (~6 game-hours)" },
                { m_Setting.GetEnumValueLocaleID(Setting.Cadence.Rare), "Rare (~12 game-hours)" },
            };
        }
        public void Unload() { }
    }
}
