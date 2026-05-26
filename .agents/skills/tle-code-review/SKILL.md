---
name: tle-code-review
description: "Use when reviewing a TLE Extended pull request, branch, commit range, or review-fix follow-up; coordinating internal, Claude Code, Gemini CLI, or other available reviewer agents; or verifying that review fixes use test-first discipline."
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
4. Offer multi-agent review for broad or release-facing changes.
   - Use an internal code-review subagent when available and the user permits subagents.
   - Discover external CLIs with shell commands such as `where.exe claude`, `where.exe gemini`, `claude --version`, and `gemini.cmd --version`.
   - Run external review commands read-only. Prefer regular Claude Code or Gemini CLI review over paid/limited hosted review products unless the user explicitly asks.
5. Synthesize findings:
   - Separate confirmed issues, plausible risks, false positives, and nits.
   - Verify external findings against local code before changing anything.
   - Preserve review URLs, session links, command output, and commit SHAs in the PR when useful.

## External Reviewer Prompts

Use compact prompts that point at the repo, PR URL, and base/head commits. Do not inline huge diffs unless the tool requires it.

Example Claude Code command:

```powershell
claude -p "<review prompt>" --permission-mode plan --output-format text
```

Example Gemini CLI command:

```powershell
gemini.cmd --prompt "<review prompt>" --approval-mode plan
```

The prompt should say:

- Review only; do not edit files.
- Findings first, ordered by severity.
- Include file and line references.
- Focus on bugs, regressions, missing tests, compatibility, UI/localization, serialization, and release risk.
- Say clearly when there are no issues.

Do not run `claude ultrareview` or other limited-budget hosted review commands unless the user explicitly asks for that specific review mode.

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
