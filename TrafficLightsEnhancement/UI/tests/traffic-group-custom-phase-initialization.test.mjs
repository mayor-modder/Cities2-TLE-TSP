import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";
import test from "node:test";

const source = (path) =>
  readFile(new URL(`../${path}`, import.meta.url), "utf8");
const repoSource = (path) =>
  readFile(new URL(`../../${path}`, import.meta.url), "utf8");

test("traffic groups offer timing matching instead of movement copying", async () => {
  const component = await source(
    "src/mods/components/traffic-groups/main-panel/IndexComponent/index.tsx");
  const bindings = await source("src/bindings.ts");

  assert.doesNotMatch(component, /CopyPhasesToAllMembers/);
  assert.doesNotMatch(bindings, /CallCopyPhasesTo(AllMembers|Junction)/);
  assert.match(component, /callMatchPhaseDurationsToLeader/);
  assert.match(bindings, /CallMatchPhaseDurationsToLeader/);
});

test("incomplete lockstep followers are marked for phase setup", async () => {
  const component = await source(
    "src/mods/components/traffic-groups/main-panel/IndexComponent/index.tsx");
  const types = await source("src/mods/general.ts");
  const locale = JSON.parse(await repoSource("Locale.json"));

  assert.match(types, /phaseSetupComplete:\s*boolean/);
  assert.match(component, /member\.phaseSetupComplete/);
  assert.equal(
    locale["UI.LABEL[C2VM.TrafficLightsEnhancement.NeedsPhaseSetup]"],
    "needs phase setup");
});
