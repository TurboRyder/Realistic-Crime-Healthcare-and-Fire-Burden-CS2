import { bindValue, useValue } from "cs2/api";
import { Panel, PanelSection, PanelSectionRow } from "cs2/ui";

// All bindings from CrimeBreakdownUISystem.cs under "realisticCrime"
const petty$ = bindValue<number>("realisticCrime", "pettyCount");
const violent$ = bindValue<number>("realisticCrime", "violentCount");
const overdose$ = bindValue<number>("realisticCrime", "overdoseCount");
const total$ = bindValue<number>("realisticCrime", "totalCriminals");
const ratio$ = bindValue<number>("realisticCrime", "criminalPopRatio");
const arrested$ = bindValue<number>("realisticCrime", "arrested");
const sentenced$ = bindValue<number>("realisticCrime", "sentenced");
const prisoner$ = bindValue<number>("realisticCrime", "prisoner");
const jailOcc$ = bindValue<number>("realisticCrime", "jailOccupancy");
const jailCap$ = bindValue<number>("realisticCrime", "jailCapacity");
const prisonOcc$ = bindValue<number>("realisticCrime", "prisonOccupancy");
const prisonCap$ = bindValue<number>("realisticCrime", "prisonCapacity");
const jammed$ = bindValue<boolean>("realisticCrime", "transportJammed");
const difficulty$ = bindValue<number>("realisticCrime", "difficulty");
const delta$ = bindValue<number>("realisticCrime", "delta");
const halted$ = bindValue<boolean>("realisticCrime", "circuitBreakerHalted");
const tolerance$ = bindValue<number>("realisticCrime", "tolerance");
const debug$ = bindValue<string>("realisticCrime", "debugInfo");

// Driver bindings
const unemployment$ = bindValue<number>("realisticCrime", "unemploymentNorm");
const education$ = bindValue<number>("realisticCrime", "educationNorm");
const healthcare$ = bindValue<number>("realisticCrime", "healthcareCov");

export const RealisticCrimePanel = () => {
    const petty = useValue(petty$);
    const violent = useValue(violent$);
    const overdose = useValue(overdose$);
    const total = useValue(total$);
    const ratio = useValue(ratio$);
    const halted = useValue(halted$);
    const debug = useValue(debug$);

    // Light injection for Police infoview: show breakdown sparkline alongside vanilla
    // Full debug panel for whole-view + troubleshooting

    return (
        <Panel header="Realistic Crime System (1.6.0f1)">
            {halted && (
                <PanelSection>
                    <PanelSectionRow>
                        <span style={{ color: "red", fontWeight: "bold" }}>
                            Circuit breaker halted: criminals &gt;15% pop. Transport jammed - build prisons or enable fix mod 113708.
                        </span>
                    </PanelSectionRow>
                </PanelSection>
            )}
            <PanelSection header="Breakdown by Type">
                <PanelSectionRow> Petty: {petty} | Violent: {violent} | Overdose: {overdose} | Total: {total} ({(ratio * 100).toFixed(1)}% pop)</PanelSectionRow>
            </PanelSection>
            <PanelSection header="Pipeline (debug)">
                <PanelSectionRow>Arrested {useValue(arrested$)} | Sentenced {useValue(sentenced$)} | Prisoner {useValue(prisoner$)}</PanelSectionRow>
                <PanelSectionRow>Jail {useValue(jailOcc$)}/{useValue(jailCap$)} | Prison {useValue(prisonOcc$)}/{useValue(prisonCap$)} | Jammed: {useValue(jammed$) ? "yes" : "no"}</PanelSectionRow>
            </PanelSection>
            <PanelSection header="Drivers & Config">
                <PanelSectionRow>Difficulty {useValue(difficulty$).toFixed(2)} Delta {useValue(delta$).toFixed(3)} Tol {useValue(tolerance$)}</PanelSectionRow>
                <PanelSectionRow>Unemp {useValue(unemployment$).toFixed(2)} Edu {useValue(education$).toFixed(2)} Health {useValue(healthcare$)} Parks {useValue(bindValue<number>("realisticCrime","parksCov"))}</PanelSectionRow>
            </PanelSection>
            <PanelSection header="Debug">
                <PanelSectionRow>{debug}</PanelSectionRow>
            </PanelSection>
        </Panel>
    );
};
