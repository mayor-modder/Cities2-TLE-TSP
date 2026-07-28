using TrafficLightsEnhancement.Logic.TrafficGroups;
using Xunit;

namespace TrafficLightsEnhancement.Tests.TrafficGroups;

public sealed class TrafficGroupLockstepDiagnosticsTests
{
    private static readonly TrafficGroupLockstepControllerSnapshot Expected =
        new(2, 1, 2, 30, 300, 4);

    [Fact]
    public void Classify_MissingMaster_ReportsSynchronizationDidNotRun()
    {
        TrafficGroupLockstepEvidence evidence = CreateAppliedEvidence(
            passFlags: TrafficGroupLockstepPassFlags.CollectionVisited
                | TrafficGroupLockstepPassFlags.IndependentVisited
                | TrafficGroupLockstepPassFlags.SynchronizationVisited,
            disposition: TrafficGroupLockstepSyncDisposition.MissingMaster);

        TrafficGroupLockstepClassification result =
            TrafficGroupLockstepDiagnostics.Classify(in evidence);

        Assert.Equal(
            TrafficGroupLockstepVerdict.SynchronizationDidNotRun,
            result.Verdict);
        Assert.Contains("master", result.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(TrafficGroupLockstepSyncDisposition.InvalidMaster)]
    [InlineData(TrafficGroupLockstepSyncDisposition.IncompleteMapping)]
    [InlineData(TrafficGroupLockstepSyncDisposition.UnmappedCurrentPhase)]
    [InlineData(TrafficGroupLockstepSyncDisposition.UnmappedNextPhase)]
    public void Classify_RefusedSynchronization_ReportsExactDisposition(
        TrafficGroupLockstepSyncDisposition disposition)
    {
        TrafficGroupLockstepEvidence evidence = CreateAppliedEvidence(
            passFlags: TrafficGroupLockstepPassFlags.CollectionVisited
                | TrafficGroupLockstepPassFlags.IndependentVisited
                | TrafficGroupLockstepPassFlags.SynchronizationVisited,
            disposition: disposition);

        TrafficGroupLockstepClassification result =
            TrafficGroupLockstepDiagnostics.Classify(in evidence);

        Assert.Equal(TrafficGroupLockstepVerdict.SynchronizationRefused, result.Verdict);
        Assert.Contains(disposition.ToString(), result.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void Classify_IndependentStateMachineAdvanced_TakesPrecedence()
    {
        TrafficGroupLockstepEvidence evidence = CreateAppliedEvidence(
            passFlags: TrafficGroupLockstepPassFlags.CollectionVisited
                | TrafficGroupLockstepPassFlags.IndependentVisited
                | TrafficGroupLockstepPassFlags.IndependentAdvanced
                | TrafficGroupLockstepPassFlags.SynchronizationVisited
                | TrafficGroupLockstepPassFlags.SynchronizationApplied,
            disposition: TrafficGroupLockstepSyncDisposition.Applied);

        TrafficGroupLockstepClassification result =
            TrafficGroupLockstepDiagnostics.Classify(in evidence);

        Assert.Equal(
            TrafficGroupLockstepVerdict.IndependentStateMachineAdvanced,
            result.Verdict);
    }

    [Fact]
    public void Classify_ControllerChangedAfterSynchronization_IsDetected()
    {
        TrafficGroupLockstepControllerSnapshot live =
            new(2, 1, 2, 31, 300, 4);
        TrafficGroupLockstepEvidence evidence = CreateAppliedEvidence(live: live);

        TrafficGroupLockstepClassification result =
            TrafficGroupLockstepDiagnostics.Classify(in evidence);

        Assert.Equal(
            TrafficGroupLockstepVerdict.ControllerChangedAfterSynchronization,
            result.Verdict);
    }

    [Fact]
    public void Classify_LaneOutputsChangedAfterSynchronization_IsDetected()
    {
        TrafficGroupLockstepEvidence evidence = CreateAppliedEvidence(
            laneHashAfter: 0x1234UL,
            liveLaneHash: 0x5678UL);

        TrafficGroupLockstepClassification result =
            TrafficGroupLockstepDiagnostics.Classify(in evidence);

        Assert.Equal(
            TrafficGroupLockstepVerdict.LaneOutputsChangedAfterSynchronization,
            result.Verdict);
    }

    [Fact]
    public void Classify_RenderedOutputsChangedAfterSynchronization_IsDetected()
    {
        TrafficGroupLockstepEvidence evidence = CreateAppliedEvidence(
            renderedHashAfter: 0xABCDUL,
            liveRenderedHash: 0xDCBAUL);

        TrafficGroupLockstepClassification result =
            TrafficGroupLockstepDiagnostics.Classify(in evidence);

        Assert.Equal(
            TrafficGroupLockstepVerdict.RenderedOutputsChangedAfterSynchronization,
            result.Verdict);
    }

    [Fact]
    public void Classify_LiveOutputsMissingMappedPhase_IsDetected()
    {
        TrafficGroupLockstepEvidence evidence = CreateAppliedEvidence(
            mappedCurrentGroupBit: 0b0010,
            mappedNextGroupBit: 0b0100,
            liveOutputGroupMask: 0b1000);

        TrafficGroupLockstepClassification result =
            TrafficGroupLockstepDiagnostics.Classify(in evidence);

        Assert.Equal(
            TrafficGroupLockstepVerdict.OutputMasksDoNotRepresentMappedPhase,
            result.Verdict);
    }

    [Fact]
    public void Classify_MatchingEvidence_IsInSync()
    {
        TrafficGroupLockstepEvidence evidence = CreateAppliedEvidence();

        TrafficGroupLockstepClassification result =
            TrafficGroupLockstepDiagnostics.Classify(in evidence);

        Assert.Equal(TrafficGroupLockstepVerdict.InSync, result.Verdict);
        Assert.Equal("All captured lockstep boundaries match.", result.Reason);
    }

    [Fact]
    public void Classify_OngoingPhase_RequiresOnlyMappedCurrentOutput()
    {
        TrafficGroupLockstepEvidence evidence = CreateAppliedEvidence(
            liveOutputGroupMask: 0b0010);

        TrafficGroupLockstepClassification result =
            TrafficGroupLockstepDiagnostics.Classify(in evidence);

        Assert.Equal(TrafficGroupLockstepVerdict.InSync, result.Verdict);
    }

    [Fact]
    public void Classify_BeginningPhase_RequiresMappedNextOutput()
    {
        TrafficGroupLockstepControllerSnapshot beginning =
            new(1, 1, 2, 30, 300, 4);
        TrafficGroupLockstepEvidence evidence = new(
            hasDebugState: true,
            isCoordinated: true,
            isGreenWave: false,
            TrafficGroupLockstepPassFlags.CollectionVisited
                | TrafficGroupLockstepPassFlags.IndependentVisited
                | TrafficGroupLockstepPassFlags.IndependentDeferred
                | TrafficGroupLockstepPassFlags.SynchronizationVisited
                | TrafficGroupLockstepPassFlags.SynchronizationApplied,
            TrafficGroupLockstepSyncDisposition.Applied,
            before: beginning,
            master: beginning,
            after: beginning,
            live: beginning,
            laneHashAfter: 0x1234UL,
            liveLaneHash: 0x1234UL,
            renderedHashAfter: 0xABCDUL,
            liveRenderedHash: 0xABCDUL,
            mappedCurrentGroupBit: 0b0010,
            mappedNextGroupBit: 0b0100,
            liveOutputGroupMask: 0b0100);

        TrafficGroupLockstepClassification result =
            TrafficGroupLockstepDiagnostics.Classify(in evidence);

        Assert.Equal(TrafficGroupLockstepVerdict.InSync, result.Verdict);
    }

    [Fact]
    public void Classify_GreenWave_IsExplicitlyExcluded()
    {
        TrafficGroupLockstepEvidence evidence = CreateAppliedEvidence(isGreenWave: true);

        TrafficGroupLockstepClassification result =
            TrafficGroupLockstepDiagnostics.Classify(in evidence);

        Assert.Equal(TrafficGroupLockstepVerdict.GreenWaveExcluded, result.Verdict);
    }

    [Fact]
    public void Classify_InactiveLeaderShard_IsInsufficientEvidence()
    {
        TrafficGroupLockstepEvidence evidence = CreateAppliedEvidence(
            passFlags: TrafficGroupLockstepPassFlags.SynchronizationVisited,
            disposition: TrafficGroupLockstepSyncDisposition.InactiveGroup);

        TrafficGroupLockstepClassification result =
            TrafficGroupLockstepDiagnostics.Classify(in evidence);

        Assert.Equal(
            TrafficGroupLockstepVerdict.InsufficientEvidence,
            result.Verdict);
        Assert.Contains("shard", result.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Classify_MissingRuntimeState_IsInsufficientEvidence()
    {
        TrafficGroupLockstepEvidence evidence = CreateAppliedEvidence(hasDebugState: false);

        TrafficGroupLockstepClassification result =
            TrafficGroupLockstepDiagnostics.Classify(in evidence);

        Assert.Equal(
            TrafficGroupLockstepVerdict.InsufficientEvidence,
            result.Verdict);
    }

    [Fact]
    public void AddHash_IsDeterministicAndOrderSensitive()
    {
        ulong first = TrafficGroupLockstepDiagnostics.AddHash(
            TrafficGroupLockstepDiagnostics.FnvOffsetBasis,
            0x0102030405060708UL);
        ulong repeat = TrafficGroupLockstepDiagnostics.AddHash(
            TrafficGroupLockstepDiagnostics.FnvOffsetBasis,
            0x0102030405060708UL);
        ulong reversed = TrafficGroupLockstepDiagnostics.AddHash(
            TrafficGroupLockstepDiagnostics.FnvOffsetBasis,
            0x0807060504030201UL);

        Assert.Equal(first, repeat);
        Assert.NotEqual(first, reversed);
    }

    private static TrafficGroupLockstepEvidence CreateAppliedEvidence(
        bool hasDebugState = true,
        bool isGreenWave = false,
        TrafficGroupLockstepPassFlags passFlags =
            TrafficGroupLockstepPassFlags.CollectionVisited
            | TrafficGroupLockstepPassFlags.IndependentVisited
            | TrafficGroupLockstepPassFlags.IndependentDeferred
            | TrafficGroupLockstepPassFlags.SynchronizationVisited
            | TrafficGroupLockstepPassFlags.SynchronizationApplied,
        TrafficGroupLockstepSyncDisposition disposition =
            TrafficGroupLockstepSyncDisposition.Applied,
        TrafficGroupLockstepControllerSnapshot? live = null,
        ulong laneHashAfter = 0x1234UL,
        ulong liveLaneHash = 0x1234UL,
        ulong renderedHashAfter = 0xABCDUL,
        ulong liveRenderedHash = 0xABCDUL,
        ushort mappedCurrentGroupBit = 0b0010,
        ushort mappedNextGroupBit = 0b0100,
        ushort liveOutputGroupMask = 0b0110)
    {
        return new TrafficGroupLockstepEvidence(
            hasDebugState,
            isCoordinated: true,
            isGreenWave,
            passFlags,
            disposition,
            before: Expected,
            master: Expected,
            after: Expected,
            live: live ?? Expected,
            laneHashAfter,
            liveLaneHash,
            renderedHashAfter,
            liveRenderedHash,
            mappedCurrentGroupBit,
            mappedNextGroupBit,
            liveOutputGroupMask);
    }
}
