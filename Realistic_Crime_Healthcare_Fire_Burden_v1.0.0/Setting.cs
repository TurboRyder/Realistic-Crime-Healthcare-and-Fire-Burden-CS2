using Colossal.IO.AssetDatabase;
using Game.Modding;
using Game.Settings;
using Game.UI.Widgets;

namespace RealisticCrimeSystem
{
    [FileLocation(nameof(RealisticCrimeSystem))]
    [SettingsUIGroupOrder(kPresetGroup, kTuningGroup, kEventsGroup, kResponseGroup)]
    [SettingsUIShowGroupName(kPresetGroup, kTuningGroup, kEventsGroup, kResponseGroup)]
    public class Setting : ModSetting
    {
        public const string kSection = "Main";
        public const string kPresetGroup = "Preset";
        public const string kTuningGroup = "Tuning";
        public const string kEventsGroup = "Crime Events";
        public const string kResponseGroup = "Response";

        public Setting(IMod mod) : base(mod) { }

        private DifficultyPreset m_Preset = DifficultyPreset.Full;

        // A preset is a bundle of slider positions: selecting one visibly snaps every
        // slider to that preset's numbers, so the Tuning tab always shows what is live.
        // (Settings deserialize in declaration order - preset first - so saved slider
        // tweaks load after the snap and are preserved.)
        [SettingsUISection(kSection, kPresetGroup)]
        [SettingsUIDropdown(typeof(Setting), nameof(GetPresetItems))]
        public DifficultyPreset SelectedPreset
        {
            get => m_Preset;
            set { m_Preset = value; ApplyPresetToSliders(value); }
        }

        // Tuning sliders hold ABSOLUTE values (not multipliers) - what you see is what
        // the systems write. Every slider is live at its write site.
        [SettingsUISection(kSection, kTuningGroup)]
        [SettingsUISlider(min = 0.25f, max = 8f, step = 0.05f, unit = "custom")]
        [SettingsUICustomFormat(fractionDigits = 2)]
        public float VolumeMult { get; set; } = 4f;

        [SettingsUISection(kSection, kTuningGroup)]
        [SettingsUISlider(min = 0.5f, max = 3f, step = 0.05f, unit = "custom")]
        [SettingsUICustomFormat(fractionDigits = 2)]
        public float ResponseMult { get; set; } = 2f;

        [SettingsUISection(kSection, kTuningGroup)]
        [SettingsUISlider(min = 0.25f, max = 2f, step = 0.05f, unit = "custom")]
        [SettingsUICustomFormat(fractionDigits = 2)]
        public float SickWavesMult { get; set; } = 1f;

        [SettingsUISection(kSection, kTuningGroup)]
        [SettingsUISlider(min = 1f, max = 40f, step = 1f, unit = "custom")]
        [SettingsUICustomFormat(fractionDigits = 0)]
        public float FireMult { get; set; } = 20f;

        [SettingsUISection(kSection, kTuningGroup)]
        [SettingsUISlider(min = 25f, max = 100f, step = 1f, unit = "custom")]
        [SettingsUICustomFormat(fractionDigits = 0)]
        public float UnhappinessMult { get; set; } = 50f;

        // Circuit breaker threshold (% criminals/pop). Halts difficulty writes above it.
        [SettingsUISection(kSection, kTuningGroup)]
        [SettingsUISlider(min = 5f, max = 20f, step = 0.5f, unit = "custom")]
        [SettingsUICustomFormat(fractionDigits = 1)]
        public float BreakerPct { get; set; } = 10f;

        // Event sources: vanilla occurrence scaling applies to all crime; per-event
        // toggles below gate the mod's own additions only.
        [SettingsUISection(kSection, kEventsGroup)]
        public bool EnableOverdoseEvents { get; set; } = true;

        [SettingsUISection(kSection, kEventsGroup)]
        public bool EnableFirePressure { get; set; } = true;

        [SettingsUISection(kSection, kEventsGroup)]
        public bool EnableOverdoseWave { get; set; } = true;

        [SettingsUISection(kSection, kEventsGroup)]
        public bool EnableVandalismDecay { get; set; } = true; // escape fallout: escapes deteriorate high-crime areas

        [SettingsUISection(kSection, kResponseGroup)]
        public bool EnableRehab { get; set; } = true;

        [SettingsUISection(kSection, kResponseGroup)]
        public bool EnableOptionBPrefabClones { get; set; } = true; // save diagnostic for old clone-persisted saves

        [SettingsUISection(kSection, kResponseGroup)]
        public bool EnableCrimeChirps { get; set; } = true;

        [SettingsUISection(kSection, kResponseGroup)]
        [SettingsUIDropdown(typeof(Setting), nameof(GetCadenceItems))]
        public Cadence ChirpCadence { get; set; } = Cadence.Standard;

        public DropdownItem<int>[] GetPresetItems()
        {
            return new[]
            {
                new DropdownItem<int> { value = 0, displayName = "Balanced" },
                new DropdownItem<int> { value = 1, displayName = "Medium" },
                new DropdownItem<int> { value = 2, displayName = "Full" },
            };
        }

        public DropdownItem<int>[] GetCadenceItems()
        {
            return new[]
            {
                new DropdownItem<int> { value = 0, displayName = "Frequent (~3 game-hours)" },
                new DropdownItem<int> { value = 1, displayName = "Standard (~6 game-hours)" },
                new DropdownItem<int> { value = 2, displayName = "Rare (~12 game-hours)" },
            };
        }

        private void ApplyPresetToSliders(DifficultyPreset preset)
        {
            // Full = playtest-verified challenge, Medium ~= 70%, Balanced = vanilla.
            switch (preset)
            {
                case DifficultyPreset.Full:
                    VolumeMult = 4f; ResponseMult = 2f; SickWavesMult = 1f;
                    FireMult = 20f; UnhappinessMult = 50f; BreakerPct = 10f;
                    break;
                case DifficultyPreset.Medium:
                    VolumeMult = 2.8f; ResponseMult = 1.5f; SickWavesMult = 1f;
                    FireMult = 14f; UnhappinessMult = 70f; BreakerPct = 10f;
                    break;
                default:
                    VolumeMult = 1f; ResponseMult = 1f; SickWavesMult = 1f;
                    FireMult = 1f; UnhappinessMult = 100f; BreakerPct = 10f;
                    break;
            }
        }

        private bool IsFull => SelectedPreset == DifficultyPreset.Full;
        private bool IsMedium => SelectedPreset == DifficultyPreset.Medium;

        // ---- Preset bundle values without sliders (logged at write sites) ----
        // Full = the playtest-verified challenge. Medium ~= 70% of Full. Balanced = vanilla.

        public float GetToleranceForPreset()
        {
            if (IsFull) return 500f;
            if (IsMedium) return 650f;
            return 1000f;
        }

        public void GetCrimeModTerms(out float baseMod, out float k, out float clampMax)
        {
            if (IsFull) { baseMod = 1.0f; k = 6.0f; clampMax = 1.5f; return; }
            if (IsMedium) { baseMod = 0.7f; k = 4.0f; clampMax = 1.05f; return; }
            baseMod = 0f; k = 0f; clampMax = 0f;
        }

        public void GetSentenceMults(out float jail, out float prison)
        {
            if (IsFull) { jail = 2.0f; prison = 1.5f; return; }
            if (IsMedium) { jail = 1.5f; prison = 1.2f; return; }
            jail = 1f; prison = 1f;
        }

        public float GetFireSpread()
        {
            if (IsFull || IsMedium) return 3.0f;
            return 1.0f;
        }

        public int GetHappinessNegligible()
        {
            if (IsFull) return 8000;
            if (IsMedium) return 6500;
            return 5000;
        }

        public float GetWelfareRecurrenceFactor()
        {
            // Lower = welfare coverage suppresses reoffending more (the carrot).
            if (IsFull) return 0.2f;
            if (IsMedium) return 0.3f;
            return 0.4f;
        }

        public float GetPrisonTimeModifier()
        {
            // CityModifier(PrisonTime) relative delta: longer sentences = prison capacity pressure.
            if (IsFull) return 0.4f;
            if (IsMedium) return 0.3f;
            return 0.0f;
        }

        public float GetRehabRetention()
        {
            // Rehab retention: Balanced 35%, Medium 30%, Full 25% (shorter stays = faster return = harder)
            if (IsFull) return 0.25f;
            if (IsMedium) return 0.3f;
            return 0.35f;
        }

        public int GetOverdoseGate()
        {
            // Sick-wave gate: Balanced criminals>=30, Medium >=25, Full >=20 + trickle
            if (IsFull) return 20;
            if (IsMedium) return 25;
            return 30;
        }

        public override void SetDefaults()
        {
            SelectedPreset = DifficultyPreset.Full; // snaps sliders via setter
            EnableOverdoseEvents = true;
            EnableFirePressure = true;
            EnableOverdoseWave = true;
            EnableVandalismDecay = true;
            EnableRehab = true;
            EnableOptionBPrefabClones = true;
            EnableCrimeChirps = true;
            ChirpCadence = Cadence.Standard;
        }

        public enum DifficultyPreset
        {
            Balanced = 0,
            Medium = 1,
            Full = 2,
        }

        public enum Cadence
        {
            Frequent = 0,
            Standard = 1,
            Rare = 2,
        }
    }
}
