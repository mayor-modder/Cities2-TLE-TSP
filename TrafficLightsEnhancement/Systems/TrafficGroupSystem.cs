using C2VM.TrafficLightsEnhancement.Components;
using Colossal.Logging;
using Game;
using Game.Common;
using Game.Net;
using Game.Simulation;
using Game.UI.Localization;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using System.Collections.Generic;
using C2VM.TrafficLightsEnhancement.Domain;
using C2VM.TrafficLightsEnhancement.Extensions;
using C2VM.TrafficLightsEnhancement.Utils;
using Colossal.Entities;
using Game.SceneFlow;
using TrafficLightsEnhancement.Logic.TrafficGroups;

namespace C2VM.TrafficLightsEnhancement.Systems;

public partial class TrafficGroupSystem : GameSystemBase
{
	private static ILog m_Log = Mod.log;

	private EntityQuery m_GroupQuery;
	private EntityQuery m_MemberQuery;
	private EntityQuery m_LockstepDiagnosticQuery;
	private SimulationSystem m_SimulationSystem;
	private readonly Dictionary<Entity, string> m_LastMovementMappingFailureReports = new();

	protected override void OnCreate()
	{
		base.OnCreate();

		m_GroupQuery = GetEntityQuery(
			ComponentType.ReadOnly<TrafficGroup>()
		);

		m_MemberQuery = GetEntityQuery(
			ComponentType.ReadOnly<TrafficGroupMember>()
		);

		m_LockstepDiagnosticQuery = GetEntityQuery(
			ComponentType.ReadOnly<TrafficGroupLockstepDebugState>()
		);
		
		m_SimulationSystem = World.GetOrCreateSystemManaged<SimulationSystem>();
	}

	protected override void OnUpdate()
	{
		MaintainLockstepDiagnosticsComponents(
			Mod.m_Setting != null
			&& Mod.m_Setting.m_ShowTransitSignalPriorityDiagnostics);

		float currentTick = m_SimulationSystem.frameIndex;
		
		var groups = m_GroupQuery.ToEntityArray(Allocator.Temp);
		var groupComponents = m_GroupQuery.ToComponentDataArray<TrafficGroup>(Allocator.Temp);
		
		for (int i = 0; i < groups.Length; i++)
		{
			var groupEntity = groups[i];
			var group = groupComponents[i];
			
			if (!group.m_IsCoordinated)
			{
				continue;
			}

			Entity leaderEntity = GetGroupLeader(groupEntity);
			RefreshGroupRuntimeState(groupEntity, leaderEntity);
			RefreshMovementMappings(groupEntity, leaderEntity);
			
			group.m_CycleTimer += 1f;
			if (group.m_CycleTimer >= group.m_CycleLength)
			{
				group.m_CycleTimer = 0f;
			}
			UpdateMasterClock(groupEntity, ref group);
			ApplyCoordination(groupEntity, group);
			EntityManager.SetComponentData(groupEntity, group);
		}
		
		groups.Dispose();
		groupComponents.Dispose();
	}

	private void MaintainLockstepDiagnosticsComponents(bool diagnosticsEnabled)
	{
		using NativeArray<Entity> memberEntities =
			m_MemberQuery.ToEntityArray(Allocator.Temp);
		if (diagnosticsEnabled)
		{
			foreach (Entity memberEntity in memberEntities)
			{
				if (!EntityManager.HasComponent<TrafficGroupLockstepDebugState>(memberEntity))
				{
					EntityManager.AddComponentData(
						memberEntity,
						default(TrafficGroupLockstepDebugState));
				}
			}

			using NativeArray<Entity> diagnosticEntities =
				m_LockstepDiagnosticQuery.ToEntityArray(Allocator.Temp);
			foreach (Entity diagnosticEntity in diagnosticEntities)
			{
				if (!EntityManager.HasComponent<TrafficGroupMember>(diagnosticEntity))
				{
					EntityManager.RemoveComponent<TrafficGroupLockstepDebugState>(
						diagnosticEntity);
				}
			}
			return;
		}

		using NativeArray<Entity> existingDiagnosticEntities =
			m_LockstepDiagnosticQuery.ToEntityArray(Allocator.Temp);
		foreach (Entity diagnosticEntity in existingDiagnosticEntities)
		{
			EntityManager.RemoveComponent<TrafficGroupLockstepDebugState>(
				diagnosticEntity);
		}
	}

	private void RefreshGroupRuntimeState(Entity groupEntity, Entity leaderEntity)
	{
		if (leaderEntity == Entity.Null
		    || !EntityManager.TryGetSharedComponent<UpdateFrame>(leaderEntity, out var updateFrame))
		{
			if (EntityManager.HasComponent<TrafficGroupRuntimeData>(groupEntity))
			{
				EntityManager.RemoveComponent<TrafficGroupRuntimeData>(groupEntity);
			}
			return;
		}

		var runtimeData = new TrafficGroupRuntimeData
		{
			m_LeaderUpdateFrameIndex = updateFrame.m_Index
		};
		if (EntityManager.HasComponent<TrafficGroupRuntimeData>(groupEntity))
		{
			EntityManager.SetComponentData(groupEntity, runtimeData);
		}
		else
		{
			EntityManager.AddComponentData(groupEntity, runtimeData);
		}
	}

	private void RefreshMovementMappings(Entity groupEntity, Entity leaderEntity)
	{
		if (leaderEntity == Entity.Null
		    || !EntityManager.TryGetComponent(leaderEntity, out TrafficLights leaderLights))
		{
			RemoveMovementMappings(groupEntity);
			return;
		}

		TrafficGroupPhaseSignature[] leaderSignatures =
			BuildPhaseSignatures(leaderEntity, leaderLights);
		var members = GetGroupMembers(groupEntity);
		foreach (Entity memberEntity in members)
		{
			if (!EntityManager.TryGetComponent(memberEntity, out TrafficLights memberLights))
			{
				if (EntityManager.HasComponent<TrafficGroupPhaseMapping>(memberEntity))
				{
					EntityManager.RemoveComponent<TrafficGroupPhaseMapping>(memberEntity);
				}
				continue;
			}

			TrafficGroupPhaseSignature[] memberSignatures =
				BuildPhaseSignatures(memberEntity, memberLights);
			TrafficGroupPhaseMap phaseMap = default;
			TrafficGroupMovementMappingFailure mappingFailure;
			bool useIdentityMapping = memberEntity == leaderEntity
				|| (UsesCustomPhase(leaderEntity) && UsesCustomPhase(memberEntity));
			bool mapped = useIdentityMapping
				? TrafficGroupMovementMappingPolicy.TryBuildIdentity(
					leaderSignatures,
					memberSignatures,
					out phaseMap,
					out mappingFailure)
				: TrafficGroupMovementMappingPolicy.TryBuild(
					leaderSignatures,
					memberSignatures,
					out phaseMap,
					out mappingFailure);
			if (mapped)
			{
				m_LastMovementMappingFailureReports.Remove(memberEntity);
				var mapping = new TrafficGroupPhaseMapping { m_Map = phaseMap };
				if (EntityManager.HasComponent<TrafficGroupPhaseMapping>(memberEntity))
				{
					EntityManager.SetComponentData(memberEntity, mapping);
				}
				else
				{
					EntityManager.AddComponentData(memberEntity, mapping);
				}
			}
			else if (EntityManager.HasComponent<TrafficGroupPhaseMapping>(memberEntity))
			{
				EntityManager.RemoveComponent<TrafficGroupPhaseMapping>(memberEntity);
			}

			if (!mapped)
			{
				LogMovementMappingFailureIfChanged(
					groupEntity,
					leaderEntity,
					memberEntity,
					mappingFailure,
					leaderSignatures,
					memberSignatures);
			}
		}
		members.Dispose();
	}

	private bool UsesCustomPhase(Entity junctionEntity)
	{
		return EntityManager.TryGetComponent(
			       junctionEntity,
			       out CustomTrafficLights customTrafficLights)
		       && customTrafficLights.GetPatternOnly()
		       == CustomTrafficLights.Patterns.CustomPhase;
	}

	private void LogMovementMappingFailureIfChanged(
		Entity groupEntity,
		Entity leaderEntity,
		Entity memberEntity,
		TrafficGroupMovementMappingFailure failure,
		TrafficGroupPhaseSignature[] leaderSignatures,
		TrafficGroupPhaseSignature[] memberSignatures)
	{
		string report =
			$"[TLE][TrafficGroupMapping] group={groupEntity.Index}:{groupEntity.Version} "
			+ $"leader={leaderEntity.Index}:{leaderEntity.Version} "
			+ $"member={memberEntity.Index}:{memberEntity.Version} "
			+ $"reason={failure.Reason} "
			+ $"leaderPhase={failure.LeaderPhase} "
			+ $"memberPhase={failure.MemberPhase} "
			+ $"leaderSignatures=[{FormatPhaseSignatures(leaderSignatures)}] "
			+ $"memberSignatures=[{FormatPhaseSignatures(memberSignatures)}]";

		if (m_LastMovementMappingFailureReports.TryGetValue(
			    memberEntity,
			    out string previousReport)
		    && previousReport == report)
		{
			return;
		}

		m_LastMovementMappingFailureReports[memberEntity] = report;
		m_Log.Warn(report);
	}

	private static string FormatPhaseSignatures(
		TrafficGroupPhaseSignature[] signatures)
	{
		var formatted = new string[signatures.Length];
		for (int index = 0; index < signatures.Length; index++)
		{
			formatted[index] = signatures[index].ToDiagnosticString();
		}

		return string.Join(" | ", formatted);
	}

	private TrafficGroupPhaseSignature[] BuildPhaseSignatures(
		Entity junctionEntity,
		TrafficLights trafficLights)
	{
		int phaseCount = trafficLights.m_SignalGroupCount;
		if (phaseCount < 1
		    || phaseCount > TrafficGroupMovementMappingPolicy.MaximumMappedPhaseCount
		    || !EntityManager.TryGetBuffer<SubLane>(junctionEntity, true, out var subLanes)
		    || !EntityManager.TryGetBuffer<ConnectedEdge>(junctionEntity, true, out var connectedEdges)
		    || !EntityManager.TryGetComponent(junctionEntity, out Node node))
		{
			return System.Array.Empty<TrafficGroupPhaseSignature>();
		}

		var subLaneLookup = GetBufferLookup<SubLane>(true);
		var laneLookup = GetComponentLookup<Lane>(true);
		var edgeLookup = GetComponentLookup<Edge>(true);
		var edgeGeometryLookup = GetComponentLookup<EdgeGeometry>(true);
		var carLaneLookup = GetComponentLookup<CarLane>(true);
		var trackLaneLookup = GetComponentLookup<TrackLane>(true);
		using NativeHashMap<Entity, NodeUtils.LaneConnection> laneConnectionMap =
			NodeUtils.GetLaneConnectionMap(
				Allocator.Temp,
				subLanes,
				connectedEdges,
				subLaneLookup,
				laneLookup);

		var roadAxes = new ulong[phaseCount];
		var trackAxes = new ulong[phaseCount];
		var roadMovements = new TrafficGroupMovementMask[phaseCount];
		var trackMovements = new TrafficGroupMovementMask[phaseCount];
		var roadYieldMovements = new TrafficGroupMovementMask[phaseCount];
		var trackYieldMovements = new TrafficGroupMovementMask[phaseCount];
		for (int laneIndex = 0; laneIndex < subLanes.Length; laneIndex++)
		{
			Entity subLaneEntity = subLanes[laneIndex].m_SubLane;
			if (!EntityManager.TryGetComponent(subLaneEntity, out LaneSignal laneSignal)
			    || !laneConnectionMap.TryGetValue(
				    subLaneEntity,
				    out NodeUtils.LaneConnection laneConnection)
			    || laneConnection.m_SourceEdge == Entity.Null)
			{
				continue;
			}

			bool isCarLane = carLaneLookup.HasComponent(subLaneEntity)
			                 || (laneConnection.m_SourceSubLane != Entity.Null
			                     && carLaneLookup.HasComponent(laneConnection.m_SourceSubLane));
			bool isTrackLane = trackLaneLookup.HasComponent(subLaneEntity)
			                   || (laneConnection.m_SourceSubLane != Entity.Null
			                       && trackLaneLookup.HasComponent(laneConnection.m_SourceSubLane));
			if (!isCarLane && !isTrackLane)
			{
				continue;
			}

			float3 edgePosition = GetEdgePositionForJunction(
				junctionEntity,
				laneConnection.m_SourceEdge,
				edgeLookup,
				edgeGeometryLookup);
			int axisBin = TrafficGroupMovementMappingPolicy.QuantizeUndirectedAxis(
				edgePosition.x - node.m_Position.x,
				edgePosition.z - node.m_Position.z);
			if (axisBin < 0)
			{
				continue;
			}

			ulong axisBit = 1UL << axisBin;
			ExtraLaneSignal extraLaneSignal =
				EntityManager.TryGetComponent(subLaneEntity, out ExtraLaneSignal existingExtraLaneSignal)
					? existingExtraLaneSignal
					: default;
			TrafficGroupMovementMask movementBit = default;
			if (laneConnection.m_DestEdge != Entity.Null)
			{
				float3 destinationPosition = GetEdgePositionForJunction(
					junctionEntity,
					laneConnection.m_DestEdge,
					edgeLookup,
					edgeGeometryLookup);
				int destinationAxisBin =
					TrafficGroupMovementMappingPolicy.QuantizeUndirectedAxis(
						destinationPosition.x - node.m_Position.x,
						destinationPosition.z - node.m_Position.z);
				movementBit = TrafficGroupMovementMask.FromAxisBins(
					axisBin,
					destinationAxisBin);
			}

			for (int phaseIndex = 0; phaseIndex < phaseCount; phaseIndex++)
			{
				int phaseBit = 1 << phaseIndex;
				if ((laneSignal.m_GroupMask & phaseBit) == 0)
				{
					continue;
				}

				bool isYieldMovement = (extraLaneSignal.m_YieldGroupMask & phaseBit) != 0;
				if (isCarLane)
				{
					roadAxes[phaseIndex] |= axisBit;
					roadMovements[phaseIndex] |= movementBit;
					if (isYieldMovement)
					{
						roadYieldMovements[phaseIndex] |= movementBit;
					}
				}
				if (isTrackLane)
				{
					trackAxes[phaseIndex] |= axisBit;
					trackMovements[phaseIndex] |= movementBit;
					if (isYieldMovement)
					{
						trackYieldMovements[phaseIndex] |= movementBit;
					}
				}
			}
		}

		var signatures = new TrafficGroupPhaseSignature[phaseCount];
		for (int phaseIndex = 0; phaseIndex < phaseCount; phaseIndex++)
		{
			signatures[phaseIndex] = new TrafficGroupPhaseSignature(
				phaseIndex + 1,
				roadAxes[phaseIndex],
				trackAxes[phaseIndex],
				roadMovements[phaseIndex],
				trackMovements[phaseIndex],
				roadYieldMovements[phaseIndex],
				trackYieldMovements[phaseIndex]);
		}
		return signatures;
	}

	private void RemoveMovementMappings(Entity groupEntity)
	{
		var members = GetGroupMembers(groupEntity);
		foreach (Entity memberEntity in members)
		{
			m_LastMovementMappingFailureReports.Remove(memberEntity);
			if (EntityManager.HasComponent<TrafficGroupPhaseMapping>(memberEntity))
			{
				EntityManager.RemoveComponent<TrafficGroupPhaseMapping>(memberEntity);
			}
		}
		members.Dispose();
	}

	public Entity CreateGroup(string name = null)
	{
		if (string.IsNullOrEmpty(name))
		{
			var allGroups = GetAllGroups();
			int groupCount = 0;
			foreach (var group in allGroups)
			{
				groupCount++;
			}
			allGroups.Dispose();
			name = $"Group #{groupCount + 1}";
		}
		
		Entity groupEntity = EntityManager.CreateEntity();
		EntityManager.AddComponentData(groupEntity, new TrafficGroup(isCoordinated: true));
		EntityManager.AddComponentData(groupEntity, new TrafficGroupName(name));

		return groupEntity;
	}

	public bool AddJunctionToGroup(Entity groupEntity, Entity junctionEntity)
	{
		if (!CanAssignTrafficGroupMember(EntityManager, groupEntity, junctionEntity))
		{
			return false;
		}

		int memberCount = GetGroupMemberCount(groupEntity);
		bool isLeader = memberCount == 0;
		Entity leaderEntity = isLeader ? junctionEntity : GetGroupLeader(groupEntity);

		var member = new TrafficGroupMember(groupEntity, leaderEntity, memberCount, 0f, 0f, 0, 0, 0f, isLeader);
		SetOrAddTrafficGroupMember(EntityManager, junctionEntity, member);
		EnsureMemberCustomPhaseSetup(groupEntity, junctionEntity);
		if (isLeader)
		{
			UpdateAllMembersLeader(groupEntity, junctionEntity);
		}
		SyncCycleLengthFromJunction(groupEntity, junctionEntity);
		var group = EntityManager.GetComponentData<TrafficGroup>(groupEntity);
		if (group.m_GreenWaveEnabled)
		{
			CalculateGreenWaveTiming(groupEntity);
		}
		
		if (group.m_IsCoordinated && !isLeader)
		{
			UpdateMasterClock(groupEntity, ref group);
			EntityManager.SetComponentData(groupEntity, group);
			if (group.m_MasterSignalGroupCount > 0)
			{
				PropagateLeaderPhaseChange(groupEntity, group.m_MasterPhase, group.m_MasterState);
			}
		}
		return true;
	}

	private static bool CanAssignTrafficGroupMember(EntityManager entityManager, Entity groupEntity, Entity junctionEntity)
	{
		if (groupEntity == Entity.Null || junctionEntity == Entity.Null)
		{
			return false;
		}

		if (!entityManager.HasComponent<TrafficGroup>(groupEntity))
		{
			return false;
		}

		if (!entityManager.HasComponent<TrafficGroupMember>(junctionEntity))
		{
			return true;
		}

		var existingMember = entityManager.GetComponentData<TrafficGroupMember>(junctionEntity);
		return existingMember.m_GroupEntity == Entity.Null;
	}

	private static void SetOrAddTrafficGroupMember(EntityManager entityManager, Entity junctionEntity, TrafficGroupMember member)
	{
		if (entityManager.HasComponent<TrafficGroupMember>(junctionEntity))
		{
			entityManager.SetComponentData(junctionEntity, member);
			return;
		}

		entityManager.AddComponentData(junctionEntity, member);
	}

	public bool RemoveJunctionFromGroup(Entity junctionEntity)
	{
		if (junctionEntity == Entity.Null)
		{
			return false;
		}

		if (!EntityManager.HasComponent<TrafficGroupMember>(junctionEntity))
		{
			return false;
		}

		var member = EntityManager.GetComponentData<TrafficGroupMember>(junctionEntity);
		Entity groupEntity = member.m_GroupEntity;

		if (EntityManager.HasComponent<TrafficGroupPhaseMapping>(junctionEntity))
		{
			EntityManager.RemoveComponent<TrafficGroupPhaseMapping>(junctionEntity);
		}
		EntityManager.RemoveComponent<TrafficGroupMember>(junctionEntity);

		if (groupEntity != Entity.Null && EntityManager.HasComponent<TrafficGroup>(groupEntity))
		{
			int remainingMembers = GetGroupMemberCount(groupEntity);
			if (remainingMembers == 0)
			{
				EntityManager.DestroyEntity(groupEntity);
				return true;
			}

			if (member.m_IsGroupLeader)
			{
				AssignNewLeader(groupEntity);
			}

			ReindexGroupMembers(groupEntity);
		}

		return true;
	}

	public void DeleteGroup(Entity groupEntity)
	{
		if (groupEntity == Entity.Null || !EntityManager.HasComponent<TrafficGroup>(groupEntity))
		{
			return;
		}

		var members = GetGroupMembers(groupEntity);
		foreach (var memberEntity in members)
		{
			if (EntityManager.HasComponent<TrafficGroupPhaseMapping>(memberEntity))
			{
				EntityManager.RemoveComponent<TrafficGroupPhaseMapping>(memberEntity);
			}
			EntityManager.RemoveComponent<TrafficGroupMember>(memberEntity);
		}
		members.Dispose();

		EntityManager.DestroyEntity(groupEntity);
	}

	public NativeList<Entity> GetGroupMembers(Entity groupEntity)
	{
		var members = new NativeList<Entity>(8, Allocator.Temp);

		if (groupEntity == Entity.Null)
		{
			return members;
		}

		var entities = m_MemberQuery.ToEntityArray(Allocator.Temp);
		var memberComponents = m_MemberQuery.ToComponentDataArray<TrafficGroupMember>(Allocator.Temp);

		for (int i = 0; i < entities.Length; i++)
		{
			if (memberComponents[i].m_GroupEntity == groupEntity)
			{
				members.Add(entities[i]);
			}
		}

		entities.Dispose();
		memberComponents.Dispose();

		return members;
	}

	public int GetGroupMemberCount(Entity groupEntity)
	{
		if (groupEntity == Entity.Null)
		{
			return 0;
		}

		int count = 0;
		var memberComponents = m_MemberQuery.ToComponentDataArray<TrafficGroupMember>(Allocator.Temp);

		for (int i = 0; i < memberComponents.Length; i++)
		{
			if (memberComponents[i].m_GroupEntity == groupEntity)
			{
				count++;
			}
		}

		memberComponents.Dispose();
		return count;
	}

	public NativeArray<Entity> GetAllGroups()
	{
		return m_GroupQuery.ToEntityArray(Allocator.Temp);
	}

	public Entity GetJunctionGroup(Entity junctionEntity)
	{
		if (junctionEntity == Entity.Null)
		{
			return Entity.Null;
		}

		if (!EntityManager.HasComponent<TrafficGroupMember>(junctionEntity))
		{
			return Entity.Null;
		}

		var member = EntityManager.GetComponentData<TrafficGroupMember>(junctionEntity);
		return member.m_GroupEntity;
	}

	public string GetGroupName(Entity groupEntity)
	{
		if (groupEntity == Entity.Null || !EntityManager.HasComponent<TrafficGroupName>(groupEntity))
		{
			return "";
		}

		var groupName = EntityManager.GetComponentData<TrafficGroupName>(groupEntity);
		return groupName.GetName();
	}

	public void SetGroupName(Entity groupEntity, string name)
	{
		if (groupEntity == Entity.Null || !EntityManager.HasComponent<TrafficGroupName>(groupEntity))
		{
			return;
		}

		var groupName = new TrafficGroupName(name);
		EntityManager.SetComponentData(groupEntity, groupName);
	}

	private void AssignNewLeader(Entity groupEntity)
	{
		var members = GetGroupMembers(groupEntity);
		if (members.Length > 0)
		{
			var firstMember = members[0];
			var memberData = EntityManager.GetComponentData<TrafficGroupMember>(firstMember);
			memberData.m_IsGroupLeader = true;
			memberData.m_LeaderEntity = firstMember;
			EntityManager.SetComponentData(firstMember, memberData);
			
			UpdateAllMembersLeader(groupEntity, firstMember);
		}
		members.Dispose();
	}

	private void ReindexGroupMembers(Entity groupEntity)
	{
		var members = GetGroupMembers(groupEntity);
		for (int i = 0; i < members.Length; i++)
		{
			var memberData = EntityManager.GetComponentData<TrafficGroupMember>(members[i]);
			memberData.m_GroupIndex = i;
			EntityManager.SetComponentData(members[i], memberData);
		}
		members.Dispose();
	}

	public Entity GetGroupLeader(Entity groupEntity)
	{
		var members = GetGroupMembers(groupEntity);
		Entity leader = Entity.Null;
		
		foreach (var memberEntity in members)
		{
			var memberData = EntityManager.GetComponentData<TrafficGroupMember>(memberEntity);
			if (memberData.m_IsGroupLeader)
			{
				leader = memberEntity;
				break;
			}
		}
		
		members.Dispose();
		return leader;
	}

	private void UpdateAllMembersLeader(Entity groupEntity, Entity leaderEntity)
	{
		var members = GetGroupMembers(groupEntity);
		
		foreach (var memberEntity in members)
		{
			var memberData = EntityManager.GetComponentData<TrafficGroupMember>(memberEntity);
			memberData.m_LeaderEntity = leaderEntity;
			EntityManager.SetComponentData(memberEntity, memberData);
		}
		
		members.Dispose();
	}

	public void CalculateGreenWaveTiming(Entity groupEntity)
	{
		if (groupEntity == Entity.Null || !EntityManager.HasComponent<TrafficGroup>(groupEntity))
		{
			return;
		}

		var group = EntityManager.GetComponentData<TrafficGroup>(groupEntity);
		if (!group.m_GreenWaveEnabled)
		{
			return;
		}

		Entity leaderEntity = GetGroupLeader(groupEntity);
		if (leaderEntity == Entity.Null || !EntityManager.HasComponent<Game.Net.Node>(leaderEntity))
		{
			return;
		}

		var leaderNode = EntityManager.GetComponentData<Game.Net.Node>(leaderEntity);
		float3 leaderPosition = leaderNode.m_Position;

		var members = GetGroupMembers(groupEntity);

		foreach (var memberEntity in members)
		{
			if (memberEntity == leaderEntity)
			{
				var leaderMember = EntityManager.GetComponentData<TrafficGroupMember>(memberEntity);
				leaderMember.m_MemberCycleTimer = 0f;
				EntityManager.SetComponentData(memberEntity, leaderMember);
				continue;
			}

			if (!EntityManager.HasComponent<Game.Net.Node>(memberEntity))
			{
				continue;
			}

			var memberNode = EntityManager.GetComponentData<Game.Net.Node>(memberEntity);
			float3 memberPosition = memberNode.m_Position;

			float distance = math.distance(leaderPosition, memberPosition);

			float travelTimeSeconds = distance / group.m_GreenWaveSpeed;

			int phaseOffset;
			var memberData = EntityManager.GetComponentData<TrafficGroupMember>(memberEntity);
			if (memberData.m_SignalDelay != 0)
			{
				phaseOffset = memberData.m_SignalDelay;
			}
			else
			{
				phaseOffset = (int)math.round(travelTimeSeconds + group.m_GreenWaveOffset);
			}

			float memberCyclePos = TrafficGroupTimingPolicy.WrapCyclePosition(group.m_CycleTimer, phaseOffset, group.m_CycleLength);

			memberData.m_DistanceToLeader = distance;
			memberData.m_PhaseOffset = phaseOffset;
			memberData.m_MemberCycleTimer = memberCyclePos;
			EntityManager.SetComponentData(memberEntity, memberData);

		}

		members.Dispose();
	}

	public void SetGreenWaveEnabled(Entity groupEntity, bool enabled)
	{
		if (groupEntity == Entity.Null || !EntityManager.HasComponent<TrafficGroup>(groupEntity))
		{
			return;
		}

		var group = EntityManager.GetComponentData<TrafficGroup>(groupEntity);
		group.m_GreenWaveEnabled = enabled;
		EntityManager.SetComponentData(groupEntity, group);

		if (enabled)
		{
			Entity leaderEntity = GetGroupLeader(groupEntity);
			if (leaderEntity != Entity.Null && EntityManager.HasBuffer<CustomPhaseData>(leaderEntity) && 
			    EntityManager.TryGetBuffer<CustomPhaseData>(leaderEntity, false ,out var phases) && phases.Length > 0)
			{
				CalculateEnhancedGreenWaveTiming(groupEntity);
			}
			else
			{
				CalculateGreenWaveTiming(groupEntity);
			}
			if (group.m_IsCoordinated)
			{
				UpdateMasterClock(groupEntity, ref group);
				EntityManager.SetComponentData(groupEntity, group);
				if (group.m_MasterSignalGroupCount > 0)
				{
					PropagateLeaderPhaseChange(groupEntity, group.m_MasterPhase, group.m_MasterState);
				}
			}
		}
	}

	public void SetGreenWaveSpeed(Entity groupEntity, float speed)
	{
		if (groupEntity == Entity.Null || !EntityManager.HasComponent<TrafficGroup>(groupEntity))
		{
			return;
		}

		var group = EntityManager.GetComponentData<TrafficGroup>(groupEntity);
		group.m_GreenWaveSpeed = math.max(1f, speed);
		EntityManager.SetComponentData(groupEntity, group);

		if (group.m_GreenWaveEnabled)
		{
			Entity leaderEntity = GetGroupLeader(groupEntity);
			if (leaderEntity != Entity.Null && EntityManager.HasBuffer<CustomPhaseData>(leaderEntity) && 
			    EntityManager.TryGetBuffer<CustomPhaseData>(leaderEntity, false ,out var phases) && phases.Length > 0)
			{
				CalculateEnhancedGreenWaveTiming(groupEntity);
			}
			else
			{
				CalculateGreenWaveTiming(groupEntity);
			}
			
			if (group.m_IsCoordinated)
			{
				UpdateMasterClock(groupEntity, ref group);
				EntityManager.SetComponentData(groupEntity, group);
				if (group.m_MasterSignalGroupCount > 0)
				{
					PropagateLeaderPhaseChange(groupEntity, group.m_MasterPhase, group.m_MasterState);
				}
			}
		}
	}

	public void SetGreenWaveOffset(Entity groupEntity, float offset)
	{
		if (groupEntity == Entity.Null || !EntityManager.HasComponent<TrafficGroup>(groupEntity))
		{
			return;
		}

		var group = EntityManager.GetComponentData<TrafficGroup>(groupEntity);
		group.m_GreenWaveOffset = offset;
		EntityManager.SetComponentData(groupEntity, group);

		if (group.m_GreenWaveEnabled)
		{
			Entity leaderEntity = GetGroupLeader(groupEntity);
			if (leaderEntity != Entity.Null && EntityManager.HasBuffer<CustomPhaseData>(leaderEntity) && 
			    EntityManager.TryGetBuffer<CustomPhaseData>(leaderEntity, false ,out var phases) && phases.Length > 0)
			{
				CalculateEnhancedGreenWaveTiming(groupEntity);
			}
			else
			{
				CalculateGreenWaveTiming(groupEntity);
			}
			
			if (group.m_IsCoordinated)
			{
				UpdateMasterClock(groupEntity, ref group);
				EntityManager.SetComponentData(groupEntity, group);
				if (group.m_MasterSignalGroupCount > 0)
				{
					PropagateLeaderPhaseChange(groupEntity, group.m_MasterPhase, group.m_MasterState);
				}
			}
		}

	}

	public void SetSignalDelay(Entity groupEntity, Entity memberEntity, int signalDelay)
	{
		if (groupEntity == Entity.Null || memberEntity == Entity.Null || !EntityManager.HasComponent<TrafficGroupMember>(memberEntity))
		{
			return;
		}

		var memberData = EntityManager.GetComponentData<TrafficGroupMember>(memberEntity);
		memberData.m_SignalDelay = signalDelay;
		EntityManager.SetComponentData(memberEntity, memberData);

		if (EntityManager.HasComponent<TrafficGroup>(groupEntity))
		{
			var group = EntityManager.GetComponentData<TrafficGroup>(groupEntity);
			if (group.m_GreenWaveEnabled)
			{
				Entity leaderEntity = GetGroupLeader(groupEntity);
				if (leaderEntity != Entity.Null && EntityManager.HasBuffer<CustomPhaseData>(leaderEntity) && 
				    EntityManager.TryGetBuffer<CustomPhaseData>(leaderEntity, false ,out var phases) && phases.Length > 0)
				{
					CalculateEnhancedGreenWaveTiming(groupEntity);
				}
				else
				{
					CalculateGreenWaveTiming(groupEntity);
				}
			}
		}
	}

	public void CalculateSignalDelays(Entity groupEntity)
	{
		if (groupEntity == Entity.Null || !EntityManager.HasComponent<TrafficGroup>(groupEntity))
		{
			return;
		}

		var group = EntityManager.GetComponentData<TrafficGroup>(groupEntity);
		var members = GetGroupMembers(groupEntity);

		
		Entity leaderEntity = Entity.Null;
		float3 leaderPosition = float3.zero;
		
		foreach (var memberEntity in members)
		{
			var memberData = EntityManager.GetComponentData<TrafficGroupMember>(memberEntity);
			if (memberData.m_IsGroupLeader)
			{
				leaderEntity = memberEntity;
				if (EntityManager.HasComponent<Game.Net.Node>(leaderEntity))
				{
					var leaderNode = EntityManager.GetComponentData<Game.Net.Node>(leaderEntity);
					leaderPosition = leaderNode.m_Position;
				}
				break;
			}
		}

		if (leaderEntity == Entity.Null)
		{
			members.Dispose();
			return;
		}

		foreach (var memberEntity in members)
		{
			if (memberEntity == leaderEntity)
			{
				var leaderMemberData = EntityManager.GetComponentData<TrafficGroupMember>(memberEntity);
				leaderMemberData.m_SignalDelay = 0;
				EntityManager.SetComponentData(memberEntity, leaderMemberData);
				continue;
			}

			if (!EntityManager.HasComponent<Game.Net.Node>(memberEntity))
			{
				continue;
			}

			var memberNode = EntityManager.GetComponentData<Game.Net.Node>(memberEntity);
			float3 memberPosition = memberNode.m_Position;

			float distance = math.distance(leaderPosition, memberPosition);
			float travelTimeSeconds = distance / group.m_GreenWaveSpeed;
			int calculatedDelay = (int)math.round(travelTimeSeconds + group.m_GreenWaveOffset);

			var memberData = EntityManager.GetComponentData<TrafficGroupMember>(memberEntity);
			memberData.m_SignalDelay = calculatedDelay;
			EntityManager.SetComponentData(memberEntity, memberData);

		}

		CalculateGreenWaveTiming(groupEntity);

		members.Dispose();
	}

	public void SetCoordinated(Entity groupEntity, bool coordinated)
	{
		if (groupEntity == Entity.Null || !EntityManager.HasComponent<TrafficGroup>(groupEntity))
		{
			return;
		}

		var group = EntityManager.GetComponentData<TrafficGroup>(groupEntity);
		group.m_IsCoordinated = coordinated;
		
		if (coordinated)
		{
			group.m_LastSyncTime = 0f;
			group.m_CycleTimer = 0f;
			
			
			UpdateMasterClock(groupEntity, ref group);
			if (group.m_MasterSignalGroupCount > 0)
			{
				PropagateLeaderPhaseChange(groupEntity, group.m_MasterPhase, group.m_MasterState);
			}
		}
		
		EntityManager.SetComponentData(groupEntity, group);

	}

	private void UpdateMasterClock(Entity groupEntity, ref TrafficGroup group)
	{
		Entity leaderEntity = GetGroupLeader(groupEntity);
		if (leaderEntity == Entity.Null)
		{
			return;
		}

		if (EntityManager.HasComponent<TrafficLights>(leaderEntity))
		{
			var leaderLights = EntityManager.GetComponentData<TrafficLights>(leaderEntity);
			group.m_MasterPhase = leaderLights.m_CurrentSignalGroup;
			group.m_MasterNextPhase = leaderLights.m_NextSignalGroup;
			group.m_MasterState = leaderLights.m_State;
			group.m_MasterTimer = leaderLights.m_Timer;
			group.m_MasterSignalGroupCount = leaderLights.m_SignalGroupCount;
		}

		if (EntityManager.HasComponent<CustomTrafficLights>(leaderEntity))
		{
			var leaderCustom = EntityManager.GetComponentData<CustomTrafficLights>(leaderEntity);
			group.m_MasterCustomTimer = leaderCustom.m_Timer;
		}
	}

	private void ApplyCoordination(Entity groupEntity, TrafficGroup group)
	{
		if (group.m_CycleLength <= 0 || group.m_MasterSignalGroupCount == 0)
		{
			return;
		}

		// TMPE-style lockstep: when green wave is off, the job-level
		// SyncSignalGroupWithLeader handles sync directly by copying
		// master state to followers. No main-thread nudging needed.
		if (!group.m_GreenWaveEnabled)
		{
			return;
		}

		// Green wave mode: update member cycle timers for offset-based staggering
		var members = GetGroupMembers(groupEntity);
		
		foreach (var memberEntity in members)
		{
			var memberData = EntityManager.GetComponentData<TrafficGroupMember>(memberEntity);
			if (memberData.m_IsGroupLeader)
			{
				continue;
			}

			if (!EntityManager.HasComponent<TrafficLights>(memberEntity))
			{
				continue;
			}

			if (memberData.m_SignalDelay == 0)
			{
				continue;
			}

			float memberCyclePos = TrafficGroupTimingPolicy.WrapCyclePosition(group.m_CycleTimer, memberData.m_SignalDelay, group.m_CycleLength);

			memberData.m_MemberCycleTimer = memberCyclePos;
			EntityManager.SetComponentData(memberEntity, memberData);
		}

		members.Dispose();
	}

	private bool TryMapLeaderPhase(
		Entity memberEntity,
		int leaderPhase,
		out int memberPhase)
	{
		if (EntityManager.TryGetComponent(
			    memberEntity,
			    out TrafficGroupPhaseMapping phaseMapping)
		    && phaseMapping.m_Map.IsComplete
		    && phaseMapping.m_Map.TryMapLeaderToMember(leaderPhase, out memberPhase))
		{
			return true;
		}

		memberPhase = 0;
		return false;
	}

	
	public float CalculateCycleLengthFromJunction(Entity junctionEntity)
	{
		if (junctionEntity == Entity.Null)
		{
			return 0f;
		}

		if (!EntityManager.HasBuffer<CustomPhaseData>(junctionEntity))
		{
			return 0f;
		}

		EntityManager.TryGetBuffer<CustomPhaseData>(junctionEntity, false, out var phaseBuffer);
		if (phaseBuffer.Length == 0)
		{
			return 0f;
		}

		float totalCycleLength = 0f;
		for (int i = 0; i < phaseBuffer.Length; i++)
		{
			var phase = phaseBuffer[i];
			totalCycleLength += phase.m_MaximumDuration;
		}

		return totalCycleLength;
	}

	
	private void SyncCycleLengthFromJunction(Entity groupEntity, Entity junctionEntity)
	{
		if (groupEntity == Entity.Null || junctionEntity == Entity.Null)
		{
			return;
		}

		float junctionCycleLength = CalculateCycleLengthFromJunction(junctionEntity);
		if (junctionCycleLength <= 0)
		{
			return; 
		}

		var group = EntityManager.GetComponentData<TrafficGroup>(groupEntity);
		
		if (EntityManager.HasComponent<TrafficGroupMember>(junctionEntity))
		{
			var member = EntityManager.GetComponentData<TrafficGroupMember>(junctionEntity);
			if (member.m_IsGroupLeader)
			{
				group.m_CycleLength = junctionCycleLength;
				EntityManager.SetComponentData(groupEntity, group);
				return;
			}
		}

		
		float cycleDifference = math.abs(group.m_CycleLength - junctionCycleLength);
		
	}

	
	public void RecalculateGroupCycleLength(Entity groupEntity)
	{
		if (groupEntity == Entity.Null || !EntityManager.HasComponent<TrafficGroup>(groupEntity))
		{
			return;
		}

		Entity leaderEntity = GetGroupLeader(groupEntity);
		if (leaderEntity == Entity.Null)
		{
			return;
		}

		float leaderCycleLength = CalculateCycleLengthFromJunction(leaderEntity);
		if (leaderCycleLength <= 0)
		{
			return;
		}

		var group = EntityManager.GetComponentData<TrafficGroup>(groupEntity);
		group.m_CycleLength = leaderCycleLength;
		EntityManager.SetComponentData(groupEntity, group);


		var members = GetGroupMembers(groupEntity);
		foreach (var memberEntity in members)
		{
			if (memberEntity == leaderEntity)
			{
				continue;
			}

			float memberCycleLength = CalculateCycleLengthFromJunction(memberEntity);
			if (memberCycleLength > 0)
			{
				float cycleDifference = math.abs(leaderCycleLength - memberCycleLength);
				
			}
		}
		members.Dispose();
	}
	
	public Dictionary<Entity, (float cycleLength, bool isCompatible)> GetGroupCycleLengthInfo(Entity groupEntity)
	{
		var result = new Dictionary<Entity, (float, bool)>();
		
		if (groupEntity == Entity.Null || !EntityManager.HasComponent<TrafficGroup>(groupEntity))
		{
			return result;
		}

		var group = EntityManager.GetComponentData<TrafficGroup>(groupEntity);
		float targetCycleLength = group.m_CycleLength;

		var members = GetGroupMembers(groupEntity);
		foreach (var memberEntity in members)
		{
			float memberCycleLength = CalculateCycleLengthFromJunction(memberEntity);
			bool isCompatible = memberCycleLength <= 0 || math.abs(targetCycleLength - memberCycleLength) <= 2f;
			result[memberEntity] = (memberCycleLength, isCompatible);
		}
		members.Dispose();

		return result;
	}

	

	
	public void CalculateEnhancedGreenWaveTiming(Entity groupEntity, int mainPhaseIndex = 0)
	{
		if (groupEntity == Entity.Null || !EntityManager.HasComponent<TrafficGroup>(groupEntity))
		{
			return;
		}

		var group = EntityManager.GetComponentData<TrafficGroup>(groupEntity);
		Entity leaderEntity = GetGroupLeader(groupEntity);
		
		if (leaderEntity == Entity.Null || !EntityManager.HasComponent<Node>(leaderEntity))
		{
			return;
		}

		var leaderNode = EntityManager.GetComponentData<Node>(leaderEntity);
		float3 leaderPosition = leaderNode.m_Position;

		float leaderCycleLength = CalculateCycleLengthFromJunction(leaderEntity);
		if (leaderCycleLength <= 0)
		{
			CalculateGreenWaveTiming(groupEntity);
			return;
		}

		float mainPhaseStartTime = 0f;
		if (EntityManager.HasBuffer<CustomPhaseData>(leaderEntity))
		{
			EntityManager.TryGetBuffer<CustomPhaseData>(leaderEntity, false, out var leaderPhases);
			for (int i = 0; i < math.min(mainPhaseIndex, leaderPhases.Length); i++)
			{
				mainPhaseStartTime += leaderPhases[i].m_MaximumDuration;
			}
		}

		var members = GetGroupMembers(groupEntity);

		foreach (var memberEntity in members)
		{
			if (memberEntity == leaderEntity)
			{
				
				var leaderMember = EntityManager.GetComponentData<TrafficGroupMember>(memberEntity);
				leaderMember.m_PhaseOffset = 0;
				leaderMember.m_SignalDelay = 0;
				leaderMember.m_MemberCycleTimer = 0f;
				EntityManager.SetComponentData(memberEntity, leaderMember);
				continue;
			}

			if (!EntityManager.HasComponent<Node>(memberEntity))
			{
				continue;
			}

			var memberNode = EntityManager.GetComponentData<Node>(memberEntity);
			float3 memberPosition = memberNode.m_Position;
			float distance = math.distance(leaderPosition, memberPosition);

			float travelTimeSeconds = distance / group.m_GreenWaveSpeed;
			
			int signalDelay = (int)math.round(travelTimeSeconds + group.m_GreenWaveOffset);
			
			float arrivalTime = mainPhaseStartTime + signalDelay;
			int phaseOffset = TrafficGroupTimingPolicy.CalculateZeroBasedPhaseOffset(arrivalTime, leaderCycleLength, GetPhaseCount(memberEntity));

			float memberCyclePos = TrafficGroupTimingPolicy.WrapCyclePosition(group.m_CycleTimer, signalDelay, leaderCycleLength);

			var memberData = EntityManager.GetComponentData<TrafficGroupMember>(memberEntity);
			memberData.m_DistanceToLeader = distance;
			memberData.m_PhaseOffset = phaseOffset;
			memberData.m_SignalDelay = signalDelay;
			memberData.m_MemberCycleTimer = memberCyclePos;
			EntityManager.SetComponentData(memberEntity, memberData);

		}

		members.Dispose();
	}

	private int GetPhaseCount(Entity junctionEntity)
	{
		if (EntityManager.HasBuffer<CustomPhaseData>(junctionEntity))
		{
			return EntityManager.TryGetBuffer<CustomPhaseData>(junctionEntity, false, out var phases) ? phases.Length : 0;
		}
		return 1;
	}

	// Determines which phase (1-indexed) a member should be in based on its position in the cycle.
	// Walks the CustomPhaseData durations to find which phase window the cyclePosition falls into.
	private int DeterminePhaseFromCyclePosition(Entity memberEntity, float cyclePosition, float cycleLength)
	{
		if (!EntityManager.HasBuffer<CustomPhaseData>(memberEntity) ||
			!EntityManager.TryGetBuffer<CustomPhaseData>(memberEntity, true, out var phases) ||
			phases.Length == 0)
		{
			// Fallback: evenly divide cycle among signal groups
			if (EntityManager.HasComponent<TrafficLights>(memberEntity))
			{
				var tl = EntityManager.GetComponentData<TrafficLights>(memberEntity);
				if (tl.m_SignalGroupCount > 0)
				{
					return TrafficGroupTimingPolicy.DetermineOneBasedPhaseFromEvenCycle(cyclePosition, cycleLength, tl.m_SignalGroupCount);
				}
			}
			return 1;
		}

		float accumulated = 0f;
		for (int i = 0; i < phases.Length; i++)
		{
			accumulated += phases[i].m_MaximumDuration;
			if (cyclePosition < accumulated)
			{
				return i + 1; // 1-indexed
			}
		}
		return 1; // Wrapped past end, back to first phase
	}

	public void PropagateLeaderPhaseChange(Entity groupEntity, byte newPhase, TrafficLightState newState)
	{
		if (groupEntity == Entity.Null || !EntityManager.HasComponent<TrafficGroup>(groupEntity))
		{
			return;
		}

		var group = EntityManager.GetComponentData<TrafficGroup>(groupEntity);
		if (!group.m_IsCoordinated)
		{
			return;
		}

		group.m_MasterPhase = newPhase;
		group.m_MasterState = newState;
		EntityManager.SetComponentData(groupEntity, group);

		// When green wave is enabled, let the job-level sync handle staggered timing
		if (group.m_GreenWaveEnabled)
		{
			return;
		}

		var members = GetGroupMembers(groupEntity);

		foreach (var memberEntity in members)
		{
			var memberData = EntityManager.GetComponentData<TrafficGroupMember>(memberEntity);
			if (memberData.m_IsGroupLeader)
			{
				continue;
			}

			if (!EntityManager.HasComponent<TrafficLights>(memberEntity))
			{
				continue;
			}

			if (!TryMapLeaderPhase(memberEntity, group.m_MasterPhase, out int mappedPhase))
			{
				continue;
			}

			var trafficLights = EntityManager.GetComponentData<TrafficLights>(memberEntity);
			
			if (trafficLights.m_CurrentSignalGroup != mappedPhase)
			{
				trafficLights.m_NextSignalGroup = (byte)mappedPhase;
				
				if (trafficLights.m_State == TrafficLightState.Ongoing)
				{
					trafficLights.m_State = TrafficLightState.Ending;
				}
			}

			EntityManager.SetComponentData(memberEntity, trafficLights);
		}

		members.Dispose();
	}

	
	public void ForceSyncToLeader(Entity groupEntity)
	{
		if (groupEntity == Entity.Null || !EntityManager.HasComponent<TrafficGroup>(groupEntity))
		{
			return;
		}

		var group = EntityManager.GetComponentData<TrafficGroup>(groupEntity);
		UpdateMasterClock(groupEntity, ref group);
		EntityManager.SetComponentData(groupEntity, group);
		Entity leaderEntity = GetGroupLeader(groupEntity);
		RefreshMovementMappings(groupEntity, leaderEntity);

		if (group.m_MasterSignalGroupCount == 0)
		{
			return;
		}

		var members = GetGroupMembers(groupEntity);

		foreach (var memberEntity in members)
		{
			var memberData = EntityManager.GetComponentData<TrafficGroupMember>(memberEntity);
			if (memberData.m_IsGroupLeader)
			{
				continue;
			}

			if (!EntityManager.HasComponent<TrafficLights>(memberEntity))
			{
				continue;
			}

			// Initialize the per-member cycle timer from the group timer and signal delay
			if (group.m_GreenWaveEnabled && group.m_CycleLength > 0)
			{
				memberData.m_MemberCycleTimer = TrafficGroupTimingPolicy.WrapCyclePosition(
					group.m_CycleTimer, memberData.m_SignalDelay, group.m_CycleLength);
				EntityManager.SetComponentData(memberEntity, memberData);
			}

			var trafficLights = EntityManager.GetComponentData<TrafficLights>(memberEntity);

			int adjustedPhase;
			if (group.m_GreenWaveEnabled && memberData.m_SignalDelay != 0 && group.m_CycleLength > 0)
			{
				adjustedPhase = DeterminePhaseFromCyclePosition(
					memberEntity, memberData.m_MemberCycleTimer, group.m_CycleLength);
				trafficLights.m_CurrentSignalGroup = (byte)adjustedPhase;
				trafficLights.m_State = group.m_MasterState;
				int adjustedTimer = group.m_MasterTimer - memberData.m_SignalDelay;
				trafficLights.m_Timer = (byte)math.clamp(adjustedTimer, 0, 255);
			}
			else
			{
				if (!TryMapLeaderPhase(memberEntity, group.m_MasterPhase, out adjustedPhase))
				{
					continue;
				}
				trafficLights.m_CurrentSignalGroup = (byte)adjustedPhase;
				trafficLights.m_State = group.m_MasterState;
				trafficLights.m_Timer = group.m_MasterTimer;
			}

			EntityManager.SetComponentData(memberEntity, trafficLights);
		}

		members.Dispose();
	}

	

	#region Group Management Extensions

	
	public void JoinGroups(Entity targetGroupEntity, Entity sourceGroupEntity)
	{
		if (targetGroupEntity == Entity.Null || sourceGroupEntity == Entity.Null)
		{
			var messageDialog = new MessageDialog(LocaleHelper.Translate("UI.LABEL[C2VM.TrafficLightsEnhancement.JoinGroupNullEntity]", "Cannot join - null entity provided"));
			GameManager.instance.userInterface.appBindings.ShowMessageDialog(messageDialog, null);
			return;
		}

		if (!EntityManager.HasComponent<TrafficGroup>(targetGroupEntity) || 
		    !EntityManager.HasComponent<TrafficGroup>(sourceGroupEntity))
		{
			var messageDialog = new MessageDialog(LocaleHelper.Translate("UI.LABEL[C2VM.TrafficLightsEnhancement.JoinGroupInvalidGroups]", "One or both entities are not valid groups"));
			GameManager.instance.userInterface.appBindings.ShowMessageDialog(messageDialog, null);
			return;
		}

		if (targetGroupEntity == sourceGroupEntity)
		{
			var messageDialog = new MessageDialog(LocaleHelper.Translate("UI.LABEL[C2VM.TrafficLightsEnhancement.JoinGroupItself]", "Cannot join a group with itself"));
			GameManager.instance.userInterface.appBindings.ShowMessageDialog(messageDialog, null);
			return;
		}

		var targetGroup = EntityManager.GetComponentData<TrafficGroup>(targetGroupEntity);
		var sourceGroup = EntityManager.GetComponentData<TrafficGroup>(sourceGroupEntity);

		var targetMembers = GetGroupMembers(targetGroupEntity);
		var sourceMembers = GetGroupMembers(sourceGroupEntity);

		int targetCount = targetMembers.Length;
		int sourceCount = sourceMembers.Length;
		int totalCount = targetCount + sourceCount;

		if (totalCount == 0)
		{
			targetMembers.Dispose();
			sourceMembers.Dispose();
			return;
		}

		float avgCycleLength = (targetGroup.m_CycleLength * targetCount + sourceGroup.m_CycleLength * sourceCount) / totalCount;
		targetGroup.m_CycleLength = avgCycleLength;

		targetGroup.m_GreenWaveSpeed = (targetGroup.m_GreenWaveSpeed * targetCount + sourceGroup.m_GreenWaveSpeed * sourceCount) / totalCount;
		targetGroup.m_GreenWaveOffset = (targetGroup.m_GreenWaveOffset * targetCount + sourceGroup.m_GreenWaveOffset * sourceCount) / totalCount;
		targetGroup.m_GreenWaveEnabled = targetGroup.m_GreenWaveEnabled || sourceGroup.m_GreenWaveEnabled;

		EntityManager.SetComponentData(targetGroupEntity, targetGroup);

		Entity targetLeader = GetGroupLeader(targetGroupEntity);

		int newIndex = targetCount;
		foreach (var memberEntity in sourceMembers)
		{
			var memberData = EntityManager.GetComponentData<TrafficGroupMember>(memberEntity);
			memberData.m_GroupEntity = targetGroupEntity;
			memberData.m_LeaderEntity = targetLeader;
			memberData.m_GroupIndex = newIndex++;
			memberData.m_IsGroupLeader = false; 
			EntityManager.SetComponentData(memberEntity, memberData);
		}

		targetMembers.Dispose();
		sourceMembers.Dispose();

		EntityManager.DestroyEntity(sourceGroupEntity);

		if (targetGroup.m_GreenWaveEnabled)
		{
			Entity leaderEntity = GetGroupLeader(targetGroupEntity);
			if (leaderEntity != Entity.Null && EntityManager.HasBuffer<CustomPhaseData>(leaderEntity) && 
			    EntityManager.TryGetBuffer<CustomPhaseData>(leaderEntity, false, out var phases) && phases.Length > 0)
			{
				CalculateEnhancedGreenWaveTiming(targetGroupEntity);
			}
			else
			{
				CalculateGreenWaveTiming(targetGroupEntity);
			}
		}

		m_Log.Info($"TrafficGroupSystem: Joined groups - {sourceCount} members moved to target group (now {totalCount} members)");
	}

	
	public bool SetGroupLeader(Entity groupEntity, Entity newLeaderEntity)
	{
		if (groupEntity == Entity.Null || newLeaderEntity == Entity.Null)
		{
			return false;
		}

		if (!EntityManager.HasComponent<TrafficGroupMember>(newLeaderEntity))
		{
			m_Log.Warn($"Entity {newLeaderEntity} is not a group member");
			return false;
		}

		var newLeaderMember = EntityManager.GetComponentData<TrafficGroupMember>(newLeaderEntity);
		if (newLeaderMember.m_GroupEntity != groupEntity)
		{
			m_Log.Warn($"Entity {newLeaderEntity} is not in group {groupEntity}");
			return false;
		}

		var members = GetGroupMembers(groupEntity);
		foreach (var memberEntity in members)
		{
			var memberData = EntityManager.GetComponentData<TrafficGroupMember>(memberEntity);
			if (memberData.m_IsGroupLeader)
			{
				memberData.m_IsGroupLeader = false;
				EntityManager.SetComponentData(memberEntity, memberData);
			}
		}

		newLeaderMember.m_IsGroupLeader = true;
		newLeaderMember.m_LeaderEntity = newLeaderEntity;
		newLeaderMember.m_PhaseOffset = 0;
		newLeaderMember.m_SignalDelay = 0;
		newLeaderMember.m_DistanceToLeader = 0f;
		EntityManager.SetComponentData(newLeaderEntity, newLeaderMember);

		UpdateAllMembersLeader(groupEntity, newLeaderEntity);

		members.Dispose();

		RecalculateGroupCycleLength(groupEntity);

		var group = EntityManager.GetComponentData<TrafficGroup>(groupEntity);
		if (group.m_GreenWaveEnabled)
		{
			if (EntityManager.HasBuffer<CustomPhaseData>(newLeaderEntity) && 
			    EntityManager.TryGetBuffer<CustomPhaseData>(newLeaderEntity, false, out var phases) && phases.Length > 0)
			{
				CalculateEnhancedGreenWaveTiming(groupEntity);
			}
			else
			{
				CalculateGreenWaveTiming(groupEntity);
			}
		}

		return true;
	}

	
	public void SkipStep(Entity groupEntity)
	{
		if (groupEntity == Entity.Null || !EntityManager.HasComponent<TrafficGroup>(groupEntity))
		{
			return;
		}

		var members = GetGroupMembers(groupEntity);

		foreach (var memberEntity in members)
		{
			if (!EntityManager.HasComponent<TrafficLights>(memberEntity))
			{
				continue;
			}

			var trafficLights = EntityManager.GetComponentData<TrafficLights>(memberEntity);

			int nextPhase = trafficLights.m_CurrentSignalGroup + 1;
			if (nextPhase > trafficLights.m_SignalGroupCount)
			{
				nextPhase = 1;
			}

			trafficLights.m_NextSignalGroup = (byte)nextPhase;
			trafficLights.m_State = TrafficLightState.Ending;
			trafficLights.m_Timer = 0;

			EntityManager.SetComponentData(memberEntity, trafficLights);

			if (EntityManager.HasComponent<CustomTrafficLights>(memberEntity))
			{
				var customLights = EntityManager.GetComponentData<CustomTrafficLights>(memberEntity);
				customLights.m_Timer = 0;
				EntityManager.SetComponentData(memberEntity, customLights);
			}
		}

		members.Dispose();
	}

	public void EnsureMemberCustomPhaseSetup(Entity groupEntity, Entity memberEntity)
	{
		if (memberEntity == Entity.Null || !EntityManager.Exists(memberEntity))
		{
			return;
		}

		EnsureCustomPhaseComponents(memberEntity);

		Entity leaderEntity = groupEntity != Entity.Null
			&& EntityManager.HasComponent<TrafficGroup>(groupEntity)
				? GetGroupLeader(groupEntity)
				: Entity.Null;
		DynamicBuffer<CustomPhaseData> leaderPhases = default;
		bool leaderHasCustomPhases = leaderEntity != Entity.Null
			&& EntityManager.HasComponent<CustomTrafficLights>(leaderEntity)
			&& EntityManager.GetComponentData<CustomTrafficLights>(leaderEntity)
				.GetPatternOnly() == CustomTrafficLights.Patterns.CustomPhase
			&& EntityManager.TryGetBuffer(
				leaderEntity,
				true,
				out leaderPhases)
			&& leaderPhases.Length > 0;

		var memberPhases = EntityManager.GetBuffer<CustomPhaseData>(memberEntity);
		if (leaderHasCustomPhases)
		{
			EnsureMemberUsesCustomPhases(memberEntity);
			for (int i = memberPhases.Length; i < leaderPhases.Length; i++)
			{
				CustomPhaseData leaderPhase = leaderPhases[i];
				var memberPhase = new CustomPhaseData
				{
					m_MinimumDuration = leaderPhase.m_MinimumDuration,
					m_MaximumDuration = leaderPhase.m_MaximumDuration
				};
				memberPhases.Add(memberPhase);
			}
		}
		else if (memberPhases.Length == 0)
		{
			memberPhases.Add(new CustomPhaseData());
		}

		EnsureTopologyLocalEdgeMasks(memberEntity);
		MarkMemberUpdated(memberEntity);
	}

	private void EnsureCustomPhaseComponents(Entity memberEntity)
	{
		if (!EntityManager.HasComponent<CustomTrafficLights>(memberEntity))
		{
			EntityManager.AddComponentData(
				memberEntity,
				new CustomTrafficLights(CustomTrafficLights.Patterns.Vanilla));
		}

		var customTrafficLights =
			EntityManager.GetComponentData<CustomTrafficLights>(memberEntity);
		var currentMode = customTrafficLights.GetMode();
		if (currentMode != CustomTrafficLights.TrafficMode.Dynamic
			&& currentMode != CustomTrafficLights.TrafficMode.FixedTimed)
		{
			customTrafficLights.SetMode(CustomTrafficLights.TrafficMode.Dynamic);
		}
		customTrafficLights.m_Timer = 0;
		EntityManager.SetComponentData(memberEntity, customTrafficLights);

		if (!EntityManager.HasBuffer<CustomPhaseData>(memberEntity))
		{
			EntityManager.AddBuffer<CustomPhaseData>(memberEntity);
		}
		if (!EntityManager.HasBuffer<EdgeGroupMask>(memberEntity))
		{
			EntityManager.AddBuffer<EdgeGroupMask>(memberEntity);
		}
		if (!EntityManager.HasBuffer<SubLaneGroupMask>(memberEntity))
		{
			EntityManager.AddBuffer<SubLaneGroupMask>(memberEntity);
		}
	}

	private void EnsureMemberUsesCustomPhases(Entity memberEntity)
	{
		var customTrafficLights =
			EntityManager.GetComponentData<CustomTrafficLights>(memberEntity);
		customTrafficLights.SetPatternOnly(
			CustomTrafficLights.Patterns.CustomPhase);
		var currentMode = customTrafficLights.GetMode();
		if (currentMode != CustomTrafficLights.TrafficMode.Dynamic
			&& currentMode != CustomTrafficLights.TrafficMode.FixedTimed)
		{
			customTrafficLights.SetMode(CustomTrafficLights.TrafficMode.Dynamic);
		}
		customTrafficLights.m_Timer = 0;
		EntityManager.SetComponentData(memberEntity, customTrafficLights);
	}

	private void EnsureTopologyLocalEdgeMasks(Entity memberEntity)
	{
		if (!EntityManager.HasBuffer<ConnectedEdge>(memberEntity))
		{
			return;
		}

		var connectedEdges = EntityManager.GetBuffer<ConnectedEdge>(memberEntity);
		var edgeMasks = EntityManager.GetBuffer<EdgeGroupMask>(memberEntity);
		var edgeLookup = GetComponentLookup<Edge>(true);
		var edgeGeometryLookup = GetComponentLookup<EdgeGeometry>(true);

		foreach (ConnectedEdge connectedEdge in connectedEdges)
		{
			Entity edgeEntity = connectedEdge.m_Edge;
			bool edgeFound = false;
			for (int i = 0; i < edgeMasks.Length; i++)
			{
				if (edgeMasks[i].m_Edge == edgeEntity)
				{
					edgeFound = true;
					break;
				}
			}

			if (!edgeFound)
			{
				float3 edgePosition = NodeUtils.GetEdgePosition(
					memberEntity,
					edgeEntity,
					edgeLookup,
					edgeGeometryLookup);
				edgeMasks.Add(new EdgeGroupMask(edgeEntity, edgePosition));
			}
		}
	}

	private void MarkMemberUpdated(Entity memberEntity)
	{
		if (!EntityManager.HasComponent<Updated>(memberEntity))
		{
			EntityManager.AddComponentData(memberEntity, default(Updated));
		}
	}

	public void MatchPhaseDurationsToLeader(Entity groupEntity)
	{
		if (groupEntity == Entity.Null
			|| !EntityManager.HasComponent<TrafficGroup>(groupEntity))
		{
			return;
		}

		Entity leaderEntity = GetGroupLeader(groupEntity);
		if (leaderEntity == Entity.Null
			|| !EntityManager.TryGetBuffer(
				leaderEntity,
				true,
				out DynamicBuffer<CustomPhaseData> leaderPhases)
			|| leaderPhases.Length == 0)
		{
			m_Log.Warn("Leader has no phases");
			return;
		}

		var members = GetGroupMembers(groupEntity);
		foreach (Entity memberEntity in members)
		{
			if (memberEntity == leaderEntity)
			{
				continue;
			}

			EnsureMemberCustomPhaseSetup(groupEntity, memberEntity);
			var memberPhases =
				EntityManager.GetBuffer<CustomPhaseData>(memberEntity);
			int phaseCount = math.min(leaderPhases.Length, memberPhases.Length);
			for (int i = 0; i < phaseCount; i++)
			{
				CustomPhaseData memberPhase = memberPhases[i];
				CustomPhaseData leaderPhase = leaderPhases[i];
				memberPhase.m_MinimumDuration =
					leaderPhase.m_MinimumDuration;
				memberPhase.m_MaximumDuration =
					leaderPhase.m_MaximumDuration;
				memberPhases[i] = memberPhase;
			}

			MarkMemberUpdated(memberEntity);
		}

		members.Dispose();
		RecalculateGroupCycleLength(groupEntity);
	}

	public void PropagatePatternToMembers(
		Entity groupEntity,
		CustomTrafficLights.Patterns pattern)
	{
		if (groupEntity == Entity.Null
			|| !EntityManager.HasComponent<TrafficGroup>(groupEntity))
		{
			return;
		}

		Entity leaderEntity = GetGroupLeader(groupEntity);
		var members = GetGroupMembers(groupEntity);
		foreach (Entity memberEntity in members)
		{
			if (memberEntity == leaderEntity)
			{
				continue;
			}

			if (!EntityManager.HasComponent<CustomTrafficLights>(memberEntity))
			{
				EntityManager.AddComponentData(
					memberEntity,
					new CustomTrafficLights(pattern));
			}
			else
			{
				var memberLights =
					EntityManager.GetComponentData<CustomTrafficLights>(
						memberEntity);
				memberLights.SetPattern(pattern);
				memberLights.m_Timer = 0;
				EntityManager.SetComponentData(memberEntity, memberLights);
			}

			if (((uint)pattern & 0xFFFF)
				== (uint)CustomTrafficLights.Patterns.CustomPhase)
			{
				EnsureMemberCustomPhaseSetup(groupEntity, memberEntity);
			}
			else
			{
				MarkMemberUpdated(memberEntity);
			}
		}

		int memberCount = members.Length;
		members.Dispose();
		m_Log.Info(
			$"Propagated pattern {pattern} to {memberCount - 1} group members");
	}

	#endregion

	

	

	#region Flow/Wait Look-ahead

	
	public int CalculateBestNextPhase(Entity junctionEntity, int currentPhase)
	{
		if (junctionEntity == Entity.Null || !EntityManager.HasBuffer<CustomPhaseData>(junctionEntity))
		{
			return (currentPhase + 1) % 1; 
		}

		EntityManager.TryGetBuffer<CustomPhaseData>(junctionEntity, false, out var phases);
		if (phases.Length == 0)
		{
			return 0;
		}

		int nextPhase = (currentPhase + 1) % phases.Length;
		float bestMetric = float.MinValue;
		int bestPhase = nextPhase;

		int checkedPhases = 0;
		int checkPhase = nextPhase;

		while (checkedPhases < phases.Length)
		{
			var phase = phases[checkPhase];
			
			float flow = phase.AverageCarFlow();
			float wait = phase.m_WeightedWaiting * phase.m_WaitFlowBalance;
			float metric = CalculatePhaseMetric(phase.m_ChangeMetric, flow, wait);

			if (metric > bestMetric)
			{
				bestMetric = metric;
				bestPhase = checkPhase;
			}

			if (phase.m_MinimumDuration > 0)
			{
				break;
			}

			checkPhase = (checkPhase + 1) % phases.Length;
			checkedPhases++;

			if (checkPhase == currentPhase)
			{
				break;
			}
		}

		return bestPhase;
	}

	
	private float CalculatePhaseMetric(CustomPhaseData.StepChangeMetric metric, float flow, float wait)
	{
		switch (metric)
		{
			case CustomPhaseData.StepChangeMetric.FirstFlow:
				return flow > 0 ? flow : float.MinValue;
			case CustomPhaseData.StepChangeMetric.FirstWait:
				return wait > 0 ? wait : float.MinValue;
			case CustomPhaseData.StepChangeMetric.NoFlow:
				return flow <= 0 ? 1f : float.MinValue;
			case CustomPhaseData.StepChangeMetric.NoWait:
				return wait <= 0 ? 1f : float.MinValue;
			case CustomPhaseData.StepChangeMetric.Default:
			default:
				return flow - wait; 
		}
	}

	
	public void ApplyBestPhaseToGroup(Entity groupEntity)
	{
		if (groupEntity == Entity.Null || !EntityManager.HasComponent<TrafficGroup>(groupEntity))
		{
			return;
		}

		var group = EntityManager.GetComponentData<TrafficGroup>(groupEntity);
		UpdateMasterClock(groupEntity, ref group);
		EntityManager.SetComponentData(groupEntity, group);

		if (group.m_MasterSignalGroupCount == 0)
		{
			return;
		}

		Entity leaderEntity = GetGroupLeader(groupEntity);
		if (leaderEntity == Entity.Null)
		{
			return;
		}

		int currentPhase = group.m_MasterPhase - 1;

		int bestPhase = CalculateBestNextPhase(leaderEntity, currentPhase);

		if (bestPhase != currentPhase)
		{
			RefreshMovementMappings(groupEntity, leaderEntity);
			
			var members = GetGroupMembers(groupEntity);

			foreach (var memberEntity in members)
			{
				if (!EntityManager.HasComponent<TrafficLights>(memberEntity))
				{
					continue;
				}

				var memberData = EntityManager.GetComponentData<TrafficGroupMember>(memberEntity);
				var trafficLights = EntityManager.GetComponentData<TrafficLights>(memberEntity);

				int phaseCount = GetPhaseCount(memberEntity);
				if (phaseCount <= 0)
				{
					continue;
				}

				if (!TryMapLeaderPhase(memberEntity, bestPhase + 1, out int mappedPhase))
				{
					continue;
				}

				int adjustedPhase = TrafficGroupTimingPolicy.WrapZeroBasedPhase(
					(mappedPhase - 1) + memberData.m_PhaseOffset,
					phaseCount);
				trafficLights.m_NextSignalGroup = (byte)(adjustedPhase + 1);
				EntityManager.SetComponentData(memberEntity, trafficLights);
			}

			members.Dispose();
		}
	}

	#endregion


	
	public void OnJunctionGeometryUpdate(Entity junctionEntity)
	{
		if (junctionEntity == Entity.Null)
		{
			return;
		}

		
		if (!EntityManager.HasComponent<TrafficGroupMember>(junctionEntity))
		{
			return;
		}

		var member = EntityManager.GetComponentData<TrafficGroupMember>(junctionEntity);
		Entity groupEntity = member.m_GroupEntity;

		if (groupEntity == Entity.Null)
		{
			return;
		}


		ValidateJunctionPhases(junctionEntity);

		if (member.m_IsGroupLeader)
		{
			RecalculateGroupCycleLength(groupEntity);
			
			var group = EntityManager.GetComponentData<TrafficGroup>(groupEntity);
			if (group.m_GreenWaveEnabled)
			{
				if (EntityManager.HasBuffer<CustomPhaseData>(junctionEntity) && 
				    EntityManager.TryGetBuffer<CustomPhaseData>(junctionEntity, false, out var phases) && phases.Length > 0)
				{
					CalculateEnhancedGreenWaveTiming(groupEntity);
				}
				else
				{
					CalculateGreenWaveTiming(groupEntity);
				}
			}
		}

		UpdateMemberDistanceToLeader(junctionEntity);
	}

	
	private void ValidateJunctionPhases(Entity junctionEntity)
	{
		if (!EntityManager.HasBuffer<CustomPhaseData>(junctionEntity))
		{
			return;
		}

		EntityManager.TryGetBuffer<CustomPhaseData>(junctionEntity, false, out var phases);
		
		for (int i = 0; i < phases.Length; i++)
		{
			var phase = phases[i];
			phase.m_TurnsSinceLastRun = 0;
			phase.m_LowFlowTimer = 0;
			phase.m_LowPriorityTimer = 0;
			phase.m_WeightedWaiting = 0f;
			phase.m_Options &= ~CustomPhaseData.Options.EndPhasePrematurely;
			phases[i] = phase;
		}

		if (EntityManager.HasBuffer<EdgeGroupMask>(junctionEntity))
		{
			EntityManager.TryGetBuffer<EdgeGroupMask>(junctionEntity, false, out var edgeMasks);
			
			if (edgeMasks.Length != phases.Length && phases.Length > 0)
			{
				
				while (edgeMasks.Length > phases.Length)
				{
					edgeMasks.RemoveAt(edgeMasks.Length - 1);
				}
				while (edgeMasks.Length < phases.Length)
				{
					edgeMasks.Add(new EdgeGroupMask());
				}
			}
		}
	}

	
	private void UpdateMemberDistanceToLeader(Entity memberEntity)
	{
		if (!EntityManager.HasComponent<TrafficGroupMember>(memberEntity))
		{
			return;
		}

		var memberData = EntityManager.GetComponentData<TrafficGroupMember>(memberEntity);
		
		if (memberData.m_IsGroupLeader)
		{
			memberData.m_DistanceToLeader = 0f;
			EntityManager.SetComponentData(memberEntity, memberData);
			return;
		}

		Entity leaderEntity = memberData.m_LeaderEntity;
		if (leaderEntity == Entity.Null)
		{
			return;
		}

		if (!EntityManager.HasComponent<Node>(memberEntity) || !EntityManager.HasComponent<Node>(leaderEntity))
		{
			return;
		}

		var memberNode = EntityManager.GetComponentData<Node>(memberEntity);
		var leaderNode = EntityManager.GetComponentData<Node>(leaderEntity);

		float distance = math.distance(memberNode.m_Position, leaderNode.m_Position);
		memberData.m_DistanceToLeader = distance;
		EntityManager.SetComponentData(memberEntity, memberData);
	}

	
	public void HousekeepingAllGroups()
	{
		var groups = m_GroupQuery.ToEntityArray(Allocator.Temp);

		foreach (var groupEntity in groups)
		{
			HousekeepingGroup(groupEntity);
		}

		groups.Dispose();
	}

	
	public void HousekeepingGroup(Entity groupEntity)
	{
		if (groupEntity == Entity.Null || !EntityManager.HasComponent<TrafficGroup>(groupEntity))
		{
			return;
		}

		var members = GetGroupMembers(groupEntity);
		var invalidMembers = new NativeList<Entity>(Allocator.Temp);

		
		foreach (var memberEntity in members)
		{
			if (!EntityManager.Exists(memberEntity) || !EntityManager.HasComponent<TrafficLights>(memberEntity))
			{
				invalidMembers.Add(memberEntity);
			}
		}

		
		foreach (var invalidMember in invalidMembers)
		{
			if (EntityManager.HasComponent<TrafficGroupMember>(invalidMember))
			{
				EntityManager.RemoveComponent<TrafficGroupMember>(invalidMember);
			}
		}

		invalidMembers.Dispose();
		members.Dispose();

		
		int memberCount = GetGroupMemberCount(groupEntity);
		if (memberCount == 0)
		{
			
			EntityManager.DestroyEntity(groupEntity);
			return;
		}

		
		Entity leader = GetGroupLeader(groupEntity);
		if (leader == Entity.Null)
		{
			AssignNewLeader(groupEntity);
		}

		
		ReindexGroupMembers(groupEntity);
	}

	

	#region Edge Position Helpers

	
	private struct AngleComparer : IComparer<(Entity edge, float angle, int originalIndex)>
	{
		public int Compare((Entity edge, float angle, int originalIndex) x, (Entity edge, float angle, int originalIndex) y)
		{
			return x.angle.CompareTo(y.angle);
		}
	}

	
	private float3 GetEdgePositionForJunction(Entity nodeEntity, Entity edgeEntity, ComponentLookup<Edge> edgeLookup, ComponentLookup<EdgeGeometry> edgeGeometryLookup)
	{
		float3 position = float3.zero;
		
		if (!edgeLookup.TryGetComponent(edgeEntity, out Edge edge))
		{
			return position;
		}
		
		if (!edgeGeometryLookup.TryGetComponent(edgeEntity, out EdgeGeometry edgeGeometry))
		{
			return position;
		}
		
		if (edge.m_Start.Equals(nodeEntity))
		{
			position = (edgeGeometry.m_Start.m_Left.a + edgeGeometry.m_Start.m_Right.a) / 2;
		}
		else if (edge.m_End.Equals(nodeEntity))
		{
			position = (edgeGeometry.m_End.m_Left.d + edgeGeometry.m_End.m_Right.d) / 2;
		}
		
		return position;
	}

	#endregion
}
