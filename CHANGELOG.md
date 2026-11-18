# Changelog

All notable changes to the Rewordly project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [2.1.0] - 2026-11-18

### Added

- **Rewrite as AI agent prompt** action menu entry with five variants: General agent, Coding agent, Research agent, System prompt, and Chat assistant, plus an **Image AI** section with Image generation and Image editing.
- One-click releases: **Actions › Release › Run workflow** (or `gh workflow run release.yml -f bump=major|minor|patch`) tags, builds all targets and publishes the GitHub Release, and pushes the `Rewordly.Core` NuGet package to GitHub Packages, then commits the new version into the README, project files and CHANGELOG.
- Role/persona instructions in the AI prompts.
- `prompts/agent-prompt/` folder of plain-text templates that contributors can review and extend without code changes; each file adds a menu variant and is editable under Settings › Prompts.

## [2.0.0] - 2026-10-24

### Added

- Independent cross-platform rewrite in C# with .NET 8 and Avalonia UI.
- Global keyboard capture using SharpHook across Windows, macOS, and Linux (X11).
- Context menu displayed at cursor position upon double-copy trigger.
- Alternative trigger for text capture without modifying clipboard history.
- Preconfigured AI provider presets for Google Gemini, OpenAI, OpenRouter, Groq, and Ollama.
- Generic OpenAI-compatible custom server support.
- Encrypted credential storage using Windows DPAPI, macOS Keychain, and Linux Secret Service.
- Interactive Result window with diff review, edit mode, and direct text replacement.
- Interactive Chat window with prompt memory and direct paste-back support.
- Text-to-speech reading support with speed adjustments and multi-language detection.
- Start at login support for Windows, macOS LaunchAgents, and Linux autostart.
- Single instance enforcement across all supported operating systems.
- Automated packaging scripts for Windows (zip), macOS (.app), and Linux (tar.gz).
- Localized default prompt templates for English, Italian, Spanish, French, German, and Hindi across all rewriting actions.
- Hindi style and explanation level translations in the prompt catalog.
