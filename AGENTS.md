# Agent Orientation

This repository contains repo-local agent skills in `.agents/skills`. At the start of a new session, load the smallest relevant set before editing:

- `tle-codebase` for repository orientation, project boundaries, and core compatibility invariants.
- `tle-csharp-ecs` for C#, Unity ECS, traffic-light simulation, TSP policy, save data, and migration work.
- `tle-ui-localization` for React UI, C# UI bindings, diagnostics panel changes, options text, and `Locale.json`.
- `tle-testing-release` for focused test selection, build/toolchain expectations, and release checks.
- `tle-code-review` for PR/branch review rounds, multi-agent reviewer coordination, review-fix verification, and GitHub review thread hygiene.
- `tle-code-documentation` for maintainer-facing source documentation, inherited behavior audits, useful inline comments/XML docs, architecture notes, and mystery-code follow-up issues.

Prefer existing repo docs over memory. `README.md`, `BUILD.md`, `GUIDE.md`, `docs/tsp-architecture.md`, `docs/save-format-contract.md`, and `docs/localization-workflow.md` are the main source files future agents should read on demand.

Before implementing a feature, bugfix, or GitHub issue, work on a dedicated task branch and preferably in a separate git worktree. Do not make issue/feature/fix edits directly on `main`; if the current checkout is `main`, create or switch to an isolated branch/worktree first. If the tree is dirty, preserve the user's work and ask before moving or mixing unrelated changes.

These skills use the portable `SKILL.md` layout with YAML `name` and `description` frontmatter. If an agent does not auto-discover repo-local skills, read the relevant `SKILL.md` file from `.agents/skills/<skill-name>/SKILL.md` manually.

Client-specific files such as `CLAUDE.md`, `GEMINI.md`, `.cursor/rules/agents.mdc`, `.cursorrules`, `.windsurfrules`, `.clinerules`, and `.github/copilot-instructions.md` are compatibility shims. Keep this file and `.agents/skills` canonical rather than duplicating project rules into those shims.

See `docs/agent-workflow.md` for why the repo-local skills exist, what each skill covers, and how to keep the shims and canonical instructions in sync.

When an AI agent authors GitHub-facing text, including pull request bodies, pull request comments, or review comments, prefix the text with `*Written by <AgentName>.*` using that agent's actual name, such as `Codex`, `Claude`, or `Gemini`.

Pull requests should be squash-merged into `main`. Keep individual branch commits useful for review, but expect the PR title/body and final squash commit to carry the durable history. Delete merged head branches after merge unless a maintainer asks to keep one for follow-up work.

Keep changes scoped and preserve user work in the current tree. This mod intentionally keeps `C2VM.TrafficLightsEnhancement` assembly/root namespace identifiers for compatibility with existing Traffic Lights Enhancement saves and configured intersections.
