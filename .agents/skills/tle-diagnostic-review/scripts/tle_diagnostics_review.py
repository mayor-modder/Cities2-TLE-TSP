#!/usr/bin/env python3
"""Summarize TLE TSP diagnostics JSONL traces without mirroring every UI row."""

from __future__ import annotations

import argparse
import json
import os
import re
import sys
import time
from collections import Counter, defaultdict, deque
from dataclasses import dataclass, field
from pathlib import Path
from typing import Any, Iterable


TRACE_NAME = "C2VM.TrafficLightsEnhancement.TspDiagnostics.jsonl"
TRACE_GLOB = "C2VM.TrafficLightsEnhancement.TspDiagnostics*.jsonl"
LOG_PATTERNS = re.compile(
    r"(C2VM\.TrafficLightsEnhancement|TrafficLightsEnhancement|\bTLE\b|Exception|Error|Crash|Failed|fail)",
    re.IGNORECASE,
)


def default_game_root() -> Path:
    local_appdata = os.environ.get("LOCALAPPDATA")
    if local_appdata:
        appdata = Path(local_appdata).parent
    else:
        appdata = Path.home() / "AppData"
    return appdata / "LocalLow" / "Colossal Order" / "Cities Skylines II"


def entity_text(value: Any) -> str:
    if isinstance(value, dict):
        index = value.get("index")
        version = value.get("version")
        if index is not None and version is not None:
            return f"{index}:{version}"
    if value in (None, "", "-"):
        return "-"
    return str(value)


def group_text(value: Any) -> str:
    if value in (None, "", "-", 0, "0"):
        return "G-"
    return f"G{value}"


def read_jsonl(path: Path, tail: int | None = None) -> list[dict[str, Any]]:
    if not path.exists():
        return []

    with path.open("r", encoding="utf-8-sig", errors="replace") as handle:
        lines: Iterable[str]
        if tail is None or tail <= 0:
            lines = handle.readlines()
        else:
            lines = deque(handle, maxlen=tail)

    records: list[dict[str, Any]] = []
    for line in lines:
        text = line.strip()
        if not text:
            continue
        try:
            obj = json.loads(text)
        except json.JSONDecodeError:
            records.append({"_parseError": text[:240]})
            continue
        if isinstance(obj, dict):
            records.append(obj)
    return records


def newest_trace_files(root: Path, include_rotated: bool) -> list[Path]:
    active = root / TRACE_NAME
    if not include_rotated:
        return [active] if active.exists() else []
    return sorted(root.glob(TRACE_GLOB), key=lambda path: path.stat().st_mtime)


@dataclass
class ReviewEvent:
    timestamp: str
    frame: Any
    selected: str
    summary: str
    signal: str
    request: str
    decision: str
    bus: str
    candidates: list[str] = field(default_factory=list)
    notices: list[str] = field(default_factory=list)

    @property
    def signature(self) -> tuple[Any, ...]:
        return (
            self.selected,
            self.summary,
            self.signal,
            self.request,
            self.decision,
            self.bus,
            tuple(self.candidates),
            tuple(self.notices),
        )


def describe_signal(record: dict[str, Any]) -> str:
    traffic_lights = record.get("trafficLights")
    if not isinstance(traffic_lights, dict):
        return "-"
    return (
        f"{traffic_lights.get('state', '-')} "
        f"{group_text(traffic_lights.get('currentGroup'))}->{group_text(traffic_lights.get('nextGroup'))} "
        f"t={traffic_lights.get('timer', '-')}"
    )


def describe_request(record: dict[str, Any]) -> str:
    request = record.get("request")
    if not isinstance(request, dict):
        return "none"

    parts = [
        f"{request.get('kind', '-')}/{request.get('source', '-')}",
        f"target {group_text(request.get('targetGroup'))}",
        f"strength={request.get('strength', '-')}",
        f"expiry={request.get('expiry', '-')}",
    ]
    lane_bits = []
    for label, key in (("S", "signaledLane"), ("A", "approachLane"), ("U", "upstreamLane")):
        lane = request.get(key)
        if lane not in (None, "", "-"):
            lane_bits.append(f"{label}={lane}")
    if lane_bits:
        parts.append("lanes " + " ".join(lane_bits))
    if request.get("extendCurrentPhase") is True:
        parts.append("extend")
    return " ".join(parts)


def describe_decision(record: dict[str, Any]) -> str:
    decision = record.get("decision")
    if not isinstance(decision, dict):
        return "-"
    return (
        f"{decision.get('reason', '-')} "
        f"base {group_text(decision.get('baseGroup'))} "
        f"selected {group_text(decision.get('selectedGroup'))} "
        f"target {group_text(decision.get('targetGroup'))}"
    )


def describe_bus(record: dict[str, Any]) -> str:
    bus = record.get("busApproach")
    if not isinstance(bus, dict):
        return "-"
    text = (
        f"{bus.get('decision', '-')} "
        f"target {group_text(bus.get('targetGroup'))} "
        f"hits={bus.get('hitCount', '-')} "
        f"lane={bus.get('lane', '-')} "
        f"vehicle={bus.get('vehicle', '-')}"
    )
    probe = bus.get("probe")
    if probe:
        text += f" probe={probe}"
    return text


def collect_candidate_sections(record: dict[str, Any]) -> list[str]:
    candidates: list[str] = []

    def visit(value: Any, path: str) -> None:
        if isinstance(value, list):
            interesting_name = any(
                token in path.lower()
                for token in ("candidate", "vehicle", "request", "contender", "bike", "approach")
            )
            if interesting_name:
                for item in value[:8]:
                    if isinstance(item, dict):
                        source = item.get("source") or item.get("kind") or path
                        target = item.get("targetGroup") or item.get("group") or item.get("selectedGroup")
                        vehicle = item.get("vehicle") or item.get("vehicleEntity") or item.get("entity")
                        lane = item.get("lane") or item.get("laneEntity")
                        reason = item.get("reason") or item.get("decision") or item.get("status")
                        bits = [str(source)]
                        if target is not None:
                            bits.append(f"target {group_text(target)}")
                        if vehicle is not None:
                            bits.append(f"vehicle={entity_text(vehicle)}")
                        if lane is not None:
                            bits.append(f"lane={entity_text(lane)}")
                        if reason is not None:
                            bits.append(f"reason={reason}")
                        candidates.append(" ".join(bits))
                    else:
                        candidates.append(f"{path}: {item}")
            for index, item in enumerate(value):
                visit(item, f"{path}[{index}]")
        elif isinstance(value, dict):
            for key, child in value.items():
                visit(child, f"{path}.{key}" if path else key)

    visit(record, "")
    return candidates[:12]


def anomaly_notices(record: dict[str, Any]) -> list[str]:
    notices: list[str] = []
    request = record.get("request") if isinstance(record.get("request"), dict) else None
    decision = record.get("decision") if isinstance(record.get("decision"), dict) else None
    bus = record.get("busApproach") if isinstance(record.get("busApproach"), dict) else None
    group = record.get("trafficGroup") if isinstance(record.get("trafficGroup"), dict) else None

    if group and group.get("isMember"):
        notices.append("local TSP may be paused by traffic-group membership")

    if request:
        source = str(request.get("source", ""))
        lane_evidence = [request.get("signaledLane"), request.get("approachLane"), request.get("upstreamLane")]
        if request.get("kind") in ("Early", "Petitioner") and all(v in (None, "", "-") for v in lane_evidence):
            notices.append("request has no lane evidence")
        if request.get("expiry") == 0:
            notices.append("request latch is expiring")
        if source.lower() == "bus" and bus and bus.get("decision") == "No eligible bus sample":
            notices.append("bus source conflicts with no eligible bus sample")

    if decision:
        selected = decision.get("selectedGroup")
        target = decision.get("targetGroup")
        suppressed = decision.get("preemptionSuppressedByPedestrianPhase") or decision.get(
            "preemptionSuppressedByVehicleFairness"
        )
        if selected and target and selected != target and not suppressed:
            notices.append("selected group differs from target without explicit suppression")
        if decision.get("preemptionSuppressedByPedestrianPhase"):
            notices.append("preemption suppressed by pedestrian protection")
        if decision.get("preemptionSuppressedByVehicleFairness"):
            notices.append("preemption suppressed by vehicle fairness")

    if bus:
        bus_decision = str(bus.get("decision", ""))
        if bus_decision.startswith("Suppressed"):
            notices.append(f"bus request {bus_decision.lower()}")
        elif bus_decision == "Request emitted" and not request:
            notices.append("bus detector emitted request but active request is absent")

    return notices


def build_event(record: dict[str, Any]) -> ReviewEvent:
    return ReviewEvent(
        timestamp=str(record.get("timestampUtc", "-")),
        frame=record.get("simulationFrame", "-"),
        selected=entity_text(record.get("selectedEntity")),
        summary=str(record.get("summary", "-")),
        signal=describe_signal(record),
        request=describe_request(record),
        decision=describe_decision(record),
        bus=describe_bus(record),
        candidates=collect_candidate_sections(record),
        notices=anomaly_notices(record),
    )


def print_event(event: ReviewEvent, blank_after: bool = False) -> None:
    print(f"{event.timestamp} frame={event.frame} selected={event.selected}")
    print(f"  {event.summary}")
    print(f"  signal:   {event.signal}")
    print(f"  request:  {event.request}")
    if event.decision != "-":
        print(f"  decision: {event.decision}")
    if event.bus != "-":
        print(f"  bus:      {event.bus}")
    if event.candidates:
        print("  candidates:")
        for candidate in event.candidates:
            print(f"    - {candidate}")
    if event.notices:
        print("  notices:")
        for notice in event.notices:
            print(f"    - {notice}")
    if blank_after:
        print()


def summarize(records: list[dict[str, Any]], limit_events: int) -> int:
    parsed = [record for record in records if "_parseError" not in record]
    parse_errors = len(records) - len(parsed)
    by_entity: dict[str, list[ReviewEvent]] = defaultdict(list)

    for record in parsed:
        event = build_event(record)
        by_entity[event.selected].append(event)

    print(f"Records parsed: {len(parsed)}")
    if parse_errors:
        print(f"Parse errors: {parse_errors}")
    print(f"Selected entities: {len(by_entity)}")

    for selected, events in sorted(by_entity.items(), key=lambda item: item[1][-1].timestamp):
        sources = Counter()
        targets = Counter()
        decisions = Counter()
        bus_decisions = Counter()
        notices = Counter()

        for event in events:
            if "/" in event.request:
                source = event.request.split("/", 1)[1].split(" ", 1)[0]
                sources[source] += 1
            target_match = re.search(r"target (G\S+)", event.request)
            if target_match:
                targets[target_match.group(1)] += 1
            if event.decision != "-":
                decisions[event.decision.split(" base ", 1)[0]] += 1
            if event.bus != "-":
                bus_decisions[event.bus.split(" target ", 1)[0]] += 1
            notices.update(event.notices)

        first = events[0]
        last = events[-1]
        print()
        print(f"{selected}: {len(events)} records, {first.timestamp} -> {last.timestamp}")
        if sources:
            print("  sources: " + ", ".join(f"{key}={value}" for key, value in sources.most_common()))
        if targets:
            print("  targets: " + ", ".join(f"{key}={value}" for key, value in targets.most_common()))
        if decisions:
            print("  decisions: " + ", ".join(f"{key}={value}" for key, value in decisions.most_common()))
        if bus_decisions:
            print("  bus: " + ", ".join(f"{key}={value}" for key, value in bus_decisions.most_common()))
        if notices:
            print("  notices: " + ", ".join(f"{key}={value}" for key, value in notices.most_common()))
        print("  last:")
        print_event(last)

    if limit_events > 0:
        print()
        print(f"Last {min(limit_events, len(parsed))} meaningful events:")
        previous_signature: tuple[Any, ...] | None = None
        emitted = 0
        for record in parsed[-limit_events * 4 :]:
            event = build_event(record)
            if event.signature == previous_signature:
                continue
            previous_signature = event.signature
            print_event(event, blank_after=True)
            emitted += 1
            if emitted >= limit_events:
                break

    return 0


def tail_log_matches(path: Path, lines: int) -> list[str]:
    if not path.exists():
        return []
    matches: deque[str] = deque(maxlen=lines)
    with path.open("r", encoding="utf-8-sig", errors="replace") as handle:
        for line in handle:
            if LOG_PATTERNS.search(line):
                matches.append(line.rstrip())
    return list(matches)


def print_player_log(root: Path, lines: int) -> None:
    for name in ("Player.log", "Player-prev.log"):
        path = root / name
        matches = tail_log_matches(path, lines)
        if not matches:
            continue
        print()
        print(f"{name}: last {len(matches)} matching lines")
        for line in matches:
            print(f"  {line}")


def watch(path: Path, seconds: float, poll: float, verbose: bool) -> int:
    deadline = time.monotonic() + seconds if seconds > 0 else None
    last_size = path.stat().st_size if path.exists() else 0
    previous_selected = None
    previous_signature = None
    print(f"Watching {path}")

    while deadline is None or time.monotonic() < deadline:
        if path.exists():
            size = path.stat().st_size
            if size < last_size:
                last_size = 0
            if size > last_size:
                with path.open("r", encoding="utf-8-sig", errors="replace") as handle:
                    handle.seek(last_size)
                    lines = handle.readlines()
                last_size = size
                for line in lines:
                    try:
                        record = json.loads(line)
                    except json.JSONDecodeError:
                        continue
                    if not isinstance(record, dict):
                        continue
                    event = build_event(record)
                    important = event.selected != previous_selected
                    if event.selected != previous_selected:
                        previous_selected = event.selected
                    if event.notices or event.decision != "-" or "target" in event.request or "Suppressed" in event.bus:
                        important = True
                    if event.signature == previous_signature:
                        important = False
                    if important or verbose:
                        print_event(event, blank_after=True)
                        previous_signature = event.signature
        time.sleep(poll)
    return 0


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--root", type=Path, default=default_game_root(), help="Cities II LocalLow root")
    parser.add_argument("--jsonl", type=Path, help="Specific diagnostics JSONL file")
    parser.add_argument("--include-rotated", action="store_true", help="Include rotated diagnostics files")
    parser.add_argument("--tail", type=int, default=250, help="Records to read from each file")
    parser.add_argument("--events", type=int, default=8, help="Meaningful recent events to print")
    parser.add_argument("--watch", type=float, default=0, help="Watch active trace for this many seconds; 0 disables")
    parser.add_argument("--poll", type=float, default=0.75, help="Watch poll interval in seconds")
    parser.add_argument("--with-player-log", action="store_true", help="Print matching Player.log lines")
    parser.add_argument("--log-lines", type=int, default=40, help="Matching Player log lines to print")
    parser.add_argument("--verbose", action="store_true", help="Print every live event instead of only important events")
    args = parser.parse_args(argv)

    if args.watch:
        path = args.jsonl or args.root / TRACE_NAME
        return watch(path, args.watch, args.poll, args.verbose)

    files = [args.jsonl] if args.jsonl else newest_trace_files(args.root, args.include_rotated)
    records: list[dict[str, Any]] = []
    for path in files:
        if not path or not path.exists():
            print(f"Missing diagnostics file: {path}", file=sys.stderr)
            continue
        file_records = read_jsonl(path, args.tail)
        print(f"Loaded {len(file_records)} records from {path}")
        records.extend(file_records)

    if records:
        summarize(records, args.events)
    else:
        print("No diagnostics records found.")

    if args.with_player_log:
        print_player_log(args.root, args.log_lines)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
