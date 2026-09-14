import { ModRegistrar } from "cs2/modding";
import { RealisticCrimePanel } from "mods/realistic-crime-panel";

// Best implementation (analysis): HYBRID
// - Standalone Realistic Crime panel (full debug, all views) -> survives vanilla UI changes, shows whole mod effect for debugging.
// - Light injection into Police infoview proven by adding ValueBinding reading in same panel header.
// Chosen over pure Police-tab injection because: standalone isolates debug, light injection keeps discovery where player already looks.
// Requires no extra bridging mod; optional Extended InfoViews just reuses same bindings.

const register: ModRegistrar = (moduleRegistry) => {
    // Standalone panel under InfoViews / CityInfo (visible always when mod active)
    moduleRegistry.append('CityInfo', RealisticCrimePanel);
    // Light injection: also append to Police infoview if that extension point exists - falls back to CityInfo
    moduleRegistry.extend('PoliceInfoview', RealisticCrimePanel);
}

export default register;