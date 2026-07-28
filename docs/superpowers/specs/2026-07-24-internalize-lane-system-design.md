# Internalize Lane System design

## Goal

Present and package TLE Extended as one mod with one release version. Keep the
Lane System code as an internal DLL so existing type and save compatibility is
not disturbed.

## Current problem

Both `C2VM.TrafficLightsEnhancement.dll` and
`C2VM.CommonLibraries.LaneSystem.dll` implement `Game.Modding.IMod`. Skyve
scans the DLLs in a local mod folder and reports the first mod implementation
it finds, which causes it to display Lane System version `0.0.17.0` instead of
the TLEE version.

Lane System's mod entry point performs only one required startup action: it
disables `Game.Net.C2VMPatchedLaneSystem`. The rest of the assembly is library
code used by TLEE.

## Design

- Remove Lane System's separate `IMod` entry point.
- Perform its required system-disable action from TLEE's existing `Mod.OnLoad`.
- Keep `C2VM.CommonLibraries.LaneSystem.dll`, its assembly name, namespaces,
  component types, and serialization unchanged.
- Keep the DLL beside the main TLEE DLL as an internal dependency.
- Package both DLLs in the single `C2VM.TrafficLightsEnhancement` folder.
- Remove the Lane System version row from TLEE's settings and localization.
- Treat the TLEE project and UI manifest versions as the only release version.
  Lane System's assembly version remains internal compiler metadata and is not
  independently released or displayed.
- Leave Paradox dependency `74417` unchanged because it is unrelated to Lane
  System.

## Compatibility

This change does not rename or move any saved component type and does not
change any serialized field, payload version, or migration. Existing TLE and
TLEE saves should therefore see the same Lane System assembly and types as
before.

## Verification

- Add a regression check proving that the built package contains only one
  `IMod` implementation and that it is TLEE.
- Verify the build output and installed folder contain both required managed
  DLLs.
- Run the pure logic, ECS, serialization, and UI test suites.
- Run a Release build with the game closed and confirm the installed TLEE DLL
  reports the intended semantic version.
- Refresh Skyve and confirm it reports TLEE's version.
- Start the game and confirm TLEE loads, Lane System is not listed as a
  separate mod, and existing lane-direction behavior still works.

## Out of scope

- Merging Lane System types into the main TLEE assembly.
- Renaming Lane System namespaces, types, or assembly identifiers.
- Changing save data or migration versions.
- Publishing a public Paradox Mods release.
