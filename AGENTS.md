# Agent Orientation

This repository contains repo-local agent skills in `.agents/skills`. At the start of a new session, load the smallest relevant set before editing:

- `tle-codebase` for repository orientation, project boundaries, and core compatibility invariants.
- `tle-csharp-ecs` for C#, Unity ECS, traffic-light simulation, TSP policy, save data, and migration work.
- `tle-ui-localization` for React UI, C# UI bindings, diagnostics panel changes, options text, and `Locale.json`.
- `tle-testing-release` for focused test selection, build/toolchain expectations, and release checks.

Prefer existing repo docs over memory. `README.md`, `BUILD.md`, `GUIDE.md`, `docs/tsp-architecture.md`, `docs/save-format-contract.md`, and `docs/localization-workflow.md` are the main source files future agents should read on demand.

These skills use the portable `SKILL.md` layout with YAML `name` and `description` frontmatter. If an agent does not auto-discover repo-local skills, read the relevant `SKILL.md` file from `.agents/skills/<skill-name>/SKILL.md` manually.

Client-specific files such as `CLAUDE.md`, `GEMINI.md`, `.cursor/rules/agents.mdc`, `.cursorrules`, `.windsurfrules`, `.clinerules`, and `.github/copilot-instructions.md` are compatibility shims. Keep this file and `.agents/skills` canonical rather than duplicating project rules into those shims.

Keep changes scoped and preserve user work in the current tree. This mod intentionally keeps `C2VM.TrafficLightsEnhancement` assembly/root namespace identifiers for compatibility with existing Traffic Lights Enhancement saves and configured intersections.
