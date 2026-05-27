# TLE Code Documentation Skill Design

## Purpose

Create a repo-local skill named `tle-code-documentation` for writing maintainer-facing documentation about TLE Extended source code and inherited behavior.

The skill should help future agents and human maintainers understand code that is currently difficult to reason about, especially inherited TLE/TLEE behavior. It should teach agents to map a subsystem before writing prose, document evidence carefully, and leave useful breadcrumbs when intent cannot be recovered.

This skill is not for player-facing documentation. `GUIDE.md`, release notes, marketing text, and localization copy remain outside this skill unless they are being referenced as evidence for a code contract.

## Readers

The target readers are future coding agents, maintainers, reviewers, and release testers.

The documentation produced by this skill should answer questions like:

- What owns this behavior?
- What data flows through it?
- Which invariants must not be broken?
- Which parts are verified by tests, code, or in-game behavior?
- Which parts are inferred or still unknown?
- What should a future maintainer avoid "simplifying" without deeper verification?

## Skill Trigger

The skill should use this frontmatter:

```yaml
---
name: tle-code-documentation
description: "Use when documenting TLE Extended source code or inherited behavior for future maintainers and coding agents; auditing under-documented C#/ECS/UI systems; adding useful inline comments, XML docs, architecture notes, or evidence-backed maintainer documentation. Not for player-facing guide copy."
---
```

## Scope

The skill should apply when the user asks to document:

- a specific file or method
- a subsystem or surface area
- a confusing behavior
- an inherited feature whose behavior is only partly understood
- maintainer notes for a change already being made
- evidence gaps found while reviewing or testing code

The agent should accept vague input such as "document custom phases" and resolve the surface by searching the repo, reading relevant docs, and tracing entry points.

## Artifacts

The skill should guide agents toward the smallest useful artifact:

- Inline comments for local traps, invariants, or game API quirks.
- XML doc comments for helper APIs, shared types, or methods future agents are likely to call.
- Maintainer docs under `docs/*.md` for subsystem maps, state machines, data flow, or inherited architecture.
- PR or issue notes when behavior is too uncertain for durable documentation.

It should avoid broad comment sweeps and syntax narration. Comments should document intent, boundaries, danger, and evidence, not restate what the next line of code does.

## Core Workflow

1. Identify the reader and the documentation artifact.
2. Resolve the code surface.
3. Map before writing:
   - entry points
   - owners and systems
   - state/components/data structures
   - data flow
   - invariants
   - tests
   - docs and UI/localization contracts
   - evidence gaps
4. Choose the smallest artifact that will help the next maintainer.
5. Write sparse, evidence-backed documentation.
6. Verify formatting and, when appropriate, build/tests.

The skill should explicitly warn agents not to document a subsystem after reading only one method.

## Evidence Labels

Maintainer docs should mark the source of important claims:

- `Verified from code`
- `Verified by tests`
- `Verified in-game`
- `Inferred from current behavior`
- `Unknown`
- `Needs in-game evidence`

Inline comments do not need formal labels, but they should avoid claiming author intent unless the evidence supports it.

## Last-Resort History Check

Git history should be a hail mary, not the default workflow. Use it only when current code, tests, docs, and diagnostics do not explain why behavior exists.

Good triggers:

- code looks wrong, but removing it could break compatibility
- a state transition or mask operation has no obvious purpose
- behavior survived multiple rewrites and may be intentional
- the agent is about to write "probably" in maintainer docs
- it matters whether behavior came from slyh-era TLE or a later TLEE rewrite

Suggested commands:

```powershell
git blame -- path/to/file.cs
git blame -L <start>,<end> -- path/to/file.cs
git log --follow -- path/to/file.cs
git log -L :MethodName:path/to/file.cs
git show <sha>
```

Rules:

- Start with the smallest relevant line range.
- Use `git log -L` for methods when available.
- Use `git show` only on promising commits.
- Treat commit messages as clues, not facts.
- If history is vague, write that history did not explain the behavior.
- Do not document author intent unless commit, code, or linked context supports it.
- If blame points to slyh-era code, verify that surrounding behavior survived later rewrites before preserving an explanation.
- If blame points to bruceyboy-era bulk rewrites or vague commits, rely more heavily on current code, tests, diagnostics, and in-game evidence.

## Mystery Code Protocol

If current code, tests, docs, diagnostics, and targeted git history still do not explain why a behavior exists, do not invent intent.

The agent should:

1. Document only what is known.
2. Mark the unclear part as `Unknown` or `Needs in-game evidence`.
3. Open a GitHub issue when the mystery matters.

The issue should include:

- file, method, and line range
- observed behavior
- evidence checked
- why the uncertainty matters
- what would confirm it

If GitHub access is unavailable, the agent should leave a complete issue body for a maintainer to file instead of letting the mystery stay in chat.

## Red Flags

The skill should tell agents to stop and remap when they catch themselves thinking:

- "I understand this after reading one method."
- "I'll add comments everywhere."
- "This probably means..."
- "The variable name explains it."
- "Future agents can just search for it."
- "This inherited behavior is obviously wrong."

## Common Mistakes To Prevent

- Writing a giant prose block above a confusing method instead of documenting the actual invariant.
- Claiming intent when only behavior is known.
- Documenting inherited behavior without checking callers, tests, and docs.
- Mixing maintainer docs with player-facing guide text.
- Leaving uncertainty only in chat instead of in docs or an issue.
- Using blame as a shortcut before understanding current behavior.

## Validation Plan

Use the `superpowers:writing-skills` RED/GREEN/REFACTOR model.

Baseline scenario:

- Ask an agent without the skill to document a messy surface such as custom phases, traffic groups, or another inherited subsystem.
- Capture whether it comments too broadly, misses tests/callers, overstates intent, or fails to mark evidence gaps.

Skill scenario:

- Ask another agent to use the new skill on the same or similar surface.
- Success means the agent maps before writing, produces a small useful artifact, separates evidence from inference, and creates a follow-up issue when behavior remains unexplained.

No helper scripts or bundled references are needed initially. The skill should be a concise `SKILL.md` only.

## Repository Changes

Implementation should add:

- `.agents/skills/tle-code-documentation/SKILL.md`
- `AGENTS.md` entry for the new skill
- `docs/agent-workflow.md` entry for the new skill

Implementation should not update client shim files unless they need to point at canonical instructions. The repo keeps `AGENTS.md` and `.agents/skills` authoritative.

## Approval

This design reflects the approved direction from the maintainer conversation on 2026-05-27.
