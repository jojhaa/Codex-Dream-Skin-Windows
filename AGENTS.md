# Codex Dream Skin Windows maintainer notes

Instructions for AI and human maintainers working in this repository.

## Required project records

- Update `DEVELOPMENT_PROGRESS.md` after every completed development task.
- Update `DEVELOPMENT_LOG.md` with the implementation summary and validation evidence.
- Update `CHANGELOG.md` for user-visible release changes.
- Keep release notes user-focused: Added, Improved, Fixed, Safety, and Validation.

## Windows scope

- This repository supports Windows only.
- Keep runtime code, documentation, issue templates, tests, and release artifacts under the Windows scope.
- The supported application is the official Microsoft Store Codex Desktop app.
- Discover Store package paths dynamically; never bind runtime behavior to one Codex version directory.

## Safety boundary

- Apply themes through loopback CDP only.
- Never modify Codex binaries, `WindowsApps`, `app.asar`, or application signatures.
- Never silently change API Base URLs, API keys, authentication files, or account data.
- Validate package and process identity before guarded takeover or process-close operations.
- Do not commit screenshots containing private chats, account names, project names, or local paths.

## Development

- Preserve hot reload and restore behavior when changing the theme engine.
- Keep the native manager and injected renderer schema compatible.
- Prefer existing scripts and tests over one-off replacements.
- Run `windows/tests/run-tests.ps1` for functional changes.
- Build `windows/app/CodexDreamSkin/CodexDreamSkin.csproj` in Release mode for manager changes.
