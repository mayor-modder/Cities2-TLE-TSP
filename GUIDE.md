> [!NOTE]
> Currently, the mod only supports three-way and four-way junctions. Junctions of other types will offer fewer options in the mod.

> [!TIP]
> Most controls change the sequencing or grouping of signals. Dynamic mode and Transit Signal Priority can also affect phase selection or timing within their configured limits.

## Introduction
TLE Extended introduces advanced traffic light controls for Cities: Skylines II, allowing for more explicit traffic management at supported junctions. It supports both Left-Hand Traffic (LHT) and Right-Hand Traffic (RHT), and it is intended to remain compatible with intersections already configured in Traffic Lights Enhancement.

## Modes

| Mode | Description |
| --- | --- |
| Vanilla | Operates like the base game.<br>LHT: protected straight, protected left, and permissive right.<br>RHT: protected straight, protected right, and permissive left. |
| Split-Phasing | Only one road has a green light at a time. |
| Advanced Split Phasing[^1] | Similar to Split Phasing, with additional protected turns for the other road at the same time.[^2] |
| Protected Left/Right-Turns[^1] | LHT: Centre lanes perform a protected left turn first, followed by normal traffic flow including straight and right turns.<br>RHT: Centre lanes perform a protected right turn first, followed by normal traffic flow including straight and left turns.<br>[Video Illustration](https://www.youtube.com/watch?v=CIw0Au8qFQ8) |

## Options

| Option | Description |
| --- | --- |
| Allow Turning on Red | Allow vehicles to turn left (in LHT) or right (in RHT) when the signal is red. |
| Give Way to Oncoming Vehicles<br>(Only for vanilla signals) | Require vehicles to give way to oncoming traffic when turning.<br>Note: Although drivers are required to give way, their aggressive behavior may reduce the effectiveness of this option at busy junctions. |
| Exclusive Pedestrian Phase | A dedicated phase for pedestrian crossings, stopping all vehicular traffic. |
| Pedestrian Phase Duration | Sets the duration of the green light for pedestrians.<br>Only available when the "Exclusive Pedestrian Phase" option is enabled.<br>Note: Pedestrian traffic lights are not "smart" and will not extend the green signal. |

## Transit Signal Priority

Transit Signal Priority is configured per intersection. The panel provides separate source toggles for trams and buses, so each intersection can enable either source independently.

| Source Option | Description |
| --- | --- |
| Enable for trams | Allows approaching trams to request signal priority at this intersection. TSP may extend the current compatible phase or preempt toward a tram-serving phase, while respecting an already-active exclusive pedestrian phase. |
| Enable for buses | Allows approaching buses to request soft signal priority at this intersection. Bus priority may hold an already-serving green or select the bus-serving group at normal transition points, but it does not use aggressive tram-style preemption. |

TSP is intended to reduce avoidable transit delay, not to force every signal to flip immediately. Tram requests have higher priority than bus requests and can use stronger preemption behavior. Bus requests are softer: they can extend a compatible green or select their target group at normal transition points, while stop-aware suppression avoids holding cross traffic for buses that are boarding or likely stopping before the signal.

If the mod option for TSP diagnostics is enabled, the selected-intersection panel can show recent TSP decisions and write selected diagnostic traces for troubleshooting. Diagnostics are off by default and live in the mod options Diagnostics group.

> [!WARNING]
> There may be pedestrian pathfinding issues at junctions, potentially indicating a bug in the game's node or pathfinding system, not addressed by this mod.

## How To Use

1. Open the Roads Tool, switch to the Road Services tab, and select "Traffic Lights"

![Screenshot 2023-12-10 102831](https://github.com/primeinc/Cities2-Various-Mods/assets/80482978/de6a9184-d340-4371-82c9-ef6731a69630)

2. A small window should appear in the top-left corner of your screen. Move your cursor to any existing junction and press the left mouse button

![Screenshot 2023-12-10 103024](https://github.com/primeinc/Cities2-Various-Mods/assets/80482978/c0beae47-9175-4a31-aad4-ea169f81e1e7)

3. Select the signal mode and options you prefer. Enable Transit Signal Priority for trams or buses only on intersections where that source should receive priority.

![Screenshot 2023-12-10 103213](https://github.com/primeinc/Cities2-Various-Mods/assets/80482978/ee258c53-0ab4-43a2-a9b8-2ed07a792c1a)

4. Save the selected junction. It should now operate with the chosen mode and per-intersection options.

[^1]: Advanced Split Phasing and Protected Left/Right-Turns are unavailable at complex junctions, such as those with tram tracks.
[^2]: This advanced split phasing handles traffic light groups dynamically, considering traffic direction and neighboring lane groups.
