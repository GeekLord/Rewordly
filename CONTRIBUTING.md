# Contributing to Rewordly

## Purpose

This document explains how to contribute to the Rewordly project. Rewordly is developed and maintained by [Shobhit Kumar Prabhakar](https://shobhit.net); pull requests and issues are welcome on [GitHub](https://github.com/GeekLord/Rewordly).

## Prerequisites

- .NET 8.0 SDK or later
- Git command-line client
- Code editor (Visual Studio, JetBrains Rider, or Visual Studio Code with C# Dev Kit)

## Recommended Tools per Operating System

- **Windows:** Visual Studio 2022 or .NET CLI.
- **macOS:** Xcode command-line tools (`xcode-select --install`) and .NET CLI.
- **Linux:** .NET CLI, `libsecret-tools`, `xdotool`, `xclip`, and `espeak-ng`.

## Code of Conduct

Maintain professional communication. Respect other contributors and follow courteous communication standards.

## Procedure to Set Up the Development Environment

1. Clone the repository to your local computer:
   ```bash
   git clone https://github.com/GeekLord/Rewordly.git
   cd Rewordly
   ```

2. Restore NuGet dependencies:
   ```bash
   dotnet restore Rewordly.sln
   ```
   **Expected result:** NuGet packages download and restore without errors.

3. Build the solution in Debug configuration:
   ```bash
   dotnet build Rewordly.sln
   ```
   **Expected result:** The build finishes with 0 errors.

4. Run the automated test suite:
   ```bash
   dotnet test tests/Rewordly.Tests/Rewordly.Tests.csproj
   ```
   **Expected result:** All test assertions pass.

5. Run the application locally:
   ```bash
   dotnet run --project src/Rewordly.Desktop
   ```
   **Expected result:** The Rewordly application starts and shows the tray icon.

## Code Conventions

- Keep `src/Rewordly.Core` independent of UI libraries.
- Put all OS-specific API logic in `src/Rewordly.Desktop/Platform`.
- Keep public interfaces clean and documented with XML documentation comments.
- Do not commit secrets, API keys, or machine-specific paths.
- Write unit tests for new business logic in `tests/Rewordly.Tests`.

## Contributing Prompt Templates

The templates behind **Rewrite as AI agent prompt** are plain text files in [`prompts/agent-prompt/`](prompts/agent-prompt/). You can add or improve a variant without writing C#. See [`prompts/README.md`](prompts/README.md) for the file format and review checklist.

## Procedure to Submit Changes

1. Create a descriptive feature branch from `main`:
   ```bash
   git checkout -b feature/my-new-feature
   ```

2. Make your code modifications.

3. Execute the unit test suite:
   ```bash
   dotnet test
   ```

4. Commit your changes with clear, imperative commit messages:
   ```bash
   git commit -m "Add custom prompt template validation"
   ```

5. Push the branch to your GitHub fork:
   ```bash
   git push origin feature/my-new-feature
   ```

6. Open a Pull Request against the `main` branch on GitHub.

7. Complete the pull request template checklist.
