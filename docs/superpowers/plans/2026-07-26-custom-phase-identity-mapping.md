# Custom phase identity mapping implementation plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Synchronize configured custom-phase traffic-group members by phase number instead of inferred physical movement overlap.

**Architecture:** Add a validated identity-map constructor to the pure traffic-group policy. `TrafficGroupSystem` will use it for the leader and when both leader and member use custom phases, while retaining physical mapping for other pattern combinations.

**Tech Stack:** C# 12, Unity Entities, xUnit.

## Global Constraints

- Preserve local movement masks; never copy, clear, or translate them.
- Reject empty or invalid local phases.
- Preserve physical mapping for non-custom followers.
- Do not change serialized component layouts.
- Do not build or install the mod while Cities: Skylines II is running.
- Preserve the pre-existing `CommonLibraries` working-tree modification.

---

### Task 1: Add validated identity mapping

**Files:**
- Modify: `TrafficLightsEnhancement.Tests/TrafficGroups/TrafficGroupPhaseMapTests.cs`
- Modify: `TrafficLightsEnhancement.Logic/TrafficGroups/TrafficGroupPhaseMap.cs`

**Interfaces:**
- Produces: `TrafficGroupMovementMappingPolicy.TryBuildIdentity(...)`.

- [ ] **Step 1: Write failing pure-logic tests**

Add tests proving identity mapping accepts non-overlapping and duplicate
physical signatures while preserving `1 -> 1` and `2 -> 2`, and rejects an
empty phase or a member with fewer phases.

- [ ] **Step 2: Run the focused tests and verify RED**

```powershell
dotnet test TrafficLightsEnhancement.Tests/TrafficLightsEnhancement.Tests.csproj -p:UseSharedCompilation=false -m:1 --filter TrafficGroupPhaseMapTests
```

Expected: compilation fails because `TryBuildIdentity(...)` does not exist.

- [ ] **Step 3: Implement the minimal identity-map policy**

Validate both signature lists using the existing sequence and approach checks,
reject a shorter member list, create the one-based identity phase array, and
call `TrafficGroupPhaseMap.TryCreate(...)`.

- [ ] **Step 4: Run the focused tests and verify GREEN**

Run the command from Step 2. Expected: all `TrafficGroupPhaseMapTests` pass.

---

### Task 2: Select mapping policy by runtime pattern

**Files:**
- Modify: `TrafficLightsEnhancement.Ecs.Tests/TrafficGroupSystemSourceTests.cs`
- Modify: `TrafficLightsEnhancement/Systems/TrafficGroupSystem.cs`
- Modify: `docs/traffic-groups.md`

**Interfaces:**
- Consumes: `TrafficGroupMovementMappingPolicy.TryBuildIdentity(...)`.
- Produces: pattern-aware runtime mapping selection.

- [ ] **Step 1: Write a failing ECS/source regression test**

Assert that `RefreshMovementMappings(...)` uses identity mapping when the
member is the leader or both sides use `CustomPhase`, and still contains the
physical `TryBuild(...)` fallback.

- [ ] **Step 2: Run the focused ECS test and verify RED**

```powershell
dotnet test TrafficLightsEnhancement.Ecs.Tests/TrafficLightsEnhancement.Ecs.Tests.csproj -p:LangVersion=latest -p:UseSharedCompilation=false -m:1 --filter TrafficGroupSystemSourceTests
```

Expected: the new source assertion fails because runtime selection is absent.

- [ ] **Step 3: Implement pattern-aware selection**

Read each entity's `CustomTrafficLights.GetPatternOnly()`. Use identity mapping
for the leader and for custom-to-custom members; otherwise call the existing
physical mapper. Keep existing failure logging and component removal.

- [ ] **Step 4: Update traffic-group documentation**

Document that custom phases use validated phase-number identity while
automatically derived patterns use physical movement matching.

- [ ] **Step 5: Run focused and complete safe verification**

Run the focused pure and ECS suites, then all pure, ECS, serialization, and UI
tests plus `git diff --check`. Do not run the UI production build or Release
solution build while the game is running because those deploy installed files.

- [ ] **Step 6: Commit the implementation**

```powershell
git add -- TrafficLightsEnhancement.Logic/TrafficGroups/TrafficGroupPhaseMap.cs TrafficLightsEnhancement.Tests/TrafficGroups/TrafficGroupPhaseMapTests.cs TrafficLightsEnhancement/Systems/TrafficGroupSystem.cs TrafficLightsEnhancement.Ecs.Tests/TrafficGroupSystemSourceTests.cs docs/traffic-groups.md
git commit -m "fix: map custom group phases by number"
```

- [ ] **Step 7: Build and install after the game closes**

Confirm `Cities2` is not running, then run the Release solution build and
verify the installed postprocessed assembly contains the identity-mapping
runtime path before the targeted diverging-diamond playtest.
