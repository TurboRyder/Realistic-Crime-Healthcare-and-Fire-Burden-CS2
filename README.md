# Realistic-Crime-Healthcare-and-Fire-Burden-CS2
Cities Skylines 2 - Crime that scales with your city: more criminals, fuller jails and prisons, busier hospitals and fires. Balanced, Medium and Full presets plus tuning sliders. Requires 1.6.0f1. https://mods.paradoxplaza.com/mods/159091/Windows

Realistic Crime, Healthcare and Fire Burden - Cities: Skylines II (requires game version 1.6.0f1)

Crime stops being a solved problem after two police stations. This mod makes crime, sickness and fire scale with how your city is actually doing - unemployment, welfare coverage, policing capacity - and makes the police, prisons, hospitals and fire department feel it.

**Systems the mod targets (all vanilla systems, tuned - nothing replaced):**

*   Crime accumulation and tolerance (PoliceConfigurationData): how fast crime pressure builds and how much converts into real attempts.
*   Crime occurrence and recurrence (CrimeData): how often citizens offend and reoffend.
*   Catch window (alarm delay, crime duration): how fast police are notified and how long each crime lasts.
*   Sentencing (jail/prison time ranges): how long cells stay occupied.
*   Welfare recurrence factor: how well welfare coverage stops reoffending (the carrot).
*   Citizen happiness crime parameters: how strongly crime reduces well-being.
*   Fire outbreak and spread (FireData): real vanilla fires, more of them.
*   Sick-wave medical events: civilians needing ambulances and beds (never criminals - arrests are never diverted to hospital).

**How it works:** Every value is rewritten as an absolute number from verified 1.6.0f1 baselines each run, with sanity bands and drift checks - if the game rebuilds data, the mod re-applies cleanly instead of stacking. A circuit breaker pauses all difficulty writes if criminals ever exceed the configured share of population. Balanced restores vanilla values, so it plays as vanilla with reporting. No custom prefabs, no save edits, no hard dependencies.

**What the player experiences past vanilla:**

*   Criminals at large that grow with neglect: high unemployment breeds first offenders, low welfare coverage breeds repeat offenders.
*   Arrests that fill real jail cells, sentences that fill real prisons via vanilla transport, and a prison system that pressures back.
*   Faster alarms and longer crimes on higher presets, so attending patrols catch criminals instead of watching them walk away.
*   Hospitals loaded by sick waves and fire injuries, with surge warnings and per-report wave status.
*   More building fires pressuring fire coverage, scaled per preset.
*   Escapes that deteriorate high-crime areas over time, hitting land value.
*   City Crime Bureau reports in the Chirper (requires Custom Chirps, otherwise silent): breakdowns, hotspot links, jail/prison occupancy, outcome deltas, and capacity warnings when police are overstretched, jails fill, or prison vans back up.

**Presets and tuning:**

*   Balanced: vanilla rates with all response systems active.
*   Medium: about 70% of Full.
*   Full (default): the complete challenge.
*   Tuning sliders (all live, all scale the active preset): crime volume, police response, sick-wave intensity, fire frequency, unhappiness impact, plus the circuit-breaker threshold.

Please help me make this better by flagging issues or expanding it further!
TurboRyder 
