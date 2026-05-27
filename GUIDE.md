## Introduction
TLE Extended introduces advanced traffic light controls for Cities: Skylines II, allowing for more explicit traffic management at supported junctions. It supports both Left-Hand Traffic (LHT) and Right-Hand Traffic (RHT), and it is intended to remain compatible with intersections already configured in Traffic Lights Enhancement.

## Modes

| Mode | Description |
| --- | --- |
| Vanilla | Operates like the base game.<br>LHT: protected straight, protected left, and permissive right.<br>RHT: protected straight, protected right, and permissive left. |
| Split-Phasing | Only one road has a green light at a time. |
| Advanced Split Phasing[^1] | Similar to Split Phasing, with additional protected turns for the other road at the same time.[^2] |
| Protected Left/Right-Turns[^1] | LHT: Centre lanes perform a protected left turn first, followed by normal traffic flow including straight and right turns.<br>RHT: Centre lanes perform a protected right turn first, followed by normal traffic flow including straight and left turns.<br>[Video Illustration](https://www.youtube.com/watch?v=CIw0Au8qFQ8) |

> [!TIP]
> Most modes change the sequencing or grouping of signals. Dynamic mode and Transit Signal Priority can also affect phase selection or timing within their configured limits.

## Options

| Option | Description |
| --- | --- |
| Allow Turning on Red | Allow vehicles to turn left (in LHT) or right (in RHT) when the signal is red. |
| Give Way to Oncoming Vehicles<br>(Only for vanilla signals) | Require vehicles to give way to oncoming traffic when turning.<br>Note: Although drivers are required to give way, their aggressive behavior may reduce the effectiveness of this option at busy junctions. |
| Exclusive Pedestrian Phase | A dedicated phase for pedestrian crossings, stopping all vehicular traffic. |
| Pedestrian Phase Duration | Multiplies the base green-light duration for the exclusive pedestrian phase.<br>Only available when the "Exclusive Pedestrian Phase" option is enabled.<br>Note: Pedestrian traffic lights are not "smart" and will not extend the green signal. |

## Transit Signal Priority

Transit Signal Priority is configured per intersection. The panel provides separate source toggles for trams and buses, so each intersection can enable either source independently.

| Source Option | Description |
| --- | --- |
| Enable for trams | Allows approaching trams to request signal priority at this intersection. TSP may extend the current compatible phase or preempt toward a tram-serving phase, while respecting an already-active exclusive pedestrian phase. |
| Enable for buses | Allows approaching buses to request soft signal priority at this intersection. Bus priority may hold an already-serving green or select the bus-serving group at normal transition points, but it does not use aggressive tram-style preemption. |

TSP is intended to reduce avoidable transit delay, not to force every signal to flip immediately. Tram requests have higher priority than bus requests and can use stronger preemption behavior. Bus requests are softer: they can extend a compatible green or select their target group at normal transition points, while stop-aware suppression avoids holding cross traffic for buses that are boarding or likely stopping before the signal.

Bus TSP is implemented as a conservative soft-priority feature and repo notes record release-readiness playtesting on bus-only lanes, mixed lanes, vanilla signals, split phasing, protected turns, tram corridors, and exclusive pedestrian phases. Dedicated bus lanes usually produce cleaner detection. Mixed-lane buses are supported, but the detector is more conservative when the bus stop relationship or lane-change target is unclear.

Traffic groups and TSP are intentionally treated as incompatible controls. When an intersection is part of a TLE traffic group, local TSP is suspended so group coordination and green-wave timing remain authoritative. The intersection's saved TSP settings are preserved and can take effect again if the intersection is removed from the group.

If the mod option for TSP diagnostics is enabled, the selected-intersection panel can show recent TSP decisions and write selected diagnostic traces for troubleshooting. Diagnostics are off by default and live in the mod options Diagnostics group.

> [!WARNING]
> There may be pedestrian pathfinding issues at junctions, potentially indicating a bug in the game's node or pathfinding system, not addressed by this mod.

## How To Use

1. Select the Traffic Lights Enhancement button from the top-left toolbar.

<img width="500" height="195" alt="Traffic Lights Enhancement toolbar button" src="docs/images/guide/tle-button.png" />

2. The Traffic Lights Enhancement panel opens. Click a signalized intersection once to select it.

<img width="500" height="371" alt="Traffic Lights Enhancement panel before selecting an intersection" src="https://github.com/user-attachments/assets/19597999-abf0-4f84-ac73-a7c400b8c5fd" />

3. Choose the signal mode and per-intersection options you prefer. Enable Transit Signal Priority for trams or buses only where that source should receive priority.

<img width="420" height="767" alt="Traffic Lights Enhancement selected-intersection options" src="https://github.com/user-attachments/assets/528771b5-12d5-4536-b360-1616d0a7eea7" />

4. Click Save. The selected junction should now operate with the chosen mode and per-intersection options.

## Transit Signal Priority Diagnostics Legend

When Transit Signal Priority diagnostics are enabled, the selected-intersection panel shows the current TSP state for the selected junction. These fields are intended for troubleshooting and are most useful while the game is paused.

| Field | Meaning |
| --- | --- |
| Enabled | Whether TSP is enabled for the selected intersection. |
| Signal state | Current traffic light transition state, such as `Ongoing`, `Ending`, `Changing`, `Beginning`, `Extending`, or `Extended`. |
| Current group | The signal group currently being served. |
| Next group | The signal group the controller is preparing to serve next. `-` means no next group is currently selected. |
| Timer | Current signal timer value. |
| Signal groups | Number of signal groups configured at the intersection. |
| Request | Active TSP request type. `Early` is a fresh approach request; `Latched` is a recently accepted request being carried briefly after the vehicle sample disappears; `None` means no active request. |
| Source | Request source, usually `Track` for trams or `Bus` for bus TSP. |
| Target group | Signal group requested by the transit vehicle. |
| Strength | Request strength used when comparing competing TSP requests. |
| Expiry | How long a latched request can remain active before clearing. |
| Extend current phase | Whether the request can hold the current compatible green. |
| Bus probe | How the detector matched the sampled bus, such as an approach-lane match, connected-approach match, or signaled-lane match. |
| Bus decision | Outcome for bus TSP, such as `Request emitted` or a suppression reason. |
| Bus target group | Signal group mapped to the sampled bus. |
| Bus hits | Number of bus samples contributing to the selected match. |
| Bus lane / Bus vehicle | Internal entity IDs for comparing panel state to trace logs. |
| Bus curve | Sampled bus position along its lane. |
| Bus lane type | Whether the sampled bus is in a mixed or bus-only lane. |
| Bus lane change | Whether the sampled bus appears to be changing lanes. |
| Bus speed | Sampled bus speed. |
| Decision | Final controller decision for the active request, such as extending the current phase, selecting the target phase, or deferring. |
| Base group | Group the controller would normally serve before applying TSP. |
| Selected group | Group selected after considering TSP. |
| Decision target | Requested signal group considered by the decision. |
| Decision source | Request source considered by the decision. |
| Exclusive pedestrian phase | Whether exclusive pedestrian phasing is enabled. This only appears when pedestrian context affects the TSP decision. |
| Active pedestrian protection | Whether an active pedestrian phase is protected from preemption. |
| Pedestrian phase due | A pedestrian phase is waiting and may delay or prevent TSP so pedestrians are not starved. |
| Recent TSP events | Compact history of recent request and decision changes for the selected intersection. The newest event appears first. |

The JSONL trace also includes lower-level troubleshooting context that is not all shown in the panel: the selected signal configuration, traffic-group membership, simulation frame, and lane signal group masks for comparing the trace to the colored lane overlay.

### Common Bus Decisions

| Decision | Meaning |
| --- | --- |
| Request emitted | A bus sample was eligible and generated a TSP request. |
| No eligible bus sample | No current bus sample met detector and eligibility rules. |
| Bus priority disabled | Bus TSP is disabled at this intersection. |
| Suppressed: boarding | The bus appears to be boarding or stopped for service. |
| Suppressed: near-side stop | The bus likely needs to stop before the signal, so holding the light would not help. |
| Suppressed: stop relation unknown | The stop relationship could not be determined safely. |
| Suppressed: lane change ambiguous | The bus appears to be changing lanes, so the target group may be unreliable. |

[^1]: The available modes depend on junction topology. Vanilla and Custom Phases are offered broadly, while predefined advanced modes can be hidden unless the selected junction meets their requirements. Protected Left/Right-Turns require a four-approach junction with straight-through approaches; split-phasing variants are unavailable on rail/track junctions or junctions with more than seven connected edges.
[^2]: This advanced split phasing handles traffic light groups dynamically, considering traffic direction and neighboring lane groups.
