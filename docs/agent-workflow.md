# Agent Workflow

This repository includes repo-local agent instructions so coding assistants can work from the same project rules regardless of client. The canonical entry point is `AGENTS.md`; the canonical skill bodies live under `.agents/skills`.

## Why These Files Exist

TLE Extended has a few compatibility rules that are easy to break from memory: the `C2VM.TrafficLightsEnhancement` assembly and namespace stay stable, save payloads must remain backward compatible, diagnostics should stay opt-in, and user-facing text should flow through `Locale.json`. Repo-local skills make those rules discoverable before an agent edits code.

The compatibility shim files (`CLAUDE.md`, `GEMINI.md`, `.cursor/rules/agents.mdc`, `.cursorrules`, `.windsurfrules`, `.clinerules`, and `.github/copilot-instructions.md`) intentionally point back to `AGENTS.md` instead of duplicating instructions. Keep `AGENTS.md` and `.agents/skills` authoritative.

## Skill Set

- `tle-codebase`: repository orientation, project boundaries, and compatibility invariants.
- `tle-csharp-ecs`: C#, Unity ECS, traffic-light simulation, Transit Signal Priority policy, save data, migrations, and build behavior.
- `tle-ui-localization`: React UI, C# UI bindings, diagnostics panel payloads, option text, tooltips, localization keys, and `Locale.json`.
- `tle-testing-release`: focused verification commands, mod build/toolchain expectations, packaging risk, and release-readiness checks.

Agents should load the smallest relevant set for the task, then prefer the main project docs (`README.md`, `BUILD.md`, `GUIDE.md`, `docs/tsp-architecture.md`, `docs/save-format-contract.md`, and `docs/localization-workflow.md`) over memory.

## Maintenance Rules

- Keep skill bodies concise and procedural; move durable project knowledge into normal docs when humans should read it too.
- Update `AGENTS.md` when adding, renaming, or removing a repo-local skill.
- Keep client-specific shim files as pointers only.
- Update the relevant skill when a project invariant, verification command, UI/localization workflow, or serialized compatibility rule changes.
- Review skill changes like source changes: they affect future code edits even though they do not ship in the mod assembly.
