import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";
import test from "node:test";

const source = (path) => readFile(new URL(`../${path}`, import.meta.url), "utf8");

test("custom phase lane renders bicycle all-signal controls", async () => {
  const lane = await source("src/mods/components/custom-phase-tool/lane.tsx");
  const bicycleStart = lane.indexOf('props.data.type == "bicycleLane"');

  assert.notEqual(bicycleStart, -1, "bicycle lanes should have an explicit render branch");

  const bicycleEnd = lane.indexOf('props.data.left != "none"', bicycleStart);
  const bicycleSource = lane.slice(bicycleStart, bicycleEnd);

  assert.match(bicycleSource, /<Bicycle\b/);
  assert.match(bicycleSource, /variant="traffic-light"/);
  assert.match(bicycleSource, /"all"/);
  assert.match(bicycleSource, /props\.data\.all/);
  assert.match(bicycleSource, /props\.onClick\(props\.index,\s*props\.data\.type,\s*"all",\s*props\.data\.all\)/);
});

test("traffic group member signal editor renders bicycle controls when bicycle lanes exist", async () => {
  const panel = await source("src/mods/components/traffic-groups/main-panel/IndexComponent/index.tsx");
  const hasBicycleIndex = panel.indexOf("const hasBicycleLanes");

  assert.notEqual(hasBicycleIndex, -1, "traffic group editor should calculate bicycle lane presence");

  const signalGroupEnd = panel.indexOf("</div>\n\t\t\t</div>", hasBicycleIndex);
  const signalGroupSource = panel.slice(hasBicycleIndex, signalGroupEnd);

  assert.match(signalGroupSource, /hasBicycleLanes\s*&&\s*\(/);
  assert.match(signalGroupSource, /m_EdgeGroupMask\.m_Bicycle\.m_GoGroupMask/);
  assert.match(signalGroupSource, /handleSignalClick\(edge,\s*"bicycle",\s*"all"\)/);
});
