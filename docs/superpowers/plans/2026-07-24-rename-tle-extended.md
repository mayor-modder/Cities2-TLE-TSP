# Traffic Lights Enhancement Extended rename implementation plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Rename the player-facing local mod and release package to `Traffic Lights Enhancement Extended` without changing compatibility-sensitive identifiers.

**Architecture:** Add a spaced display name and a separate `TrafficLightsEnhancementExtended` filesystem name alongside the existing internal module ID, point C# and UI deployment at the filesystem-safe folder, rename release staging, and update visible product labels while preserving assembly, namespace, localization-key, and serialization identities.

**Tech stack:** C#/.NET Framework 4.8, MSBuild, React/webpack, JSON localization, GitHub Actions, xUnit, Node test runner.

---

## Task 1: Protect the naming boundary

- [x] Add a failing test that expects the exact publisher display name and confirms assembly/root namespace identifiers remain `C2VM.TrafficLightsEnhancement`.
- [x] Add a failing UI test that expects the exact display/deploy name while preserving the internal `id`.
- [x] Run the focused tests and confirm they fail on the old player-facing name.

## Task 2: Apply the visible and folder rename

- [x] Add `displayName` and `deployFolder` to the UI manifest and use the filesystem-safe deploy folder for webpack output while keeping the internal ID for bundle and binding names.
- [x] Override the C# local deployment directory after the toolchain calculates its default.
- [x] Update the publisher display name, settings-section name, input-map name, English version description, and other base English product-name labels.
- [x] Update release staging so the archive contains the `TrafficLightsEnhancementExtended` folder.
- [x] Run the focused tests and confirm they pass.

## Task 3: Verify and install

- [x] Run all C#, serialization, and UI tests plus the UI and non-deploying Release builds.
- [x] Confirm Cities: Skylines II is closed.
- [x] Build and install into `Mods\TrafficLightsEnhancementExtended`.
- [x] Compare installed and built DLL hashes.
- [x] Remove the obsolete `Mods\C2VM.TrafficLightsEnhancement` folder after the new install is verified.
- [x] Restart Skyve normally and confirm its refreshed data uses the new folder name, TLE Extended 1.0.3, and one mod entry point.
