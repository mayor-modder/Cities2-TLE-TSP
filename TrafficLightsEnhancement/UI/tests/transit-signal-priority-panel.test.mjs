import assert from "node:assert/strict";
import { readdir, readFile } from "node:fs/promises";
import test from "node:test";

const source = (path) => readFile(new URL(`../${path}`, import.meta.url), "utf8");
const repoSource = (path) => readFile(new URL(`../../${path}`, import.meta.url), "utf8");

test("main panel data exposes transit signal priority tram source state", async () => {
  const general = await source("src/mods/general.ts");

  assert.match(general, /transitSignalPriority\?\s*:\s*\{/);
  assert.match(general, /tram:\s*\{/);
  assert.match(general, /isVisible:\s*boolean/);
  assert.match(general, /isEnabled:\s*boolean/);
  assert.match(general, /isEditable:\s*boolean/);
  assert.match(general, /statusLabel\?:\s*string/);
  assert.match(general, /diagnostics\?:\s*\{/);
  assert.match(general, /summary\?:\s*\{\s*label:\s*string,\s*value:\s*string\s*\}/);
  assert.match(general, /events\?:\s*Array<\{\s*sequence:\s*number,\s*label:\s*string,\s*value:\s*string\s*\}>/);
  assert.match(general, /rows:\s*Array<\{\s*label:\s*string,\s*value:\s*string\s*\}>/);
});

test("main panel data exposes transit signal priority bus source state", async () => {
  const general = await source("src/mods/general.ts");

  assert.match(general, /transitSignalPriority\?\s*:\s*\{/);
  assert.match(general, /bus:\s*\{/);
  assert.match(general, /isVisible:\s*boolean/);
  assert.match(general, /isEnabled:\s*boolean/);
  assert.match(general, /isEditable:\s*boolean/);
  assert.match(general, /statusLabel\?:\s*string/);
});

test("bindings exposes the transit signal priority tram toggle trigger", async () => {
  const bindings = await source("src/bindings.ts");

  assert.match(bindings, /toggleTransitSignalPriorityForTrams\s*=\s*triggers\.create<\[boolean\]>\("ToggleTransitSignalPriorityForTrams"\)/);
});

test("bindings exposes the transit signal priority bus toggle trigger", async () => {
  const bindings = await source("src/bindings.ts");
  const uiBindings = await repoSource("Systems/UI/UISystem.UIBIndings.cs");

  assert.match(bindings, /toggleTransitSignalPriorityForBuses\s*=\s*triggers\.create<\[boolean\]>\("ToggleTransitSignalPriorityForBuses"\)/);
  assert.match(uiBindings, /CreateTrigger<bool>\("ToggleTransitSignalPriorityForBuses",\s*ToggleTransitSignalPriorityForBuses\)/);
});

test("migration issue UI derives boolean state from affected entities", async () => {
  const bindings = await source("src/bindings.ts");
  const uiBindings = await repoSource("Systems/UI/UISystem.UIBIndings.cs");
  const content = await source("src/mods/components/main-panel/content.tsx");

  assert.match(bindings, /affectedEntities\s*=\s*new OneWayBinding<any\[\]>\("GetAffectedEntities",\s*\[\]\)/);
  assert.match(content, /const hasMigrationIssues\s*=\s*migrationEntities\s*&&\s*migrationEntities\.length\s*>\s*0/);
  assert.doesNotMatch(bindings, /hasMigrationIssues\s*=\s*new OneWayBinding/);
  assert.doesNotMatch(uiBindings, /HasMigrationIssues/);
  assert.doesNotMatch(uiBindings, /HasLoadingErrors/);
});

test("main panel renders tram and bus controls under one transit signal priority section", async () => {
  const content = await source("src/mods/components/main-panel/content.tsx");
  const panelStart = content.indexOf("TransitSignalPriority");

  assert.notEqual(panelStart, -1);
  const panelEnd = content.indexOf("{mainData.hasLaneDirectionTool", panelStart);
  const panelSource = panelEnd === -1 ? content.slice(panelStart) : content.slice(panelStart, panelEnd);

  assert.match(panelSource, /TransitSignalPriority/);
  assert.match(panelSource, /EnableTransitPriorityForTrams/);
  assert.match(panelSource, /EnableTransitPriorityForBuses/);
  assert.match(panelSource, /toggleTransitSignalPriorityForBuses/);
  assert.match(panelSource, /TransitSignalPriorityDiagnostics/);
  assert.doesNotMatch(panelSource, /title="TransitPriorityForBuses"/);
  assert.doesNotMatch(panelSource, /source/i);
  assert.doesNotMatch(panelSource, /public[-\s]?car|publicCar/i);
});

test("bus source row is visible independently from tram source row", async () => {
  const content = await source("src/mods/components/main-panel/content.tsx");
  const tramVisible = "mainData.transitSignalPriority?.tram.isVisible";
  const busVisible = "mainData.transitSignalPriority?.bus.isVisible";
  const tramVisibleIndex = content.indexOf(tramVisible);
  const busVisibleIndex = content.indexOf(busVisible);

  assert.notEqual(tramVisibleIndex, -1);
  assert.notEqual(busVisibleIndex, -1);
  assert.ok(busVisibleIndex > tramVisibleIndex);

  const betweenVisibilityChecks = content.slice(tramVisibleIndex, busVisibleIndex);
  const nestedFragments = betweenVisibilityChecks.split("<>").length - 1
    - (betweenVisibilityChecks.split("</>").length - 1);
  assert.equal(nestedFragments, 0);
});

test("backend exposes separate tram and bus transit priority controls", async () => {
  const uiBindings = await repoSource("Systems/UI/UISystem.UIBIndings.cs");
  const toggleStart = uiBindings.indexOf("private void ToggleTransitSignalPrioritySource");
  const toggleEnd = uiBindings.indexOf("protected void SetCustomPhase", toggleStart);
  const toggleSource = uiBindings.slice(toggleStart, toggleEnd);

  assert.match(uiBindings, /transitSignalPriority\s*=\s*new/);
  assert.match(uiBindings, /tram\s*=\s*new/);
  assert.match(uiBindings, /bus\s*=\s*new/);
  assert.match(uiBindings, /protected void ToggleTransitSignalPriorityForTrams\(bool enabled\)/);
  assert.match(uiBindings, /protected void ToggleTransitSignalPriorityForBuses\(bool enabled\)/);
  assert.match(uiBindings, /tramStatusLabel\s*=\s*isTrafficGroupFollower\s*\?\s*"TramTransitPriorityFollowerUnavailable"/);
  assert.match(uiBindings, /busStatusLabel\s*=\s*isTrafficGroupFollower\s*\?\s*"BusTransitPriorityFollowerUnavailable"/);
  assert.match(uiBindings, /settings\.m_AllowTrackRequests\s*=\s*enabled/);
  assert.match(uiBindings, /settings\.m_AllowPublicCarRequests\s*=\s*enabled/);
  assert.match(uiBindings, /settings\.m_Enabled\s*=\s*settings\.m_AllowTrackRequests\s*\|\|\s*settings\.m_AllowPublicCarRequests/);
  assert.match(toggleSource, /hasExistingTransitSignalPrioritySettings/);
  assert.match(toggleSource, /settings\.m_AllowTrackRequests\s*=\s*false/);
  assert.match(toggleSource, /settings\.m_AllowPublicCarRequests\s*=\s*false/);
});

test("transit signal priority has concise English base labels", async () => {
  const locale = JSON.parse(await repoSource("Locale.json"));

  assert.equal(locale["UI.LABEL[C2VM.TrafficLightsEnhancement.TransitSignalPriority]"], "Transit Signal Priority");
  assert.equal(locale["UI.LABEL[C2VM.TrafficLightsEnhancement.EnableTransitPriorityForTrams]"], "Enable for trams");
  assert.equal(locale["UI.LABEL[C2VM.TrafficLightsEnhancement.EnableTransitPriorityForBuses]"], "Enable for buses");
  assert.equal(locale["UI.LABEL[C2VM.TrafficLightsEnhancement.TransitSignalPriorityDiagnostics]"], "Diagnostics");
  assert.equal(locale["UI.LABEL[C2VM.TrafficLightsEnhancement.BusTransitPriorityFollowerUnavailable]"], "Transit Signal Priority for buses is controlled by the group leader");
});

test("transit signal priority diagnostics are gated by a mod option", async () => {
  const settings = await repoSource("Settings.cs");
  const uiBindings = await repoSource("Systems/UI/UISystem.UIBIndings.cs");
  const uiSystem = await repoSource("Systems/UI/UISystem.cs");
  const locale = await repoSource("Locale.json");
  const content = await source("src/mods/components/main-panel/content.tsx");

  assert.match(settings, /public\s+bool\s+m_ShowTransitSignalPriorityDiagnostics\s*\{\s*get;\s*set;\s*\}/);
  assert.match(settings, /m_ShowTransitSignalPriorityDiagnostics\s*=\s*false/);
  assert.match(uiBindings, /m_ShowTransitSignalPriorityDiagnostics\s*\?\s*GetTransitSignalPriorityDiagnostics\(m_SelectedEntity,\s*tspSettings\)/);
  assert.match(uiBindings, /diagnostics\s*=\s*tspDiagnostics/);
  assert.match(uiSystem, /ShouldRefreshMainPanelForDiagnostics\(\)/);
  assert.match(uiSystem, /m_MainPanelState\s*==\s*MainPanelState\.Main/);
  assert.match(content, /const\s+transitSignalPriorityDiagnostics\s*=\s*mainData\.transitSignalPriority\?\.diagnostics/);
  assert.match(content, /transitSignalPriorityDiagnostics\.summary/);
  assert.match(content, /transitSignalPriorityDiagnostics\.events/);
  assert.match(locale, /Show Transit Signal Priority Diagnostics/);
  assert.match(locale, /TSPDiagnosticsRequest/);
  assert.match(locale, /TSPDiagnosticsCurrentGroup/);
  assert.match(locale, /TSPDiagnosticsCurveApproach/);
  assert.match(locale, /TSPDiagnosticsDecision/);
});

test("backend provides transit signal priority summary and event history", async () => {
  const uiBindings = await repoSource("Systems/UI/UISystem.UIBIndings.cs");
  const uiSystem = await repoSource("Systems/UI/UISystem.cs");
  const locale = await repoSource("Locale.json");

  assert.match(uiBindings, /GetTspDiagnosticsSummary/);
  assert.match(uiBindings, /GetTspDiagnosticsEvents/);
  assert.match(uiBindings, /ShouldRecordTspDiagnosticsEvent/);
  assert.match(uiBindings, /RecordTspDiagnosticsEvent/);
  assert.match(uiBindings, /summary\s*=\s*GetTspDiagnosticsSummary/);
  assert.match(uiBindings, /events\s*=\s*GetTspDiagnosticsEvents/);
  assert.match(uiSystem, /m_TspDiagnosticsEvents/);
  assert.match(locale, /TSPDiagnosticsSummary/);
  assert.match(locale, /TSPDiagnosticsEvents/);
});

test("backend writes selected transit signal priority diagnostics to a trace file", async () => {
  const uiBindings = await repoSource("Systems/UI/UISystem.UIBIndings.cs");

  assert.match(uiBindings, /TspDiagnosticsTraceFileName/);
  assert.match(uiBindings, /C2VM\.TrafficLightsEnhancement\.TspDiagnostics\.jsonl/);
  assert.match(uiBindings, /WriteTspDiagnosticsTraceEvent/);
  assert.match(uiBindings, /Application\.persistentDataPath/);
  assert.match(uiBindings, /TspDiagnosticsTraceFileLock/);
  assert.match(uiBindings, /RotateTspDiagnosticsTraceFileIfNeeded/);
  assert.match(uiBindings, /TspDiagnosticsTraceMaxRotatedFiles/);
  assert.match(uiBindings, /PruneTspDiagnosticsTraceFiles/);
  assert.match(uiBindings, /FileMode\.Append/);
});

test("backend trace writes follow selected diagnostics event filtering", async () => {
  const uiBindings = await repoSource("Systems/UI/UISystem.UIBIndings.cs");
  const eventsStart = uiBindings.indexOf("private ArrayList GetTspDiagnosticsEvents");
  const eventsEnd = uiBindings.indexOf("private void PruneTspDiagnosticsEvents", eventsStart);
  const eventsSource = uiBindings.slice(eventsStart, eventsEnd);

  assert.notEqual(eventsStart, -1);
  assert.notEqual(eventsEnd, -1);
  assert.match(eventsSource, /bool\s+shouldRecordEvent\s*=\s*signatureChanged\s*&&\s*ShouldRecordTspDiagnosticsEvent\(history,\s*hasRuntimeDebug\s*\|\|\s*hasBusApproachDebug\s*\|\|\s*hasDecisionTrace\)/);
  assert.match(eventsSource, /if\s*\(\s*signatureChanged\s*\)/);
  assert.match(eventsSource, /if\s*\(\s*shouldRecordEvent\s*\)/);
  assert.ok(eventsSource.indexOf("bool shouldRecordEvent") < eventsSource.indexOf("WriteTspDiagnosticsTraceEvent"));
  assert.ok(eventsSource.indexOf("bool shouldRecordEvent") < eventsSource.indexOf("RecordTspDiagnosticsEvent"));
});

test("static locale provides descriptions for visible mod options", async () => {
  const locale = JSON.parse(await repoSource("Locale.json"));
  const optionPrefix =
    "Options.OPTION[C2VM.TrafficLightsEnhancement.C2VM.TrafficLightsEnhancement.Mod.Settings.";
  const descriptionPrefix =
    "Options.OPTION_DESCRIPTION[C2VM.TrafficLightsEnhancement.C2VM.TrafficLightsEnhancement.Mod.Settings.";
  const visibleOptions = [
    "m_LocaleOption",
    "m_CompatibilityModeOption",
    "m_DefaultSplitPhasing",
    "m_DefaultAlwaysGreenKerbsideTurn",
    "m_DefaultExclusivePedestrian",
    "m_ShowTransitSignalPriorityDiagnostics",
    "m_ForceNodeUpdate",
    "m_ComponentTypeToClear",
    "m_ClearSelectedComponent",
    "m_ReleaseChannel",
    "m_TleVersion",
    "m_LaneSystemVersion",
    "m_SuppressCanaryWarning",
    "m_MainPanelToggleKeyboardBinding",
    "m_MultiSelectEntityKeyboardBinding",
    "m_ResetBindings",
  ];

  for (const option of visibleOptions) {
    const optionKey = `${optionPrefix}${option}]`;
    const descriptionKey = `${descriptionPrefix}${option}]`;

    assert.equal(typeof locale[optionKey], "string", `${option} needs a label`);
    assert.equal(typeof locale[descriptionKey], "string", `${option} needs a description`);
    assert.notEqual(locale[descriptionKey].trim(), "", `${option} description cannot be empty`);
    assert.doesNotMatch(locale[descriptionKey], /^Options\.OPTION_DESCRIPTION/, `${option} description cannot be a raw localization key`);
  }
});

test("UI does not carry unused TypeScript localization fallback dictionaries", async () => {
  const sourceFiles = await readdir(new URL("../src/mods", import.meta.url), { recursive: true });
  const localizationFallbackFiles = sourceFiles.filter((file) => file === "localisations" || file.startsWith("localisations/") || file.startsWith("localisations\\"));

  assert.deepEqual(localizationFallbackFiles, []);
});

test("backend localization uses Locale.json instead of legacy resource dictionaries", async () => {
  const mod = await repoSource("Mod.cs");
  const resourceFiles = await readdir(new URL("../../Resources", import.meta.url), { recursive: true });
  const utilsFiles = await readdir(new URL("../../Utils", import.meta.url), { recursive: true });
  const legacyResourceFiles = resourceFiles.filter(
    (file) => file === "Localisations" || file.startsWith("Localisations/") || file.startsWith("Localisations\\"));
  const legacyUtils = utilsFiles.filter(
    (file) => file === "LocalisationUtils.cs" || file.endsWith("/LocalisationUtils.cs") || file.endsWith("\\LocalisationUtils.cs"));

  assert.match(mod, /new LocaleHelper\(modName \+ "\.Locale\.json"\)\.GetAvailableLanguages\(\)/);
  assert.deepEqual(legacyResourceFiles, []);
  assert.deepEqual(legacyUtils, []);
});

test("custom phase vehicle weights expose bicycle weight control", async () => {
  const subPanel = await source("src/mods/components/custom-phase-tool/main-panel/sub-panel.tsx");
  const locale = JSON.parse(await repoSource("Locale.json"));

  assert.match(subPanel, /keyName="BicycleWeight"/);
  assert.match(subPanel, /label="BicycleWeight"/);
  assert.match(subPanel, /value=\{data\.bicycleWeight\}/);
  assert.match(subPanel, /Tooltip\.LABEL\[C2VM\.TrafficLightsEnhancement\.BicycleWeight\]/);
  assert.equal(locale["UI.LABEL[C2VM.TrafficLightsEnhancement.BicycleWeight]"], "Bicycle Weight");
  assert.equal(
    typeof locale["Tooltip.LABEL[C2VM.TrafficLightsEnhancement.BicycleWeight]"],
    "string");
});

test("backend toggle removes transit signal priority settings when all sources are disabled", async () => {
  const uiBindings = await repoSource("Systems/UI/UISystem.UIBIndings.cs");
  const toggleStart = uiBindings.indexOf("protected void ToggleTransitSignalPriorityForTrams(bool enabled)");

  assert.notEqual(toggleStart, -1);
  const toggleEnd = uiBindings.indexOf("protected void CallMainPanelUpdatePosition", toggleStart);
  const toggleSource = toggleEnd === -1 ? uiBindings.slice(toggleStart) : uiBindings.slice(toggleStart, toggleEnd);

  assert.match(toggleSource, /ToggleTransitSignalPrioritySource\(enabled,\s*allowTrackRequests:\s*true\)/);
  assert.match(toggleSource, /ToggleTransitSignalPrioritySource\(enabled,\s*allowTrackRequests:\s*false\)/);
  assert.match(toggleSource, /settings\.m_Enabled\s*=\s*settings\.m_AllowTrackRequests\s*\|\|\s*settings\.m_AllowPublicCarRequests/);
  assert.match(toggleSource, /if\s*\(!settings\.m_Enabled\)/);
  assert.match(toggleSource, /EntityManager\.RemoveComponent<TransitSignalPrioritySettings>\(m_SelectedEntity\)/);
});

test("tool removal clears transit signal priority runtime components", async () => {
  const toolSystem = await repoSource("Systems/Tool/ToolSystem.cs");
  const helperStart = toolSystem.indexOf("private void RemoveTransitSignalPriorityComponents(Entity entity)");

  assert.notEqual(helperStart, -1);
  const helperEnd = toolSystem.indexOf("private void", helperStart + 1);
  const helperSource = helperEnd === -1 ? toolSystem.slice(helperStart) : toolSystem.slice(helperStart, helperEnd);

  assert.match(helperSource, /RemoveComponent<TransitSignalPrioritySettings>/);
  assert.match(helperSource, /RemoveComponent<TransitSignalPriorityRequest>/);
  assert.match(helperSource, /RemoveComponent<TransitSignalPriorityRuntimeDebugInfo>/);
  assert.match(helperSource, /RemoveComponent<TransitSignalPriorityBusApproachDebugInfo>/);
  assert.match(helperSource, /RemoveComponent<TransitSignalPriorityDecisionTrace>/);
  assert.match(helperSource, /RemoveComponent<TransitSignalPriorityPedestrianFairnessState>/);
  assert.match(helperSource, /RemoveComponent<TransitSignalPriorityVehicleFairnessState>/);

  const removalStart = toolSystem.indexOf("EntityManager.RemoveComponent<CustomTrafficLights>(m_RaycastResult)");
  const removalEnd = toolSystem.indexOf("EntityManager.AddComponentData(m_RaycastResult", removalStart);
  const removalSource = removalEnd === -1 ? toolSystem.slice(removalStart) : toolSystem.slice(removalStart, removalEnd);

  assert.match(removalSource, /RemoveTransitSignalPriorityComponents\(m_RaycastResult\)/);
});

test("transit signal priority settings reserve public car priority without persisting group propagation", async () => {
  const settings = await repoSource("Components/TransitSignalPrioritySettings.cs");
  const normalizeStart = settings.indexOf("public void Normalize()");
  const serializeStart = settings.indexOf("public void Serialize");
  const deserializeStart = settings.indexOf("public void Deserialize");

  assert.notEqual(normalizeStart, -1);
  assert.notEqual(serializeStart, -1);
  assert.notEqual(deserializeStart, -1);

  const normalizeSource = settings.slice(normalizeStart, serializeStart);
  const serializeSource = settings.slice(serializeStart, deserializeStart);
  const deserializeSource = settings.slice(deserializeStart);

  assert.doesNotMatch(normalizeSource, /m_AllowPublicCarRequests\s*=/);
  assert.match(serializeSource, /writer\.Write\(2\)/);
  assert.match(serializeSource, /writer\.Write\(m_AllowPublicCarRequests\)/);
  assert.doesNotMatch(serializeSource, /m_AllowGroupPropagation/);
  assert.match(deserializeSource, /if\s*\(version\s*==\s*1\)/);
  assert.doesNotMatch(deserializeSource, /reader\.Read\(out m_AllowGroupPropagation\)/);
});

test("backend exposes bus approach index details", async () => {
  const patchedSystem = await repoSource("Systems/TrafficLightSystems/Simulation/PatchedTrafficLightSystem.cs");
  const extraTypeHandle = await repoSource("Systems/TrafficLightSystems/Simulation/ExtraTypeHandle.cs");
  const busIndex = await repoSource("Systems/TrafficLightSystems/Simulation/BusApproachIndex.cs");
  const runtime = await repoSource("Systems/TrafficLightSystems/Simulation/TransitSignalPriorityRuntime.cs");
  const components = await repoSource("Components/TransitSignalPriorityBusApproachDebugInfo.cs");
  const uiBindings = await repoSource("Systems/UI/UISystem.UIBIndings.cs");
  const locale = JSON.parse(await repoSource("Locale.json"));
  const busBuildCondition = patchedSystem.match(/var busApproachIndex = ([\s\S]*?)\? BusApproachIndex\.Build/);

  assert.match(patchedSystem, /m_BusTransitQuery/);
  assert.match(patchedSystem, /ComponentType\.ReadOnly<PassengerTransport>\(\)/);
  assert.match(patchedSystem, /BusApproachIndex\.Build/);
  assert.match(patchedSystem, /m_ShowTransitSignalPriorityDiagnostics/);
  assert.ok(busBuildCondition, "bus approach index should have an explicit build condition");
  assert.match(busBuildCondition[1], /shouldBuildBusApproachIndex/);
  assert.doesNotMatch(busBuildCondition[1], /shouldBuildTramApproachIndex/);
  assert.match(patchedSystem, /m_BusApproachIndex\s*=/);
  assert.match(patchedSystem, /m_BusApproachIndexLaneCount\s*=/);
  const busDebugStart = patchedSystem.indexOf("if (m_TransitSignalPriorityDiagnosticsEnabled");
  const busDebugEnd = patchedSystem.indexOf("if (hasActiveBusApproachDebugInfo)", busDebugStart);
  const busDebugGate = patchedSystem.slice(busDebugStart, busDebugEnd);

  assert.notEqual(busDebugStart, -1);
  assert.notEqual(busDebugEnd, -1);
  assert.match(busDebugGate, /BuildBusApproachDebugInfo/);
  assert.doesNotMatch(busDebugGate, /TransitSignalPrioritySettingsLookup/);
  assert.doesNotMatch(busDebugGate, /m_Enabled/);
  assert.match(extraTypeHandle, /CarCurrentLane/);
  assert.match(extraTypeHandle, /CarNavigation/);
  assert.match(extraTypeHandle, /CarNavigationLane/);
  assert.doesNotMatch(extraTypeHandle, /m_PassengerTransport/);
  assert.match(extraTypeHandle, /PublicTransportVehicleData/);
  assert.match(busIndex, /TransportType\.Bus/);
  assert.match(busIndex, /PublicOnly/);
  assert.match(busIndex, /m_ChangeLane/);
  assert.match(runtime, /BuildBusApproachDebugInfo/);
  assert.match(uiBindings, /if\s*\(\s*hasBusApproachDebug\s*&&\s*busApproachDebug\.m_BusHitCount\s*>\s*0\s*\)/);
  const summaryStart = uiBindings.indexOf("private string GetTspDiagnosticsSummaryValue");
  const summaryEnd = uiBindings.indexOf("private ArrayList GetTspDiagnosticsEvents", summaryStart);
  const summarySource = uiBindings.slice(summaryStart, summaryEnd);
  assert.notEqual(summaryStart, -1);
  assert.notEqual(summaryEnd, -1);
  assert.ok(summarySource.indexOf("if (hasBusApproachDebug && busApproachDebug.m_BusHitCount > 0)") < summarySource.indexOf("if (!settings.m_Enabled)"));
  assert.match(components, /TransitSignalPriorityBusProbeResult/);
  assert.match(uiBindings, /TransitSignalPriorityBusApproachDebugInfo/);
  assert.doesNotMatch(summarySource, /No tram request/);
  assert.match(summarySource, /No active request/);
  assert.match(uiBindings, /TSPDiagnosticsBusIndexLanes/);
  assert.match(uiBindings, /TSPDiagnosticsBusLaneType/);
  assert.match(uiBindings, /TSPDiagnosticsBusLaneChange/);
  assert.match(uiBindings, /TSPDiagnosticsBusVehicleFlags/);
  assert.equal(locale["UI.LABEL[C2VM.TrafficLightsEnhancement.TSPDiagnosticsBusIndexLanes]"], "Indexed bus lanes");
  assert.equal(locale["UI.LABEL[C2VM.TrafficLightsEnhancement.TSPDiagnosticsBusLaneType]"], "Bus lane type");
});

test("runtime can build public-car requests from bus approach samples", async () => {
  const runtime = await repoSource("Systems/TrafficLightSystems/Simulation/TransitSignalPriorityRuntime.cs");
  const helperStart = runtime.indexOf("public static bool TryBuildBusApproachRequestFromSample");
  const helperEnd = runtime.indexOf("public static bool TryResolveActiveLocalRequest", helperStart);
  const helperSource = runtime.slice(helperStart, helperEnd);
  const requestStart = runtime.indexOf("private static bool TryBuildBusApproachRequestForLane");
  const requestEnd = runtime.indexOf("private static bool TryBuildPetitionerRequestForLane", requestStart);
  const requestSource = runtime.slice(requestStart, requestEnd);

  assert.match(runtime, /TryBuildBusApproachRequestFromSample/);
  assert.match(runtime, /TryBuildBusApproachRequestForLane/);
  assert.notEqual(helperStart, -1);
  assert.notEqual(helperEnd, -1);
  assert.notEqual(requestStart, -1);
  assert.notEqual(requestEnd, -1);
  assert.match(requestSource, /TryBuildBusApproachRequestFromSample/);
  assert.match(helperSource, /isPublicCarLane:\s*true/);
  assert.match(helperSource, /TspSource\.PublicCar/);
  assert.match(helperSource, /BusPrioritySuppressionPolicy\.EvaluateStopSuppression/);
  assert.match(helperSource, /BusStopRelation\.Unknown/);
  assert.match(helperSource, /HasAmbiguousBusLaneChange\(sample\)/);
});

test("bus diagnostics include request and suppression decisions", async () => {
  const components = await repoSource("Components/TransitSignalPriorityBusApproachDebugInfo.cs");
  const runtime = await repoSource("Systems/TrafficLightSystems/Simulation/TransitSignalPriorityRuntime.cs");
  const uiBindings = await repoSource("Systems/UI/UISystem.UIBIndings.cs");
  const locale = JSON.parse(await repoSource("Locale.json"));

  assert.match(components, /TransitSignalPriorityBusDecision/);
  assert.match(components, /RequestEmitted/);
  assert.match(components, /SuppressedBoarding/);
  assert.match(components, /SuppressedNearSideStop/);
  assert.match(components, /SuppressedUnknownStopRelation/);
  assert.match(components, /SuppressedAmbiguousLaneChange/);
  assert.doesNotMatch(components, /SuppressedAggressivePreemption/);
  assert.match(runtime, /m_BusDecision\s*=\s*TransitSignalPriorityBusDecision\.RequestEmitted/);
  assert.match(uiBindings, /TSPDiagnosticsBusDecision/);
  assert.match(uiBindings, /GetBusDecisionName/);
  assert.match(uiBindings, /SuppressedNearSideStop => "Suppressed: near-side stop"/);
  assert.doesNotMatch(uiBindings, /SuppressedAggressivePreemption/);
  assert.ok(locale["UI.LABEL[C2VM.TrafficLightsEnhancement.TSPDiagnosticsBusDecision]"]);
  assert.ok(locale["UI.LABEL[C2VM.TrafficLightsEnhancement.TSPDiagnosticsBusDecisionSuppressedNearSideStop]"]);
});

test("bus priority builds bus approach index without requiring diagnostics", async () => {
  const patchedSystem = await repoSource("Systems/TrafficLightSystems/Simulation/PatchedTrafficLightSystem.cs");

  assert.match(patchedSystem, /bool\s+shouldBuildBusApproachIndex\s*=/);
  assert.match(patchedSystem, /showTransitSignalPriorityDiagnostics\s*\|\|\s*HasApproachIndexEligibleTransitSignalPrioritySettings\(requirePublicCarRequests:\s*true\)/);
  assert.match(patchedSystem, /shouldBuildBusApproachIndex\s*\?\s*BusApproachIndex\.Build/);
});

test("bus diagnostics reuse runtime bus scan when priority already scanned the junction", async () => {
  const runtime = await repoSource("Systems/TrafficLightSystems/Simulation/TransitSignalPriorityRuntime.cs");
  const patchedSystem = await repoSource("Systems/TrafficLightSystems/Simulation/PatchedTrafficLightSystem.cs");
  const diagnosticsStart = patchedSystem.indexOf("if (m_TransitSignalPriorityDiagnosticsEnabled)");
  const diagnosticsEnd = patchedSystem.indexOf("if (hasActiveBusApproachDebugInfo)", diagnosticsStart);
  const diagnosticsSource = patchedSystem.slice(diagnosticsStart, diagnosticsEnd);

  assert.match(runtime, /out TransitSignalPriorityBusApproachDebugInfo reusableBusApproachDebugInfo/);
  assert.match(runtime, /out bool hasReusableBusApproachDebugInfo/);
  assert.match(patchedSystem, /m_TransitSignalPriorityDiagnosticsEnabled,\s*out var tspRequest/);
  assert.match(diagnosticsSource, /hasReusableBusApproachDebugInfo\s*\?\s*reusableBusApproachDebugInfo/);
  assert.match(diagnosticsSource, /:\s*TspRuntime\.BuildBusApproachDebugInfo/);
});

test("bus priority can select target group at normal transition without aggressive preemption", async () => {
  const patchedSystem = await repoSource("Systems/TrafficLightSystems/Simulation/PatchedTrafficLightSystem.cs");
  const getNextStart = patchedSystem.indexOf("private int GetNextSignalGroup(");
  const getNextEnd = patchedSystem.indexOf("private static bool IsExclusivePedestrianEnabled", getNextStart);
  const getNextSource = patchedSystem.slice(getNextStart, getNextEnd);

  assert.notEqual(getNextStart, -1);
  assert.notEqual(getNextEnd, -1);
  assert.match(getNextSource, /ShouldApplyTargetGroupSelection/);
  assert.match(getNextSource, /ApplySignalGroupOverride/);
  assert.doesNotMatch(getNextSource, /if\s*\(\s*!hasTspRequest\s*\|\|\s*!TspRuntime\.ShouldAggressivelyPreemptToTargetGroup/);
});

test("custom phase text fields preserve numeric regex escapes", async () => {
  const panel = await source("src/mods/components/custom-phase-tool/main-panel/sub-panel.tsx");
  const regexLiterals = [...panel.matchAll(/textFieldRegExp="([^"]+)"/g)].map((match) => match[1]);
  const regexes = regexLiterals.map((literal) => new RegExp(Function(`return "${literal}"`)()));

  for (const regex of regexes.slice(0, 5)) {
    assert.match("1.2", regex);
    assert.doesNotMatch("dxd", regex);
  }

  const smoothingRegex = regexes[5];
  assert.match("0.5", smoothingRegex);
  assert.match("1.0", smoothingRegex);
  assert.doesNotMatch("0d5", smoothingRegex);
});

test("bus and custom phase docs do not carry stale review notes", async () => {
  const busResearch = await repoSource("../docs/transit-signal-priority-bus-research.md");
  const tspArchitecture = await repoSource("../docs/tsp-architecture.md");
  const customPhaseExtraction = await repoSource("../docs/custom-phase-selection-extraction.md");
  const edgeCaseHeadings = busResearch.match(/^## Edge Cases$/gm) ?? [];

  assert.equal(edgeCaseHeadings.length, 1);
  assert.doesNotMatch(tspArchitecture, /reserved for future bus|effectively track-only|only emits `TspSource\.Track`/);
  assert.match(tspArchitecture, /Transit Signal Priority for buses/i);
  assert.match(tspArchitecture, /Diagnostic cost contract/);
  assert.match(busResearch, /runtime always passes `BusStopRelation\.Unknown`/);
  assert.match(busResearch, /#35/);
  assert.match(busResearch, /#36/);
  assert.match(busResearch, /No separate bus aggressive-preemption suppression diagnostic is exposed/);
  assert.doesNotMatch(customPhaseExtraction, /production selector reports `false`/);
  assert.match(customPhaseExtraction, /linked-phase\s+behavior remains in `CustomStateMachine`/);
});
