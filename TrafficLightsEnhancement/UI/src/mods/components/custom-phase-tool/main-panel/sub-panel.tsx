import {useState} from "react";

import {callHighlightEdge, setPanelState, callUpdateCustomPhaseData, callApplyPhaseTemplate} from "bindings";

import {PanelFoldout} from "cs2/ui";

import {useLocalization} from "cs2/l10n";

import Button from "../../common/button";
import Divider from "../../main-panel/items/divider";
import MainPanelRange from "../../main-panel/items/range";
import Row from "../../main-panel/items/row";
import Title from "../../main-panel/items/title";
import TitleDim from "../../main-panel/items/title-dim";
import {EdgeInfo, MainPanelItemCustomPhase, MainPanelItemTitle} from "mods/general";
import {MainPanelState} from "../../../constants";

import styles from "./modules/sub-panel.module.scss";
import MainPanelRadio from "mods/components/main-panel/items/radio";
import MainPanelCheckbox from "mods/components/main-panel/items/checkbox";
import PresetManager from "../../common/preset-manager/preset-manager";

const PHASE_TEMPLATES = [
    { id: 0, name: "Default", description: "StandardTiming" },
    { id: 1, name: "QuickCycle", description: "ShortResponsive" },
    { id: 2, name: "HeavyTraffic", description: "LongSteadyFlow" },
    { id: 3, name: "PedestrianFriendly", description: "BalancedForPeds" },
    { id: 4, name: "RailPriority", description: "RailFirstSwitching" },
    { id: 5, name: "NightMode", description: "VeryShortSkipsEmpty" },
];
export const ItemTitle = (props: {
    title: string,
    secondaryText?: string,
    tooltip?: React.ReactNode,
    dim?: boolean
}) => {
    const item: MainPanelItemTitle = {
        itemType: "title",
        ...props
    };
    return (
        <Row data={item}>
            {props.dim && <TitleDim {...item} />}
            {!props.dim && <Title {...item} />}
        </Row>
    );
};

const EndPhaseButton = (props: { index: number, disabled?: boolean }) => {
    const clickHandler = () => {
        if (!props.disabled) {
            callUpdateCustomPhaseData(JSON.stringify({key: "EndPhasePrematurely", index: props.index}));
        }
    };
    return (
        <Row hoverEffect={!props.disabled}>
            <Button
                label={props.disabled ? "PhaseEndRequested" : "EndPhasePrematurely"}
                disabled={props.disabled}
                onClick={clickHandler}
            />
        </Row>
    );
};

export function TrafficLightModeSelector(props: { trafficLightMode: number }) {
    const { translate } = useLocalization();
    return (
        <PanelFoldout header={<div className={styles.foldoutHeader}>{translate("UI.LABEL[C2VM.TrafficLightsEnhancement.TrafficLightMode]") ?? "Traffic light mode"}</div>}
                      initialExpanded={true}>
                        <MainPanelRadio
                        keyName="TrafficLightMode"
                        value="0"
                        isChecked={props.trafficLightMode === 0}
                        label="Dynamic"
                        triggerName="CallUpdateCustomPhaseData"
            />
            <MainPanelRadio
                keyName="TrafficLightMode"
                value="1"
                isChecked={props.trafficLightMode === 1}
                label="FixedTimed"
                triggerName="CallUpdateCustomPhaseData"
            />
        </PanelFoldout>
    );
}

const EdgeFoldout = ({
                         edge,
                         phaseIndex,
                         isHighlighted,
                         onHighlight
                     }: {
    edge: EdgeInfo;
    phaseIndex: number;
    isHighlighted: boolean;
    onHighlight: (edgeIndex: number, edgeVersion: number) => void;
}) => {
    const { translate } = useLocalization();
    const edgeName = `Edge ${edge.m_Edge.index}`;

    const handleHeaderClick = () => {
        onHighlight(edge.m_Edge.index, edge.m_Edge.version);
    };

    return (
        <PanelFoldout
            header={
                <div
                    className={styles.foldoutHeader}
                    onClick={handleHeaderClick}
                    style={{
                        cursor: 'pointer',
                        color: isHighlighted ? 'var(--accentColorNormal)' : undefined
                    }}
                >
                    {edgeName}
                </div>
            }
            initialExpanded={false}
        >
            <MainPanelRange className={styles.hover} data={{
                itemType: "range",
                key: JSON.stringify({
                    edgeIndex: edge.m_Edge.index,
                    edgeVersion: edge.m_Edge.version,
                    phaseIndex: phaseIndex,
                    field: "openDelay"
                }),
                label: "StartDelay",
                value: edge.m_OpenDelay ?? 0,
                valuePrefix: "",
                valueSuffix: "",
                min: 0,
                max: 30,
                step: 1,
                defaultValue: 0,
                enableTextField: true,
                textFieldRegExp: "^\\d{0,3}$",
                engineEventName: "C2VM.TrafficLightsEnhancement.TRIGGER:CallUpdateEdgeDelay",
                tooltip: translate("Tooltip.LABEL[C2VM.TrafficLightsEnhancement.StartDelay]") ?? "Delay before this edge's signals turn green after the phase starts."
            }}/>
            <MainPanelRange className={styles.hover} data={{
                itemType: "range",
                key: JSON.stringify({
                    edgeIndex: edge.m_Edge.index,
                    edgeVersion: edge.m_Edge.version,
                    phaseIndex: phaseIndex,
                    field: "closeDelay"
                }),
                label: "EndEarly",
                value: edge.m_CloseDelay ?? 0,
                valuePrefix: "",
                valueSuffix: "",
                min: 0,
                max: 30,
                step: 1,
                defaultValue: 0,
                enableTextField: true,
                textFieldRegExp: "^\\d{0,3}$",
                engineEventName: "C2VM.TrafficLightsEnhancement.TRIGGER:CallUpdateEdgeDelay",
                tooltip: translate("Tooltip.LABEL[C2VM.TrafficLightsEnhancement.EndEarly]") ?? "Time before the phase ends when this edge's signals turn red."
            }}/>
        </PanelFoldout>
    );
};

export default function SubPanel(props: {
    data: MainPanelItemCustomPhase | null;
    edges?: EdgeInfo[];
    phaseIndex?: number;
    statisticsOnly?: boolean;
    isCoordinatedFollower?: boolean;
}) {
    const { translate } = useLocalization();
    const data = props.data;
    const [highlightedEdge, setHighlightedEdge] = useState<{ index: number, version: number } | null>(null);
    const handleBackToGroups = () => {
        setPanelState(MainPanelState.TrafficGroups);
    };

    const handleEdgeHighlight = (edgeIndex: number, edgeVersion: number) => {
        const newHighlight = {index: edgeIndex, version: edgeVersion};
        const isSameEdge = highlightedEdge?.index === edgeIndex && highlightedEdge?.version === edgeVersion;

        if (isSameEdge) {
            setHighlightedEdge(null);
            callHighlightEdge(JSON.stringify({edgeIndex: -1, edgeVersion: -1}));
        } else {
            setHighlightedEdge(newHighlight);
            callHighlightEdge(JSON.stringify({edgeIndex, edgeVersion}));
        }
    };

    if (!data) {
        return <></>;
    }

    return (
        <>
            {!props.statisticsOnly && props.isCoordinatedFollower && (
                <>
                    <div className={styles.coordinatedFollowerNotice}>
                        {translate("UI.LABEL[C2VM.TrafficLightsEnhancement.CoordinatedFollowerTimingNotice]") ?? "Phase timing is controlled by the group leader. Values shown below are from the leader."}
                    </div>
                    <PanelFoldout
                        header={<div className={styles.foldoutHeader}>{translate("UI.LABEL[C2VM.TrafficLightsEnhancement.LeaderPhaseSettingsReadOnly]") ?? "Leader phase settings (read-only)"}</div>}
                        initialExpanded={true}>
                        <ItemTitle title="TrafficLightMode" secondaryText={data.trafficLightMode === 0 ? "Dynamic" : "FixedTimed"} dim={true} />
                        <ItemTitle title="MinimumDuration" secondaryText={`${data.minimumDuration}`} dim={true} />
                        <ItemTitle title="MaximumDuration" secondaryText={`${data.maximumDuration}`} dim={true} />
                        {data.trafficLightMode === 0 && <>
                            <ItemTitle title="TargetDurationMultiplier" secondaryText={`${data.targetDurationMultiplier}x`} dim={true} />
                            <ItemTitle title="IntervalExponent" secondaryText={`${data.intervalExponent}`} dim={true} />
                            <ItemTitle title="PhaseChangeMode" secondaryText={
                                data.changeMetric === 0 ? "Auto" :
                                data.changeMetric === 1 ? "OnFlowDrop" :
                                data.changeMetric === 2 ? "OnWaitIncrease" :
                                data.changeMetric === 3 ? "WhenEmpty" : "WhenNoDemand"
                            } dim={true} />
                            <ItemTitle title="WaitSensitivity" secondaryText={`${data.waitFlowBalance}`} dim={true} />
                        </>}
                    </PanelFoldout>
                    <Divider />
                </>
            )}
            {!props.statisticsOnly && !props.isCoordinatedFollower && (
                <>
                    <PanelFoldout
                        header={<div className={styles.foldoutHeader}>{translate("UI.LABEL[C2VM.TrafficLightsEnhancement.TrafficLightMode]") ?? "Traffic light mode"}</div>}
                        initialExpanded={true}>
                        <MainPanelRadio
                            keyName="TrafficLightMode"
                            value="0"
                            isChecked={data.trafficLightMode === 0}
                            label="Dynamic"
                            triggerName="CallUpdateCustomPhaseData"
                            tooltip={translate("Tooltip.LABEL[C2VM.TrafficLightsEnhancement.Dynamic]") ?? "Dynamic phase mode that adjusts timing based on traffic conditions."}
                            className={styles.hover}
                        />
                        <MainPanelRadio
                            keyName="TrafficLightMode"
                            value="1"
                            isChecked={data.trafficLightMode === 1}
                            label="FixedTimed"
                            triggerName="CallUpdateCustomPhaseData"
                            tooltip={translate("Tooltip.LABEL[C2VM.TrafficLightsEnhancement.FixedTimed]") ?? "Fixed timing mode with preset phase durations."}
                            className={styles.hover}
                        />
                        {data.trafficLightMode === 1 && (
                            <MainPanelCheckbox
                                keyName="SmartPhaseSelection"
                                isChecked={data.smartPhaseSelection}
                                label="SmartPhaseSelection"
                                triggerName="CallUpdateCustomPhaseData"
                                tooltip={translate("Tooltip.LABEL[C2VM.TrafficLightsEnhancement.SmartPhaseSelection]") ?? "Enable intelligent phase selection based on traffic conditions. Disable for simple sequential phases (1→2→3→4→1...)."}
                                className={styles.hover}
                            />
                        )}
                    </PanelFoldout>

                    <Divider/>
                    <PanelFoldout
                        header={<div className={styles.foldoutHeader}>{translate("UI.LABEL[C2VM.TrafficLightsEnhancement.TimingTemplate]") ?? "Timing template"}</div>}
                        initialExpanded={false}>
                        <PresetManager
                            builtInTemplates={PHASE_TEMPLATES}
                            onApplyBuiltIn={(templateId) => {
                                callApplyPhaseTemplate(JSON.stringify({ templateId }));
                            }}
                        />
                    </PanelFoldout>

                    <Divider/>
                    <PanelFoldout
                        header={<div className={styles.foldoutHeader}>{translate("UI.LABEL[C2VM.TrafficLightsEnhancement.PhaseChangeMode]") ?? "Phase change mode"}</div>}
                        initialExpanded={false}>
                        <MainPanelRadio
                            keyName="ChangeMetric"
                            value="0"
                            isChecked={data.changeMetric === 0}
                            label="Auto"
                            triggerName="CallUpdateCustomPhaseData"
                            tooltip={translate("Tooltip.LABEL[C2VM.TrafficLightsEnhancement.Auto]") ?? "Automatically balances traffic flow and waiting time to decide when to change phase."}
                            className={styles.hover}
                        />
                        <MainPanelRadio
                            keyName="ChangeMetric"
                            value="1"
                            isChecked={data.changeMetric === 1}
                            label="OnFlowDrop"
                            triggerName="CallUpdateCustomPhaseData"
                            tooltip={translate("Tooltip.LABEL[C2VM.TrafficLightsEnhancement.OnFlowDrop]") ?? "Changes phase when traffic flow decreases. Keeps traffic moving smoothly."}
                            className={styles.hover}
                        />
                        <MainPanelRadio
                            keyName="ChangeMetric"
                            value="2"
                            isChecked={data.changeMetric === 2}
                            label="OnWaitIncrease"
                            triggerName="CallUpdateCustomPhaseData"
                            tooltip={translate("Tooltip.LABEL[C2VM.TrafficLightsEnhancement.OnWaitIncrease]") ?? "Changes phase when waiting traffic increases. Reduces wait times."}
                            className={styles.hover}
                        />
                        <MainPanelRadio
                            keyName="ChangeMetric"
                            value="3"
                            isChecked={data.changeMetric === 3}
                            label="WhenEmpty"
                            triggerName="CallUpdateCustomPhaseData"
                            tooltip={translate("Tooltip.LABEL[C2VM.TrafficLightsEnhancement.WhenEmpty]") ?? "Changes phase only when current lanes are empty. Maximizes throughput per phase."}
                            className={styles.hover}
                        />
                        <MainPanelRadio
                            keyName="ChangeMetric"
                            value="4"
                            isChecked={data.changeMetric === 4}
                            label="WhenNoDemand"
                            triggerName="CallUpdateCustomPhaseData"
                            tooltip={translate("Tooltip.LABEL[C2VM.TrafficLightsEnhancement.WhenNoDemand]") ?? "Changes phase only when other lanes have waiting traffic. Avoids unnecessary changes."}
                            className={styles.hover}
                        />
                        <MainPanelRange className={styles.hover} data={{
                            itemType: "range",
                            key: "WaitFlowBalance",
                            label: "WaitSensitivity",
                            value: data.waitFlowBalance,
                            valuePrefix: "",
                            valueSuffix: "",
                            min: 0.1,
                            max: 10,
                            step: 0.1,
                            defaultValue: 1,
                            enableTextField: true,
                            textFieldRegExp: "^\\d{0,4}(\\.\\d{0,2})?$",
                            engineEventName: "C2VM.TrafficLightsEnhancement.TRIGGER:CallUpdateCustomPhaseData",
                            tooltip: translate("Tooltip.LABEL[C2VM.TrafficLightsEnhancement.WaitSensitivity]") ?? "How much to prioritize waiting traffic. Higher = change phases sooner when cars are waiting."
                        }}/>
                    </PanelFoldout>
                </>
            )}

            {!props.statisticsOnly && !props.isCoordinatedFollower &&
                <>
                    <Divider/>
                    <PanelFoldout header={<div className={styles.foldoutHeader}>{translate("UI.LABEL[C2VM.TrafficLightsEnhancement.Adjustments]") ?? "Adjustments"}</div>}
                                  initialExpanded={false}>
                        <MainPanelRange className={styles.hover} data={{
                            itemType: "range",
                            key: "MinimumDuration",
                            label: "MinimumDuration",
                            value: data.minimumDuration,
                            valuePrefix: "",
                            valueSuffix: "s",
                            min: 0,
                            max: 30,
                            step: 1,
                            defaultValue: 2,
                            enableTextField: true,
                            textFieldRegExp: "^\\d{0,4}$",
                            engineEventName: "C2VM.TrafficLightsEnhancement.TRIGGER:CallUpdateCustomPhaseData",
                            tooltip: translate("Tooltip.LABEL[C2VM.TrafficLightsEnhancement.MinimumDuration]") ?? "Sets the minimum time a traffic light phase must stay active before it can change."
                        }}/>
                        <MainPanelRange className={styles.hover} data={{
                            itemType: "range",
                            key: "MaximumDuration",
                            label: "MaximumDuration",
                            value: data.maximumDuration,
                            valuePrefix: "",
                            valueSuffix: "s",
                            min: 5,
                            max: 300,
                            step: 5,
                            defaultValue: 300,
                            enableTextField: true,
                            textFieldRegExp: "^\\d{0,4}$",
                            engineEventName: "C2VM.TrafficLightsEnhancement.TRIGGER:CallUpdateCustomPhaseData",
                            tooltip: translate("Tooltip.LABEL[C2VM.TrafficLightsEnhancement.MaximumDuration]") ?? "Sets the maximum time a traffic light phase can remain active."
                        }}/>
                        {data.trafficLightMode === 0 && <>
                            <MainPanelRange className={styles.hover} data={{
                                itemType: "range",
                                key: "TargetDurationMultiplier",
                                label: "TargetDuration",
                                value: data.targetDurationMultiplier,
                                valuePrefix: "",
                                valueSuffix: "CustomPedestrianDurationMultiplierSuffix",
                                min: 0.1,
                                max: 10,
                                step: 0.1,
                                defaultValue: 1,
                                enableTextField: true,
                                textFieldRegExp: "^\\d{0,4}(\\.\\d{0,2})?$",
                                engineEventName: "C2VM.TrafficLightsEnhancement.TRIGGER:CallUpdateCustomPhaseData",
                                tooltip: translate("Tooltip.LABEL[C2VM.TrafficLightsEnhancement.TargetDuration]") ?? "Scales the calculated target duration for each phase. Higher values make phases last longer."
                            }}/>
                            <MainPanelRange className={styles.hover} data={{
                                itemType: "range",
                                key: "IntervalExponent",
                                label: "IntervalExponent",
                                value: data.intervalExponent,
                                valuePrefix: "",
                                valueSuffix: "",
                                min: 0.1,
                                max: 10,
                                step: 0.1,
                                defaultValue: 2,
                                enableTextField: true,
                                textFieldRegExp: "^\\d{0,4}(\\.\\d{0,2})?$",
                                engineEventName: "C2VM.TrafficLightsEnhancement.TRIGGER:CallUpdateCustomPhaseData",
                                tooltip: translate("Tooltip.LABEL[C2VM.TrafficLightsEnhancement.IntervalExponent]") ?? "Controls how aggressively the system prioritizes phases that haven't run recently."
                            }}/>
                        </>}
                    </PanelFoldout>
                    {data.trafficLightMode === 0 && <>
                        <Divider/>
                        <PanelFoldout
                            header={<div className={styles.foldoutHeader}>{translate("UI.LABEL[C2VM.TrafficLightsEnhancement.VehicleWeights]") ?? "Vehicle weights"}</div>}
                            initialExpanded={false}>
                            <MainPanelRange
                                className={styles.hover}
                                keyName="CarWeight"
                                label="CarWeight"
                                value={data.carWeight}
                                valueSuffix="x"
                                min={0.1}
                                max={10}
                                step={0.1}
                                defaultValue={1}
                                enableTextField
                                textFieldRegExp="^\\d{0,2}(\\.\\d{0,1})?$"
                                triggerName="CallUpdateCustomPhaseData"
                                tooltip={translate("Tooltip.LABEL[C2VM.TrafficLightsEnhancement.CarWeight]") ?? "Weight multiplier for car lanes when calculating phase priority."}
                            />
                            <MainPanelRange
                                className={styles.hover}
                                keyName="PublicCarWeight"
                                label="BusWeight"
                                value={data.publicCarWeight}
                                valueSuffix="x"
                                min={0.1}
                                max={10}
                                step={0.1}
                                defaultValue={2}
                                enableTextField
                                textFieldRegExp="^\\d{0,2}(\\.\\d{0,1})?$"
                                triggerName="CallUpdateCustomPhaseData"
                                tooltip={translate("Tooltip.LABEL[C2VM.TrafficLightsEnhancement.BusWeight]") ?? "Weight multiplier for public transport (bus) lanes."}
                            />
                            <MainPanelRange
                                className={styles.hover}
                                keyName="TrackWeight"
                                label="TrackWeight"
                                value={data.trackWeight}
                                valueSuffix="x"
                                min={0.1}
                                max={10}
                                step={0.1}
                                defaultValue={3}
                                enableTextField
                                textFieldRegExp="^\\d{0,2}(\\.\\d{0,1})?$"
                                triggerName="CallUpdateCustomPhaseData"
                                tooltip={translate("Tooltip.LABEL[C2VM.TrafficLightsEnhancement.TrackWeight]") ?? "Weight multiplier for tram/train tracks."}
                            />
                            <MainPanelRange
                                className={styles.hover}
                                keyName="PedestrianWeight"
                                label="PedestrianWeight"
                                value={data.pedestrianWeight}
                                valueSuffix="x"
                                min={0.1}
                                max={10}
                                step={0.1}
                                defaultValue={1}
                                enableTextField
                                textFieldRegExp="^\\d{0,2}(\\.\\d{0,1})?$"
                                triggerName="CallUpdateCustomPhaseData"
                                tooltip={translate("Tooltip.LABEL[C2VM.TrafficLightsEnhancement.PedestrianWeight]") ?? "Weight multiplier for pedestrian crossings."}
                            />
                            <MainPanelRange
                                className={styles.hover}
                                keyName="BicycleWeight"
                                label="BicycleWeight"
                                value={data.bicycleWeight}
                                valueSuffix="x"
                                min={0.1}
                                max={10}
                                step={0.1}
                                defaultValue={1}
                                enableTextField
                                textFieldRegExp="^\\d{0,2}(\\.\\d{0,1})?$"
                                triggerName="CallUpdateCustomPhaseData"
                                tooltip={translate("Tooltip.LABEL[C2VM.TrafficLightsEnhancement.BicycleWeight]") ?? "Weight multiplier for bicycle lanes."}
                            />
                            <MainPanelRange
                                className={styles.hover}
                                keyName="SmoothingFactor"
                                label="SmoothingFactor"
                                value={data.smoothingFactor}
                                min={0}
                                max={1}
                                step={0.1}
                                defaultValue={0.5}
                                enableTextField
                                textFieldRegExp="^(0(\\.\\d{0,1})?|1(\\.0)?)$"
                                triggerName="CallUpdateCustomPhaseData"
                                tooltip={translate("Tooltip.LABEL[C2VM.TrafficLightsEnhancement.SmoothingFactor]") ?? "How much to blend current calculations with previous values. 0 = no smoothing (instant changes), 1 = full smoothing (very gradual changes)."}
                            />
                        </PanelFoldout>
                    </>}
                    <Divider/>
                </>}

            <PanelFoldout header={<div className={styles.foldoutHeader}>{translate("UI.LABEL[C2VM.TrafficLightsEnhancement.Statistics]") ?? "Statistics"}</div>} initialExpanded={true}>
                <ItemTitle title="Timer"
                           secondaryText={`${data.timer} / ${Math.round(Math.min(Math.max(data.targetDuration, data.minimumDuration), data.maximumDuration))}`}
                           dim={true}/>
                <ItemTitle title="Priority" secondaryText={`${data.priority}`} dim={true}/>
                <ItemTitle title="TurnsSinceLastRun" secondaryText={`${data.turnsSinceLastRun}`} dim={true}/>
                <Divider/>
                <ItemTitle title="Flow" secondaryText={`${Round(data.carFlow)}`} dim={true}
                           tooltip={translate("Tooltip.LABEL[C2VM.TrafficLightsEnhancement.Flow]") ?? "Average car flow through this phase"}/>
                <ItemTitle title="FlowRatio" secondaryText={`${Round(data.flowRatio)}`} dim={true}
                           tooltip={translate("Tooltip.LABEL[C2VM.TrafficLightsEnhancement.FlowRatio]") ?? "Smoothed flow ratio for phase decisions"}/>
                <ItemTitle title="WaitRatio" secondaryText={`${Round(data.waitRatio)}`} dim={true}
                           tooltip={translate("Tooltip.LABEL[C2VM.TrafficLightsEnhancement.WaitRatio]") ?? "Smoothed wait ratio for phase decisions"}/>
                <ItemTitle title="WeightedWaiting" secondaryText={`${Round(data.weightedWaiting)}`} dim={true}
                           tooltip={translate("Tooltip.LABEL[C2VM.TrafficLightsEnhancement.WeightedWaiting]") ?? "Combined waiting metric used for phase priority"}/>
                <Divider/>
                <ItemTitle title="CarsWaiting" secondaryText={`${data.carLaneOccupied}`} dim={true}/>
                <ItemTitle title="BusesWaiting" secondaryText={`${data.publicCarLaneOccupied}`} dim={true}/>
                <ItemTitle title="TramsWaiting" secondaryText={`${data.trackLaneOccupied}`} dim={true}/>
                <ItemTitle title="PedestriansWaiting" secondaryText={`${data.pedestrianLaneOccupied}`} dim={true}/>
            </PanelFoldout>
            {!props.statisticsOnly && <>
                {props.edges && props.edges.length > 0 && props.phaseIndex !== undefined && <>
                    <Divider/>
                    <PanelFoldout header={<div className={styles.foldoutHeader}>{translate("UI.LABEL[C2VM.TrafficLightsEnhancement.SignalDelays]") ?? "Signal delays"}</div>}
                                initialExpanded={false}>
                        {props.edges.map((edge, idx) => (
                            <EdgeFoldout
                                key={`${edge.m_Edge.index}-${edge.m_Edge.version}`}
                                edge={edge}
                                phaseIndex={props.phaseIndex!}
                                isHighlighted={highlightedEdge?.index === edge.m_Edge.index && highlightedEdge?.version === edge.m_Edge.version}
                                onHighlight={handleEdgeHighlight}
                            />
                        ))}
                    </PanelFoldout>
                </>}
            </>}
            {data.activeIndex < 0 && data.manualSignalGroup <= 0 && data.currentSignalGroup == data.index + 1 &&
                <EndPhaseButton index={data.index} disabled={data.endPhasePrematurely}/>}
        </>)
}


function Round(num: number): number {
    return Math.round(num * 100) / 100;
}
