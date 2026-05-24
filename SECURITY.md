# Security Policy

## Supported Versions

Security fixes are applied to the default branch first. When practical, fixes are
also included in the latest public release.

| Version | Supported |
| --- | --- |
| Default branch | Yes |
| Latest public release | Yes |
| Older releases | Best effort |

## Reporting a Vulnerability

Please do not open a public issue with exploit details.

Use GitHub private vulnerability reporting for this repository:

https://github.com/mayor-modder/Cities2-TrafficLightsEnhancement-Extended/security/advisories/new

You can also reach the same form from the repository's Security tab by choosing
**Report a vulnerability**.

Do not post proof-of-concept payloads, local file paths, tokens, save data, or
other sensitive details publicly.

Please include:

- the affected version, commit, or release
- operating system and game version, if relevant
- a short description of the impact
- reproduction steps or a minimal proof of concept
- whether the issue requires a local Cities: Skylines II install, a specific
  mod configuration, or another mod

You can expect an initial response as soon as maintainers are available. We will
try to confirm the issue, discuss impact and timeline, and credit reporters who
want credit.

## Scope

Cities2 Traffic Lights Enhancement Extended is a Cities: Skylines II mod with
C# game code, TypeScript UI code, GitHub Actions release automation, and managed
game/toolchain dependencies.

Reports are especially useful when they involve:

- unsafe handling of mod settings, presets, localization, or UI-provided data
- save data corruption or unintended writes outside this mod's expected data
  paths
- crashes or denial of service caused by malformed settings, presets, or saved
  traffic-light data
- release workflow or artifact integrity issues
- dependency vulnerabilities that affect this mod at runtime or during release
- leaking local paths, user data, generated files, or secrets through logs,
  release artifacts, UI output, or diagnostics

The following are usually out of scope unless they bypass a security control in
this repository:

- vulnerabilities in Cities: Skylines II, Unity, Paradox Mods, GitHub, or other
  third-party platforms
- malicious mod code intentionally installed by a user
- issues requiring physical access to a user's machine
- social engineering
- dependency reports that do not affect this project
- denial-of-service reports based only on excessive automated traffic

## Safe Configuration Notes

- Install releases from trusted sources.
- Review configuration files, presets, and any locally modified mod files before
  sharing them.
- Do not publish logs, save data, game installation paths, or locally extracted
  managed assemblies unless you have reviewed them for sensitive information.

## Disclosure

Please allow a reasonable coordination period before public disclosure. After a
fix is available, maintainers may publish a GitHub advisory, release notes, or a
public issue describing the impact and upgrade path without exposing sensitive
details unnecessarily.
