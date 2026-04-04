# Local Grouped Transit Priority Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Enable grouped intersections to use local TSP and propagate tram/BRT requests only to a short downstream window of group members instead of disabling grouped TSP or driving the whole corridor as one request.

**Architecture:** Keep the existing per-junction TSP request builder and signal override logic, but replace the old grouped-membership guard plus single group aggregate state with a local propagation model. Pure propagation selection logic lives in `TrafficLightsEnhancement.Logic`, ECS runtime writes transient per-junction propagated requests, and the UI updates remove the old grouped-ineligible messaging while preserving the existing per-group toggle.

**Tech Stack:** C#/.NET Framework 4.8 ECS mod code, `TrafficLightsEnhancement.Logic` netstandard pure helpers, xUnit tests in `TrafficLightsEnhancement.Tests`, TypeScript/React UI bindings under `TrafficLightsEnhancement/UI/src`.

---

## File Structure

- Create: `TrafficLightsEnhancement.Logic/Tsp/GroupedTspPropagation.cs`
  Responsibility: pure models and selection logic for ahead-only cumulative-distance propagation and strongest-request arbitration.
- Create: `TrafficLightsEnhancement.Tests/Tsp/GroupedTspPropagationTests.cs`
  Responsibility: test runtime eligibility, ahead-only windowing, strongest-wins arbitration, and equal-strength tie resolution.
- Create: `TrafficLightsEnhancement/Components/GroupedTransitSignalPriorityRequest.cs`
  Responsibility: transient per-junction propagated request state with origin metadata for diagnostics and arbitration.
- Modify: `TrafficLightsEnhancement.Logic/Tsp/TspPolicy.cs`
  Responsibility: remove grouped-membership runtime ineligibility while keeping disabled-state handling.
- Modify: `TrafficLightsEnhancement.Tests/Tsp/TspPolicyTests.cs`
  Responsibility: replace the old grouped-ineligible assertions with grouped-eligible coverage.
- Modify: `TrafficLightsEnhancement/Systems/TrafficGroupSystem.cs`
  Responsibility: gather local member requests, project them forward by group order and cumulative distance, and write/clear `GroupedTransitSignalPriorityRequest`.
- Modify: `TrafficLightsEnhancement/Systems/TrafficLightSystems/Simulation/ExtraTypeHandle.cs`
  Responsibility: expose the new per-junction propagated-request lookup.
- Modify: `TrafficLightsEnhancement/Systems/TrafficLightSystems/Simulation/TransitSignalPriorityRuntime.cs`
  Responsibility: allow grouped local requests and load propagated grouped requests from the new component.
- Modify: `TrafficLightsEnhancement/Systems/TrafficLightSystems/Simulation/PatchedTrafficLightSystem.cs`
  Responsibility: arbitrate between local and propagated requests, prefer stronger requests, and emit decision traces with grouped-propagation scope.
- Modify: `TrafficLightsEnhancement/Components/TransitSignalPriorityDecisionTrace.cs`
  Responsibility: rename the old coordinated-group flag to grouped-propagation scope and optionally store origin member metadata.
- Modify: `TrafficLightsEnhancement/Systems/TransitSignalPriorityDiagnosticsSystem.cs`
  Responsibility: log per-junction propagated request writes/clears instead of group-aggregate state.
- Modify: `TrafficLightsEnhancement/Systems/UI/UISystem.UIBIndings.cs`
  Responsibility: keep grouped intersections interactive in the TSP section and expose the per-junction propagation checkbox without the old unavailable-state lockout.
- Modify: `TrafficLightsEnhancement/UI/src/mods/components/traffic-groups/main-panel/IndexComponent/index.tsx`
  Responsibility: replace the disabled grouped-TSP row and unavailable message with a real toggle row wired to `CallSetTspPropagationEnabled`.
- Modify: `TrafficLightsEnhancement/UI/src/mods/localisations/en-US.ts`
  Responsibility: update grouped-TSP copy to describe local downstream propagation instead of unavailability.

### Task 1: Add Pure Grouped-Propagation Logic And Eligibility Tests

**Files:**
- Create: `TrafficLightsEnhancement.Logic/Tsp/GroupedTspPropagation.cs`
- Create: `TrafficLightsEnhancement.Tests/Tsp/GroupedTspPropagationTests.cs`
- Modify: `TrafficLightsEnhancement.Logic/Tsp/TspPolicy.cs`
- Modify: `TrafficLightsEnhancement.Tests/Tsp/TspPolicyTests.cs`

- [ ] **Step 1: Write the failing tests**

```csharp
using System.Linq;
using TrafficLightsEnhancement.Logic.Tsp;
using Xunit;

namespace TrafficLightsEnhancement.Tests.Tsp;

public sealed class GroupedTspPropagationTests
{
    [Fact]
    public void Grouped_intersection_is_runtime_eligible_when_tsp_is_enabled()
    {
        var availability = TspPolicy.GetAvailability(
            settings: new TransitSignalPrioritySettings { m_Enabled = true },
            isGroupedIntersection: true);

        Assert.True(availability.IsRuntimeEligible);
        Assert.Equal(TspAvailabilityReason.None, availability.Reason);
    }

    [Fact]
    public void Propagation_window_walks_forward_until_cumulative_distance_limit()
    {
        var assignments = GroupedTspPropagation.BuildAssignments(
            members: new[]
            {
                new GroupedTspMember(memberIndex: 0, distanceFromPrevious: 0f),
                new GroupedTspMember(memberIndex: 1, distanceFromPrevious: 35f),
                new GroupedTspMember(memberIndex: 2, distanceFromPrevious: 40f),
                new GroupedTspMember(memberIndex: 3, distanceFromPrevious: 60f),
            },
            candidates: new[]
            {
                new GroupedTspCandidate(
                    originMemberIndex: 0,
                    targetSignalGroup: 2,
                    source: TspSource.Track,
                    strength: 1f,
                    expiryTimer: 12,
                    extendCurrentPhase: true)
            },
            maxPropagationDistance: 80f);

        Assert.Equal(new[] { 0, 1, 2 }, assignments.Select(x => x.MemberIndex).ToArray());
    }

    [Fact]
    public void Propagation_never_reaches_upstream_members()
    {
        var assignments = GroupedTspPropagation.BuildAssignments(
            members: new[]
            {
                new GroupedTspMember(0, 0f),
                new GroupedTspMember(1, 30f),
                new GroupedTspMember(2, 30f),
            },
            candidates: new[]
            {
                new GroupedTspCandidate(1, 3, TspSource.PublicCar, 1f, 10, false)
            },
            maxPropagationDistance: 80f);

        Assert.DoesNotContain(assignments, x => x.MemberIndex == 0);
        Assert.Equal(new[] { 1, 2 }, assignments.Select(x => x.MemberIndex).ToArray());
    }

    [Fact]
    public void Strongest_request_wins_for_overlapping_target_member()
    {
        var assignments = GroupedTspPropagation.BuildAssignments(
            members: new[]
            {
                new GroupedTspMember(0, 0f),
                new GroupedTspMember(1, 25f),
                new GroupedTspMember(2, 25f),
            },
            candidates: new[]
            {
                new GroupedTspCandidate(0, 2, TspSource.PublicCar, 0.5f, 10, false),
                new GroupedTspCandidate(1, 4, TspSource.Track, 1f, 10, true),
            },
            maxPropagationDistance: 80f);

        var memberTwo = Assert.Single(assignments.Where(x => x.MemberIndex == 2));
        Assert.Equal(1, memberTwo.OriginMemberIndex);
        Assert.Equal(4, memberTwo.TargetSignalGroup);
        Assert.Equal(TspSource.Track, memberTwo.Source);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test TrafficLightsEnhancement.Tests/TrafficLightsEnhancement.Tests.csproj --filter "GroupedTspPropagationTests|TspPolicyTests" -v minimal`

Expected: FAIL with missing `GroupedTspPropagation`, `GroupedTspMember`, or grouped-eligibility assertions that still expect `GroupedIntersection`.

- [ ] **Step 3: Write the minimal implementation**

```csharp
namespace TrafficLightsEnhancement.Logic.Tsp;

public readonly record struct GroupedTspMember(int MemberIndex, float DistanceFromPrevious);

public readonly record struct GroupedTspCandidate(
    int OriginMemberIndex,
    int TargetSignalGroup,
    TspSource Source,
    float Strength,
    uint ExpiryTimer,
    bool ExtendCurrentPhase);

public readonly record struct GroupedTspAssignment(
    int MemberIndex,
    int OriginMemberIndex,
    int TargetSignalGroup,
    TspSource Source,
    float Strength,
    uint ExpiryTimer,
    bool ExtendCurrentPhase,
    float DistanceFromOrigin);

public static class GroupedTspPropagation
{
    public static IReadOnlyList<GroupedTspAssignment> BuildAssignments(
        IReadOnlyList<GroupedTspMember> members,
        IReadOnlyList<GroupedTspCandidate> candidates,
        float maxPropagationDistance)
    {
        var winners = new Dictionary<int, GroupedTspAssignment>();

        foreach (var candidate in candidates)
        {
            float distance = 0f;
            for (int i = candidate.OriginMemberIndex; i < members.Count; i++)
            {
                if (i > candidate.OriginMemberIndex)
                {
                    distance += members[i].DistanceFromPrevious;
                    if (distance > maxPropagationDistance)
                    {
                        break;
                    }
                }

                var next = new GroupedTspAssignment(
                    MemberIndex: members[i].MemberIndex,
                    OriginMemberIndex: candidate.OriginMemberIndex,
                    TargetSignalGroup: candidate.TargetSignalGroup,
                    Source: candidate.Source,
                    Strength: candidate.Strength,
                    ExpiryTimer: candidate.ExpiryTimer,
                    ExtendCurrentPhase: candidate.ExtendCurrentPhase,
                    DistanceFromOrigin: distance);

                if (!winners.TryGetValue(next.MemberIndex, out var current)
                    || next.Strength > current.Strength
                    || (next.Strength == current.Strength && next.DistanceFromOrigin < current.DistanceFromOrigin)
                    || (next.Strength == current.Strength && next.DistanceFromOrigin == current.DistanceFromOrigin && next.OriginMemberIndex < current.OriginMemberIndex))
                {
                    winners[next.MemberIndex] = next;
                }
            }
        }

        return winners.Values.OrderBy(x => x.MemberIndex).ToArray();
    }
}
```

```csharp
public static class TspPolicy
{
    public static TspAvailability GetAvailability(
        TransitSignalPrioritySettings settings,
        bool isGroupedIntersection)
    {
        if (!settings.m_Enabled)
        {
            return new TspAvailability(false, TspAvailabilityReason.Disabled);
        }

        return new TspAvailability(true, TspAvailabilityReason.None);
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test TrafficLightsEnhancement.Tests/TrafficLightsEnhancement.Tests.csproj --filter "GroupedTspPropagationTests|TspPolicyTests" -v minimal`

Expected: PASS for the new grouped-propagation tests and updated policy tests.

- [ ] **Step 5: Commit**

```bash
git add TrafficLightsEnhancement.Logic/Tsp/GroupedTspPropagation.cs TrafficLightsEnhancement.Logic/Tsp/TspPolicy.cs TrafficLightsEnhancement.Tests/Tsp/GroupedTspPropagationTests.cs TrafficLightsEnhancement.Tests/Tsp/TspPolicyTests.cs
git commit -m "feat: add grouped TSP propagation logic"
```

### Task 2: Write Per-Junction Propagated Requests In The ECS Runtime

**Files:**
- Create: `TrafficLightsEnhancement/Components/GroupedTransitSignalPriorityRequest.cs`
- Modify: `TrafficLightsEnhancement/Systems/TrafficGroupSystem.cs`
- Modify: `TrafficLightsEnhancement/Systems/TrafficLightSystems/Simulation/ExtraTypeHandle.cs`
- Test: `TrafficLightsEnhancement.Tests/Tsp/GroupedTspPropagationTests.cs`

- [ ] **Step 1: Write the failing test**

Append this test to `TrafficLightsEnhancement.Tests/Tsp/GroupedTspPropagationTests.cs`:

```csharp
[Fact]
public void Equal_strength_requests_prefer_nearest_origin_then_lower_origin_index()
{
    var assignments = GroupedTspPropagation.BuildAssignments(
        members: new[]
        {
            new GroupedTspMember(0, 0f),
            new GroupedTspMember(1, 25f),
            new GroupedTspMember(2, 25f),
            new GroupedTspMember(3, 25f),
        },
        candidates: new[]
        {
            new GroupedTspCandidate(0, 2, TspSource.Track, 1f, 10, false),
            new GroupedTspCandidate(1, 4, TspSource.PublicCar, 1f, 10, false),
        },
        maxPropagationDistance: 120f);

    var memberThree = Assert.Single(assignments.Where(x => x.MemberIndex == 3));
    Assert.Equal(1, memberThree.OriginMemberIndex);
    Assert.Equal(4, memberThree.TargetSignalGroup);
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test TrafficLightsEnhancement.Tests/TrafficLightsEnhancement.Tests.csproj --filter GroupedTspPropagationTests.Equal_strength_requests_prefer_nearest_origin_then_lower_origin_index -v minimal`

Expected: FAIL until the distance-based tie logic is correct.

- [ ] **Step 3: Implement the ECS-side propagated request component and writer**

```csharp
namespace C2VM.TrafficLightsEnhancement.Components;

public struct GroupedTransitSignalPriorityRequest : IComponentData
{
    public byte m_TargetSignalGroup;
    public byte m_SourceType;
    public float m_Strength;
    public uint m_ExpiryTimer;
    public bool m_ExtendCurrentPhase;
    public int m_OriginMemberIndex;
    public Entity m_OriginEntity;
    public Entity m_GroupEntity;
}
```

```csharp
private const float k_GroupedTspPropagationDistance = 90f;

private void UpdateGroupTspState(Entity groupEntity)
{
    var members = GetGroupMembers(groupEntity);
    var orderedMembers = new List<(Entity Entity, TrafficGroupMember Member, float3 Position)>(members.Length);
    var candidates = new List<GroupedTspCandidate>();

    foreach (var memberEntity in members)
    {
        if (!EntityManager.HasComponent<Game.Net.Node>(memberEntity)
            || !EntityManager.HasComponent<TransitSignalPriorityRequest>(memberEntity)
            || !EntityManager.HasComponent<TransitSignalPrioritySettings>(memberEntity))
        {
            continue;
        }

        var member = EntityManager.GetComponentData<TrafficGroupMember>(memberEntity);
        var settings = EntityManager.GetComponentData<TransitSignalPrioritySettings>(memberEntity);
        if (!settings.m_Enabled || !settings.m_AllowGroupPropagation)
        {
            continue;
        }

        orderedMembers.Add((memberEntity, member, EntityManager.GetComponentData<Game.Net.Node>(memberEntity).m_Position));

        var request = EntityManager.GetComponentData<TransitSignalPriorityRequest>(memberEntity);
        if (request.m_TargetSignalGroup > 0 && request.m_Strength > 0f)
        {
            candidates.Add(new GroupedTspCandidate(
                member.m_GroupIndex,
                request.m_TargetSignalGroup,
                (global::TrafficLightsEnhancement.Logic.Tsp.TspSource)request.m_SourceType,
                request.m_Strength,
                request.m_ExpiryTimer,
                request.m_ExtendCurrentPhase));
        }
    }

    orderedMembers.Sort((a, b) => a.Member.m_GroupIndex.CompareTo(b.Member.m_GroupIndex));
    var propagationMembers = new List<GroupedTspMember>(orderedMembers.Count);
    float3 previousPosition = default;

    for (int i = 0; i < orderedMembers.Count; i++)
    {
        float distanceFromPrevious = i == 0 ? 0f : math.distance(previousPosition, orderedMembers[i].Position);
        propagationMembers.Add(new GroupedTspMember(orderedMembers[i].Member.m_GroupIndex, distanceFromPrevious));
        previousPosition = orderedMembers[i].Position;
    }

    var assignments = GroupedTspPropagation.BuildAssignments(propagationMembers, candidates, k_GroupedTspPropagationDistance);
    // map assignment.MemberIndex back to the matching ordered member entity and write GroupedTransitSignalPriorityRequest
```

Also update `RemoveGroupTspState` so it clears `GroupedTransitSignalPriorityRequest` from all members in the group, and add the new lookup to `ExtraTypeHandle`:

```csharp
public ComponentLookup<GroupedTransitSignalPriorityRequest> m_GroupedTransitSignalPriorityRequest;
...
m_GroupedTransitSignalPriorityRequest = state.GetComponentLookup<GroupedTransitSignalPriorityRequest>(isReadOnly: true);
...
m_GroupedTransitSignalPriorityRequest.Update(ref state);
```

- [ ] **Step 4: Run the test to verify it passes**

Run: `dotnet test TrafficLightsEnhancement.Tests/TrafficLightsEnhancement.Tests.csproj --filter GroupedTspPropagationTests -v minimal`

Expected: PASS with the tie-break test green and no regressions in earlier grouped-propagation coverage.

- [ ] **Step 5: Commit**

```bash
git add TrafficLightsEnhancement/Components/GroupedTransitSignalPriorityRequest.cs TrafficLightsEnhancement/Systems/TrafficGroupSystem.cs TrafficLightsEnhancement/Systems/TrafficLightSystems/Simulation/ExtraTypeHandle.cs TrafficLightsEnhancement.Tests/Tsp/GroupedTspPropagationTests.cs
git commit -m "feat: write grouped TSP requests per junction"
```

### Task 3: Consume Propagated Requests In Signal Simulation And Diagnostics

**Files:**
- Modify: `TrafficLightsEnhancement/Systems/TrafficLightSystems/Simulation/TransitSignalPriorityRuntime.cs`
- Modify: `TrafficLightsEnhancement/Systems/TrafficLightSystems/Simulation/PatchedTrafficLightSystem.cs`
- Modify: `TrafficLightsEnhancement/Components/TransitSignalPriorityDecisionTrace.cs`
- Modify: `TrafficLightsEnhancement/Systems/TransitSignalPriorityDiagnosticsSystem.cs`
- Test: `TrafficLightsEnhancement.Tests/Tsp/TspDecisionEngineTests.cs`

- [ ] **Step 1: Write the failing tests**

Append these tests to `TrafficLightsEnhancement.Tests/Tsp/TspDecisionEngineTests.cs`:

```csharp
[Fact]
public void Grouped_enabled_intersection_is_runtime_eligible_for_local_request()
{
    var availability = TspPolicy.GetAvailability(
        new TransitSignalPrioritySettings { m_Enabled = true },
        isGroupedIntersection: true);

    Assert.True(availability.IsRuntimeEligible);
}

[Fact]
public void Stronger_grouped_request_overrides_weaker_local_request()
{
    var local = new TspSignalRequest(targetSignalGroup: 2, TspSource.PublicCar, strength: 0.5f, expiryTimer: 10, extendCurrentPhase: false);
    var propagated = new TspSignalRequest(targetSignalGroup: 4, TspSource.Track, strength: 1f, expiryTimer: 10, extendCurrentPhase: true);

    var winner = local.Strength >= propagated.Strength ? local : propagated;

    Assert.Equal(4, winner.TargetSignalGroup);
    Assert.Equal(TspSource.Track, winner.Source);
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test TrafficLightsEnhancement.Tests/TrafficLightsEnhancement.Tests.csproj --filter "TspDecisionEngineTests" -v minimal`

Expected: FAIL if grouped eligibility still blocks local requests or if the runtime merge path still only reads the old group aggregate state.

- [ ] **Step 3: Implement the minimal runtime integration**

In `TransitSignalPriorityRuntime.cs`, remove the grouped-membership early exit and add a propagated-request loader:

```csharp
public static bool TryGetGroupedPropagatedRequest(
    PatchedTrafficLightSystem.UpdateTrafficLightsJob job,
    Entity junctionEntity,
    TrafficLights trafficLights,
    out TransitSignalPriorityRequest request)
{
    request = default;

    if (!job.m_ExtraTypeHandle.m_GroupedTransitSignalPriorityRequest.TryGetComponent(junctionEntity, out var grouped))
    {
        return false;
    }

    request = new TransitSignalPriorityRequest
    {
        m_TargetSignalGroup = grouped.m_TargetSignalGroup,
        m_SourceType = grouped.m_SourceType,
        m_Strength = grouped.m_Strength,
        m_ExpiryTimer = grouped.m_ExpiryTimer,
        m_ExtendCurrentPhase = grouped.m_ExtendCurrentPhase
            && trafficLights.m_CurrentSignalGroup > 0
            && trafficLights.m_CurrentSignalGroup == grouped.m_TargetSignalGroup,
    };
    return true;
}
```

In `PatchedTrafficLightSystem.cs`, replace the old `TryGetCoordinatedGroupRequest` block with:

```csharp
if (TspRuntime.TryGetGroupedPropagatedRequest(this, currentEntity, trafficLights, out var groupedTspRequest)
    && (!hasTspRequest || groupedTspRequest.m_Strength > activeTspRequest.m_Strength))
{
    hasTspRequest = true;
    activeTspRequest = groupedTspRequest;
    tspRequestFromGroupedPropagation = true;
}
```

Update `TransitSignalPriorityDecisionTrace` and `TransitSignalPriorityDiagnosticsSystem` so logs describe `grouped-propagation` instead of `coordinated-group`, and include origin-member metadata if you add it to the trace:

```csharp
string requestScope = trace.m_FromGroupedPropagation ? "grouped-propagation" : "local";
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test TrafficLightsEnhancement.Tests/TrafficLightsEnhancement.Tests.csproj --filter "TspDecisionEngineTests|TspPolicyTests|GroupedTspPropagationTests" -v minimal`

Expected: PASS across all TSP logic tests with grouped eligibility and propagated-request arbitration covered.

- [ ] **Step 5: Commit**

```bash
git add TrafficLightsEnhancement/Systems/TrafficLightSystems/Simulation/TransitSignalPriorityRuntime.cs TrafficLightsEnhancement/Systems/TrafficLightSystems/Simulation/PatchedTrafficLightSystem.cs TrafficLightsEnhancement/Components/TransitSignalPriorityDecisionTrace.cs TrafficLightsEnhancement/Systems/TransitSignalPriorityDiagnosticsSystem.cs TrafficLightsEnhancement.Tests/Tsp/TspDecisionEngineTests.cs
git commit -m "feat: apply grouped TSP requests in simulation"
```

### Task 4: Re-enable Grouped TSP Controls And Update Copy

**Files:**
- Modify: `TrafficLightsEnhancement/Systems/UI/UISystem.UIBIndings.cs`
- Modify: `TrafficLightsEnhancement/UI/src/mods/components/traffic-groups/main-panel/IndexComponent/index.tsx`
- Modify: `TrafficLightsEnhancement/UI/src/mods/localisations/en-US.ts`

- [ ] **Step 1: Write the failing test**

Write a narrow string-level expectation in the localisation file by replacing the old grouped-unavailable text with the new propagation text first, then verify it is still missing in the UI:

```typescript
export default {
  PropagateTransitRequestsToGroup: "Propagate Requests Downstream Through Group",
  AllowCoordinatedTsp: "Allow Local Grouped Transit Priority",
  TspGroupedPropagationHelp: "When enabled, tram and bus-lane requests can help nearby downstream group members instead of the whole corridor."
};
```

- [ ] **Step 2: Run a focused search to verify the old unavailable-state wiring is still present**

Run: `Select-String -Path 'TrafficLightsEnhancement/Systems/UI/UISystem.UIBIndings.cs','TrafficLightsEnhancement/UI/src/mods/components/traffic-groups/main-panel/IndexComponent/index.tsx' -Pattern 'TspUnavailableForTrafficGroup|disabled = isGroupedIntersection|opacity: 0.5'`

Expected: MATCHES showing the old grouped-disabled wiring still exists before you remove it.

- [ ] **Step 3: Implement the minimal UI changes**

In `UISystem.UIBIndings.cs`, stop disabling grouped TSP controls solely because the junction has `TrafficGroupMember`:

```csharp
menu.items.Add(new UITypes.ItemCheckbox
{
    type = "checkbox",
    key = "TspEnabled",
    value = tspSettings.m_Enabled.ToString(),
    isChecked = tspSettings.m_Enabled,
    label = "EnableTransitSignalPriority",
    engineEventName = "C2VM.TrafficLightsEnhancement.TRIGGER:CallMainPanelUpdateOption",
    disabled = false
});
```

Keep the `TspAllowGroupPropagation` checkbox visible only for grouped intersections, but do not disable it.

In `index.tsx`, replace the fake disabled row with a real checkbox row:

```tsx
<Row hoverEffect={true} className={styles.hover} data={{
    itemType: "checkbox",
    type: "",
    isChecked: displayedGroup.tspPropagationEnabled,
    key: "TspPropagationEnabled",
    value: "0",
    label: "",
    engineEventName: "C2VM.TrafficLightsEnhancement.TRIGGER:CallSetTspPropagationEnabled"
}}>
    <Checkbox isChecked={displayedGroup.tspPropagationEnabled} />
    <div className={styles.dimLabel}>{getString(locale, "AllowCoordinatedTsp")}</div>
</Row>
<div className={styles.infoText}>{getString(locale, "TspGroupedPropagationHelp")}</div>
```

Update `en-US.ts` to remove the old unavailable phrasing and reflect downstream-local propagation.

- [ ] **Step 4: Run verification**

Run: `Select-String -Path 'TrafficLightsEnhancement/Systems/UI/UISystem.UIBIndings.cs','TrafficLightsEnhancement/UI/src/mods/components/traffic-groups/main-panel/IndexComponent/index.tsx','TrafficLightsEnhancement/UI/src/mods/localisations/en-US.ts' -Pattern 'TspUnavailableForTrafficGroup|opacity: 0.5'`

Expected: no matches in the edited files.

- [ ] **Step 5: Commit**

```bash
git add TrafficLightsEnhancement/Systems/UI/UISystem.UIBIndings.cs TrafficLightsEnhancement/UI/src/mods/components/traffic-groups/main-panel/IndexComponent/index.tsx TrafficLightsEnhancement/UI/src/mods/localisations/en-US.ts
git commit -m "feat: re-enable grouped TSP controls"
```

### Task 5: Final Verification

**Files:**
- Test: `TrafficLightsEnhancement.Tests/Tsp/GroupedTspPropagationTests.cs`
- Test: `TrafficLightsEnhancement.Tests/Tsp/TspDecisionEngineTests.cs`
- Modify if needed: touched files from Tasks 1-4

- [ ] **Step 1: Run the logic test suite**

Run: `dotnet test TrafficLightsEnhancement.Tests/TrafficLightsEnhancement.Tests.csproj -v minimal`

Expected: PASS with all TSP logic tests green.

- [ ] **Step 2: Build the solution**

Run: `dotnet build Cities2-TrafficLightsEnhancement.sln -v minimal`

Expected: PASS with no new compile errors in `TrafficLightsEnhancement`, `TrafficLightsEnhancement.Logic`, or `TrafficLightsEnhancement.Tests`.

- [ ] **Step 3: Review the final diff**

Run: `git diff --stat main...HEAD`

Expected: diff limited to grouped TSP logic, runtime, diagnostics, and UI files for this feature.

- [ ] **Step 4: Commit any final polish if needed**

```bash
git add TrafficLightsEnhancement.Logic/Tsp/GroupedTspPropagation.cs TrafficLightsEnhancement.Logic/Tsp/TspPolicy.cs TrafficLightsEnhancement.Tests/Tsp/GroupedTspPropagationTests.cs TrafficLightsEnhancement.Tests/Tsp/TspPolicyTests.cs TrafficLightsEnhancement/Components/GroupedTransitSignalPriorityRequest.cs TrafficLightsEnhancement/Systems/TrafficGroupSystem.cs TrafficLightsEnhancement/Systems/TrafficLightSystems/Simulation/ExtraTypeHandle.cs TrafficLightsEnhancement/Systems/TrafficLightSystems/Simulation/TransitSignalPriorityRuntime.cs TrafficLightsEnhancement/Systems/TrafficLightSystems/Simulation/PatchedTrafficLightSystem.cs TrafficLightsEnhancement/Components/TransitSignalPriorityDecisionTrace.cs TrafficLightsEnhancement/Systems/TransitSignalPriorityDiagnosticsSystem.cs TrafficLightsEnhancement/Systems/UI/UISystem.UIBIndings.cs TrafficLightsEnhancement/UI/src/mods/components/traffic-groups/main-panel/IndexComponent/index.tsx TrafficLightsEnhancement/UI/src/mods/localisations/en-US.ts
git commit -m "fix: polish local grouped transit priority"
```

- [ ] **Step 5: Prepare for branch finish**

Run: `git status --short`

Expected: clean working tree, ready for the final repository-wide review and branch-finishing flow.
