# Custom State Machine No-TSP Regression Harness

This document captures the regression coverage plan for custom signal state
transitions when Transit Signal Priority (TSP) is disabled or absent.

## Purpose

TSP adds overloads to the custom signal state machine so selected intersections
can hold or override custom phases for tram requests. The no-request path must
continue to behave like the inherited TLE state machine.

The regression to prove is:

```text
new overload called with hasTspRequest=false == legacy overload
```

## Harness

`TrafficLightsEnhancement.Ecs.Tests` is a `net48` xUnit project that references
the built mod assembly and the Cities II / Unity assemblies needed by
`CustomStateMachine`, including:

- `Game.Net.TrafficLights`
- `Unity.Entities.DynamicBuffer<T>`
- `CustomTrafficLights`
- `CustomPhaseData`
- the local Cities II modding toolchain and game assemblies

The harness deliberately stays separate from the pure `TrafficLightsEnhancement.Tests`
project so the pure logic suite remains Unity-free.

The tests use a small unmanaged `DynamicBuffer<CustomPhaseData>` fixture instead
of creating a Unity `World`. That keeps the tests executable under normal
`dotnet test`; `World` allocation calls Unity runtime ECalls that are not
available outside the game process.

## Regression Oracle

For each scenario, construct equivalent `TrafficLights`,
`CustomTrafficLights`, and `DynamicBuffer<CustomPhaseData>` state, then compare
the legacy and TSP-aware overloads.

### State Update

Compare:

```csharp
CustomStateMachine.UpdateTrafficLightState(
    ref trafficLights,
    ref customTrafficLights,
    phases);
```

with:

```csharp
CustomStateMachine.UpdateTrafficLightState(
    ref trafficLights,
    ref customTrafficLights,
    phases,
    phases,
    default,
    false,
    default,
    out tspSelection);
```

Expected:

- the return value is identical
- `TrafficLights` fields are identical
- `CustomTrafficLights` fields are identical
- phase buffer fields are identical
- `tspSelection.Applied` is `false`

### Next-Group Selection

Compare:

```csharp
CustomStateMachine.GetNextSignalGroup(
    currentGroup,
    phases,
    customTrafficLights,
    out linkedWithNext);
```

with:

```csharp
CustomStateMachine.GetNextSignalGroup(
    currentGroup,
    phases,
    customTrafficLights,
    out linkedWithNext,
    out tspSelection,
    false,
    default);
```

Expected:

- selected group is identical
- `linkedWithNext` is identical
- `tspSelection.Applied` is `false`

## Scenario Matrix

The harness covers:

- empty phase buffer returns group `0`
- manual signal group overrides selection
- fixed timed sequential mode selects the next group
- fixed timed smart-selection mode preserves existing best-step and restart
  behavior
- dynamic mode selects the next phase with positive minimum duration
- dynamic mode skips zero-minimum phases without demand
- dynamic mode selects a zero-minimum phase with demand or priority
- `None`, `Extending`, and `Extended` transition to `Beginning`
- `Beginning` transitions to `Ongoing`
- `Ongoing` before minimum duration does not transition
- `Ongoing` at maximum duration transitions to `Ending`
- `EndPhasePrematurely` transitions to `Ending`
- `Ending` transitions to `Changing`
- `Changing` transitions to `Beginning`

## Source Invariant

The code currently preserves the no-TSP path through delegation:

- legacy `UpdateTrafficLightState(...)` delegates to the TSP-aware overload with
  `hasTspRequest=false`
- legacy `GetNextSignalGroup(...)` delegates to the TSP-aware overload with
  `hasTspRequest=false`
- `ApplyTspOverride(...)` returns the base group immediately when there is no
  TSP request

The harness above is still needed because the behavior depends on Unity ECS
state and mutable buffers that are not covered by the pure xUnit project.

## Commands

Run from a machine with Cities II and the local modding toolchain installed. The
test project resolves managed game assemblies from `CSII_MANAGEDPATH` when set,
then falls back to the default Steam and Xbox install paths.

```powershell
dotnet test TrafficLightsEnhancement.Ecs.Tests\TrafficLightsEnhancement.Ecs.Tests.csproj
```

After restore has run once, this is sufficient for repeat local checks:

```powershell
dotnet test TrafficLightsEnhancement.Ecs.Tests\TrafficLightsEnhancement.Ecs.Tests.csproj --no-restore
```

CI can run the harness only on a Windows runner that has access to the Cities II
managed assemblies or receives them through a private cache/artifact. Public CI
without those assemblies should skip this project and still run the pure logic,
serialization, and UI tests.

## Follow-Up Work

A separate follow-up should evaluate whether the base custom phase selection
logic can be extracted into pure DTOs without creating churn in the production
state machine.
