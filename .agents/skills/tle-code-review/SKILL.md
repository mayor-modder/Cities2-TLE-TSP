---
name: tle-code-review
description: "Use when reviewing a TLE Extended pull request, branch, commit range, or review-fix follow-up; coordinating internal, Claude Code, Codex, Antigravity, or other available reviewer agents; or verifying that review fixes use test-first discipline."
---

# TLE Code Review

## Overview

Use this skill for code-review rounds on TLE Extended branches and pull requests. Load `tle-codebase` first, then load the narrow TLE skill for each touched area: `tle-csharp-ecs`, `tle-ui-localization`, and/or `tle-testing-release`.

Reviews should prioritize behavior, compatibility, missing tests, serialization risk, UI/localization mismatches, and release risk. Keep praise and summaries secondary to actionable findings.

## Review Ladder

1. Resolve the base and head:
   - For PRs, use `gh pr view <number> --json baseRefOid,headRefOid,url,title,isDraft,mergeable,statusCheckRollup`.
   - For local branches, use `git merge-base HEAD main` and `git rev-parse HEAD`.
2. Inspect the diff before delegating:
   - `git diff --stat <base>..<head>`
   - `git diff --name-only <base>..<head>`
   - Read the risky files directly before accepting any external finding.
3. Run or confirm focused verification:
   - Use `tle-testing-release` to choose commands.
   - If checks were already run, record exact command names and results.
4. Offer multi-agent review for broad or release-facing changes (see "Multi-Agent Review Offer").
   - Use an internal code-review subagent when available and the user permits subagents.
   - Before opt-in, detect external reviewers with PATH lookup only: `Get-Command codex`, `Get-Command claude`, `Get-Command agy`. Do not run any external CLI command yet.
   - After opt-in, run external review commands read-only. Prefer local CLI review (Codex, Claude Code, Antigravity) over paid or limited hosted review products unless the user explicitly asks.
5. Synthesize findings:
   - Separate confirmed issues, plausible risks, false positives, and nits.
   - Verify external findings against local code before changing anything.
   - Preserve review URLs, session links, command output, and commit SHAs in the PR when useful.

## Multi-Agent Review Offer

For a large diff, branch, PR, or release-readiness pass, offer multi-agent review. Detect candidates with PATH lookup only before the user opts in:

- `Get-Command codex`, `Get-Command claude`, `Get-Command agy`.
- Do not run any external CLI command — no `--version`, `--help`, print mode, or review mode — until the user approves the offer. External reviewers may use network access, credentials, tokens, paid plans, or local config, so ask first.

Scale the offer to what is installed:

- Two or more external reviewers available: offer a 3-way review (internal subagent plus two external CLIs).
- One external reviewer available: offer a 2-way review (internal subagent plus that CLI).
- No external reviewer available: continue with the internal/normal review; this is not a problem.

Prefer diverse reviewers and documented noninteractive review modes. Confirm exact flags with `--help` only after opt-in:

- Codex: `codex review` with a review prompt.
- Claude Code: `claude -p` / `claude --print` with a review prompt.
- Antigravity: `agy --print` with a review prompt.

Treat Antigravity (`agy`) as file-output-first. Its `--print` stdout can be empty even when the model ran, and `--log-file` is an execution log for troubleshooting, not the final review artifact. Prompt `agy` to write the final review to a specific temporary file, redirect stdout to a separate fallback capture, and read the log only if both are missing or unclear:

```powershell
agy --print "<review prompt>; write the full review to $env:TEMP\tle-agy-review.md" 1> $env:TEMP\tle-agy-stdout.txt
```

Read `$env:TEMP\tle-agy-review.md` first, fall back to `$env:TEMP\tle-agy-stdout.txt`, then the execution log. Offer to remove the temporary files after synthesizing the final review; keep them if the user wants an audit trail.

Do not run `claude ultrareview`, `/code-review ultra`, or other limited-budget hosted review modes unless the user explicitly asks for that specific mode.

## External Reviewer Prompts

Use compact prompts that point at the repo, PR URL, and base/head commits. Do not inline huge diffs unless the tool requires it.

Example Claude Code command:

```powershell
claude -p "<review prompt>" --permission-mode plan --output-format text
```

Example Codex command:

```powershell
codex review "<review prompt>"
```

Example Antigravity command (file-output-first; see "Multi-Agent Review Offer"):

```powershell
agy --print "<review prompt>; write the full review to $env:TEMP\tle-agy-review.md" 1> $env:TEMP\tle-agy-stdout.txt
```

The prompt should say:

- Review only; do not edit files.
- Findings first, ordered by severity.
- Include file and line references.
- Focus on bugs, regressions, missing tests, compatibility, UI/localization, serialization, and release risk.
- Say clearly when there are no issues.

## Internal Subagent Review

When using an internal reviewer, follow the spirit of `superpowers:requesting-code-review`:

- If that Superpowers skill is available in the session, load it before spawning the reviewer.
- Give the reviewer a precise description of the branch or PR.
- Include base SHA, head SHA, requirements, and expected behavior.
- Do not pass session history or your conclusions as evidence.
- Ask for findings first, then open questions and residual risks.
- Treat the reviewer as independent input, not an authority.

## Handling Findings

For every finding:

1. Reproduce or inspect the cited code path.
2. Decide whether it is confirmed, plausible but unproven, a false positive, or a product/design tradeoff.
3. For confirmed bugs or important risks, fix before merge.
4. For false positives, document the reason briefly in the PR or final review summary.
5. For design tradeoffs, check the current docs and UI disclosure before deciding whether code should change.

## Review Fix Discipline

Behavior-changing fixes should follow test-first discipline:

- Write or update a focused test that would fail before the fix.
- Run that test and confirm the failure mode when practical.
- Implement the smallest fix that passes.
- Rerun the focused test, then the relevant broader verification from `tle-testing-release`.

Exceptions are documentation, comments, labels, and purely mechanical PR-thread cleanup. For those, use `git diff --check` and any targeted docs/UI checks that fit the change.

If a review fix cannot reasonably be test-first, say why in the PR summary or final answer.

## PR Thread Hygiene

- Resolve review threads only after the fix is committed and pushed, or after a documented technical pushback.
- For GitHub review threads, use thread-aware reads before resolving; flat PR comments do not show resolution state.
- Prefix GitHub-facing text written by an AI agent, including pull request bodies, pull request comments, and review comments, with `*Written by <AgentName>.*` using that agent's actual name.
- Add a concise PR comment after a review round summarizing reviewers consulted, fixes made, verification, and known residual risks.
- Before merge, confirm the PR is not draft, is mergeable, has green required checks, and has no unresolved actionable review threads.
