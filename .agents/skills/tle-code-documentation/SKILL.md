---
name: tle-code-documentation
description: "Use when documenting TLE Extended source code or inherited behavior for future maintainers and coding agents; auditing under-documented C#/ECS/UI systems; adding useful inline comments, XML docs, architecture notes, or evidence-backed maintainer documentation. Not for player-facing guide copy."
---

# TLE Code Documentation

## Overview

Use this skill for maintainer-facing documentation of TLE Extended code and inherited behavior. Load `tle-codebase` first, then load the narrow repo skill for the surface being documented: `tle-csharp-ecs`, `tle-ui-localization`, `tle-testing-release`, or `tle-code-review`.

Core principle: map before writing, document only what evidence supports, and leave durable breadcrumbs for the next maintainer.

## Not For

- Player-facing `GUIDE.md` copy.
- Release notes or marketing text.
- Localization wording, except when documenting a code/UI contract.
- Broad comment sweeps that narrate syntax.

## Workflow

1. Identify the reader and artifact: inline comment, XML doc, `docs/*.md`, PR note, or GitHub issue.
2. Resolve the surface:
   - For a file or method, inspect callers, tests, and nearby docs.
   - For a subsystem, use `rg` to find entry points, state, UI bindings, tests, and docs.
   - For a behavior question, trace from UI/input through policy/runtime output.
3. Map before writing:
   - entry points and owners
   - state, components, DTOs, or serialized data
   - data flow and transitions
   - invariants and compatibility constraints
   - tests and in-game evidence
   - docs/UI/localization contracts
   - unknowns and evidence gaps
4. Choose the smallest useful artifact.
5. Write sparse documentation that explains intent, boundaries, danger, and evidence.
6. Verify with `git diff --check`; run build/tests when comments touch syntax, public contracts, or behavior claims.

Do not document a subsystem after reading only one method.

## Artifact Choices

| Artifact | Use when |
| --- | --- |
| Inline comment | A local trap, invariant, game API quirk, or compatibility constraint would be easy to break. |
| XML doc | A helper API, shared type, or method is likely to be called by future maintainers. |
| `docs/*.md` | The topic is a subsystem, state machine, data flow, or inherited architecture. |
| PR or review note | The information is relevant to the current change but not durable enough for repo docs. |
| GitHub issue | The behavior remains mysterious after code, docs, tests, diagnostics, and targeted history checks. |

## Evidence Labels

Use these labels in maintainer docs when claims matter:

- `Verified from code`
- `Verified by tests`
- `Verified in-game`
- `Inferred from current behavior`
- `Unknown`
- `Needs in-game evidence`

Inline comments do not need formal labels, but do not claim author intent unless code, tests, commits, or linked context support it.

## Useful Comments

Good comments explain:

- why behavior exists
- save or compatibility constraints
- Cities: Skylines II API quirks
- 1-based game signal groups versus 0-based policy indexes
- group masks and lane matching assumptions
- state-machine transitions
- "do not simplify this" traps
- evidence gaps future maintainers must preserve

Bad comments restate syntax, describe obvious variable names, or turn one confusing method into a wall of prose.

## Last-Resort History Check

Use git history only when current code, tests, docs, and diagnostics do not explain why behavior exists.

Good triggers:

- code looks wrong, but removing it could break compatibility
- a state transition or mask operation has no obvious purpose
- behavior survived multiple rewrites and may be intentional
- you are about to write "probably" in maintainer docs
- it matters whether behavior came from slyh-era TLE or a later TLEE rewrite

Useful commands:

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

If current code, tests, docs, diagnostics, and targeted git history still do not explain why behavior exists, do not invent intent.

1. Document only what is known.
2. Mark the unclear part as `Unknown` or `Needs in-game evidence`.
3. Open a GitHub issue when the mystery matters.

Issue body template:

```markdown
*Written by Codex.*

## Mystery

[One-sentence description of the unclear behavior.]

## Location

- File:
- Method/area:
- Lines:

## What We Know

-

## Evidence Checked

- Code:
- Tests:
- Docs:
- Diagnostics/in-game:
- Git history:

## Why It Matters

-

## What Would Confirm It

-
```

If GitHub access is unavailable, leave this complete issue body in the final response or PR note instead of letting the mystery stay only in chat.

## Red Flags

Stop and remap when you catch yourself thinking:

- "I understand this after reading one method."
- "I'll add comments everywhere."
- "This probably means..."
- "The variable name explains it."
- "Future agents can just search for it."
- "This inherited behavior is obviously wrong."

## Common Mistakes

- Writing a giant prose block above a confusing method instead of documenting the invariant.
- Claiming intent when only behavior is known.
- Documenting inherited behavior without checking callers, tests, and docs.
- Mixing maintainer docs with player-facing guide text.
- Leaving uncertainty only in chat instead of docs, a PR note, or an issue.
- Using blame as a shortcut before understanding current behavior.
