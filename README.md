# Rewordly

[![CI](https://github.com/rewordly/rewordly/actions/workflows/ci.yml/badge.svg)](https://github.com/rewordly/rewordly/actions/workflows/ci.yml)
[![Release](https://github.com/rewordly/rewordly/actions/workflows/release.yml/badge.svg)](https://github.com/rewordly/rewordly/actions/workflows/release.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![.NET 8.0](https://img.shields.io/badge/.NET-8.0-purple.svg)](https://dotnet.microsoft.com/)
[![Platform](https://img.shields.io/badge/Platform-Windows%20%7C%20macOS%20%7C%20Linux-lightgrey.svg)]()

Rewordly is an independent, cross-platform desktop application for AI text rewriting. Rewordly integrates with all text applications on Windows, macOS, and Linux.

The application uses C# with .NET 8, [Avalonia UI](https://avaloniaui.net/) for user interface controls, and [SharpHook](https://github.com/TolikPylypchuk/SharpHook) for system-wide keyboard event handling. Rewordly is an independent codebase and does not require third-party runtime dependencies on client machines.

## Architecture and Core Features

- **Context Menu at Cursor:** Trigger the menu at the mouse pointer location after you press the copy shortcut twice.
- **Text Styles:** Apply 12 core styles (Formal, Informal, Technical, Academic, and others) or custom user styles.
- **AI Agent Prompts:** Rewrite a rough request into a well-structured prompt for a general agent, a coding agent, a research agent, a system prompt, or a chat assistant, plus an **Image AI** section for image generation and image editing prompts. Templates live in [`prompts/`](prompts/README.md).
- **Proofreading:** Select **Correct and improve** or **Fix errors only**.
- **Summarization:** Generate summaries at 70%, 50%, or 30% length budgets.
- **Explanation:** Generate explanations tailored to four knowledge levels (None, Basic, Intermediate, Advanced).
- **Translation:** Translate text into 27 supported languages or any custom language.
- **Interactive Result Window:** Edit the output text, display the original input, or replace text directly into the source window.
- **Interactive Chat:** Provide custom instructions and maintain multi-turn prompt history.
- **Read Aloud (Listen):** Read text aloud using local operating system speech synthesizers.
- **Multiple AI Providers:** Connect to Google Gemini, OpenAI, OpenRouter, Groq, local Ollama instances, or any OpenAI-compatible server.
- **Encrypted Credential Storage:** Store API keys in Windows DPAPI, macOS Keychain, or Linux Secret Service (freedesktop secret service API).
- **Zero Cloud Intermediaries:** Transmit all requests directly from your machine to your chosen AI endpoint.

## Downloads

| Operating System | Architecture | Distribution Package | Execution Binary |
| --- | --- | --- | --- |
| Windows 10/11 | x64 (Intel / AMD) | `Rewordly-2.0.0-win-x64.zip` | `Rewordly.exe` (Self-contained) |
| Windows 11 | ARM64 | `Rewordly-2.0.0-win-arm64.zip` | `Rewordly.exe` (Self-contained) |
| macOS 11+ | ARM64 (Apple Silicon M1–M4) | `Rewordly-2.0.0-osx-arm64.zip` | `Rewordly.app` (Ad-hoc signed) |
| macOS 11+ | x64 (Intel) | `Rewordly-2.0.0-osx-x64.zip` | `Rewordly.app` (Ad-hoc signed) |
| Linux (X11) | x64 | `Rewordly-2.0.0-linux-x64.tar.gz` | `Rewordly` (Single executable) |
| Linux (X11) | ARM64 | `Rewordly-2.0.0-linux-arm64.tar.gz` | `Rewordly` (Single executable) |

## Keyboard Shortcuts

| Action | Windows | macOS | Linux (X11) |
| --- | --- | --- | --- |
| Primary trigger | `Ctrl+C` twice (hold `Ctrl`) | `Cmd+C` twice (hold `Cmd`) | `Ctrl+C` twice (hold `Ctrl`) |
| Alternative trigger (keeps clipboard unchanged) | `Ctrl+Insert` | `Ctrl+Cmd+C` | `Ctrl+Alt+C` (requires `xclip` or `xsel`) |
| Paste and replace result | `Ctrl+Enter` | `Cmd+Enter` | `Ctrl+Enter` |
| Regenerate result | `Ctrl+R` | `Cmd+R` | `Ctrl+R` |

> [!NOTE]
> You can configure the trigger interval in Settings. The default interval is 800 milliseconds.

## Installation Procedures

### Windows Installation

#### Purpose
Install and execute Rewordly on Windows 10 or Windows 11.

#### Prerequisites
- Windows 10 64-bit (build 19041 or higher) or Windows 11.

#### Procedure
1. Extract `Rewordly-2.0.0-win-x64.zip` (or the ARM64 archive) to an installation directory.
2. Execute `Rewordly.exe`.
3. If Windows SmartScreen displays a warning, select **More info**, then select **Run anyway**.
4. Configure your preferred AI provider in the Settings window.

#### Verification
Execute `Rewordly.exe`. Verify that the Rewordly icon appears in the Windows system tray.

> [!TIP]
> If you need to paste text into applications that run as Administrator, run `Rewordly.exe` as Administrator.

---

### macOS Installation

#### Purpose
Install and configure Rewordly on macOS.

#### Prerequisites
- macOS 11 (Big Sur) or newer.

#### Procedure
1. Extract `Rewordly-2.0.0-osx-arm64.zip` (or Intel `osx-x64.zip`).
2. Move `Rewordly.app` to your `/Applications` directory.
3. If macOS blocks initial launch, remove the quarantine attribute in Terminal:
   ```bash
   xattr -dr com.apple.quarantine /Applications/Rewordly.app
   ```
4. Open **System Settings > Privacy & Security > Accessibility**.
5. Enable permissions for **Rewordly**.
6. Open **System Settings > Privacy & Security > Input Monitoring** if requested.
7. Enable permissions for **Rewordly**.
8. Launch `Rewordly.app` from the Applications folder.

#### Expected Result
Rewordly displays its icon in the macOS menu bar.

---

### Linux Installation

#### Purpose
Install and configure Rewordly on Linux desktop environments.

#### Prerequisites
- X11 desktop session (KDE, XFCE, GNOME on Xorg, Cinnamon, MATE).
- System utility packages (`xdotool`, `xclip`, `espeak-ng`, `libsecret-tools`).

#### Procedure
1. Install system prerequisites on Debian-based distributions:
   ```bash
   sudo apt update
   sudo apt install -y xdotool xclip espeak-ng libsecret-tools
   ```
2. Extract the archive:
   ```bash
   tar -xzf Rewordly-2.0.0-linux-x64.tar.gz
   cd Rewordly-2.0.0-linux-x64
   ```
3. Run the installation script:
   ```bash
   ./install-linux.sh
   ```
4. Start the application:
   ```bash
   Rewordly &
   ```

#### Verification
Open a text editor. Select text and press `Ctrl+C` twice while holding `Ctrl`. Verify that the Rewordly context menu appears.

> [!IMPORTANT]
> System-wide global key listening requires an X11 session. On Wayland sessions, global shortcuts operate only within X11 / XWayland client windows. For full shortcut capability, select an X11 desktop session at login.

---

## AI Provider Configuration

You can configure an API key in the application Settings window or set environment variables:

| Provider | Default Endpoint | Supported Environment Variables |
| --- | --- | --- |
| Google Gemini | `https://generativelanguage.googleapis.com/v1beta` | `GEMINI_API_KEY`, `GOOGLE_API_KEY` |
| OpenAI | `https://api.openai.com/v1` | `OPENAI_API_KEY` |
| OpenRouter | `https://openrouter.ai/api/v1` | `OPENROUTER_API_KEY` |
| Groq | `https://api.groq.com/openai/v1` | `GROQ_API_KEY` |
| Ollama (Local) | `http://localhost:11434/v1` | None required |
| Custom Server | User-defined | User-defined |

### Setup Steps for Local Offline Operation (Ollama)

1. Download and install Ollama from [ollama.com](https://ollama.com).
2. Pull a target model:
   ```bash
   ollama pull llama3.2
   ```
3. Open Rewordly Settings.
4. Select **Ollama (local, offline)** in the Provider list.
5. Select **Refresh list**.
6. Select `llama3.2` from the Model dropdown list.
7. Select **Save**.

---

## Configuration and Storage Locations

| Item | Windows | macOS | Linux |
| --- | --- | --- | --- |
| Configuration file | `%APPDATA%\Rewordly\settings.json` | `~/Library/Application Support/Rewordly/settings.json` | `~/.config/rewordly/settings.json` |
| Encrypted credentials | Windows DPAPI | macOS Keychain (`security`) | Linux Secret Service or local AES-256-GCM file |
| Autostart entry | Registry (`HKCU\...\Run`) | `~/Library/LaunchAgents/com.rewordly.app.plist` | `~/.config/autostart/rewordly.desktop` |
| Speech synthesizer | Windows SAPI voices | macOS `say` utility | `espeak-ng`, `espeak`, or `spd-say` |

> [!NOTE]
> Set the `REWORDLY_CONFIG_DIR` environment variable to direct configuration storage to a custom folder path.

---

## Build from Source

### Prerequisites
- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) or later.
- Git client.

### Procedure (Bash / Linux / macOS)

1. Clone the repository:
   ```bash
   git clone https://github.com/rewordly/rewordly.git
   cd rewordly
   ```
2. Run automated tests:
   ```bash
   dotnet test
   ```
3. Run the desktop application:
   ```bash
   dotnet run --project src/Rewordly.Desktop
   ```
4. Build self-contained release packages:
   ```bash
   ./packaging/publish.sh
   ./packaging/package.sh 2.0.0
   ```

### Procedure (PowerShell / Windows)

#### Purpose
Compile and package the Rewordly single-file executable for Windows.

#### Prerequisites
- Windows 10 (64-bit) or Windows 11.
- .NET 8.0 SDK or later.

#### Procedure
1. Open PowerShell.
2. Navigate to the project root directory.
3. Configure the .NET environment paths if you installed .NET in a local directory:
   ```powershell
   $env:DOTNET_ROOT = "$env:LOCALAPPDATA\Microsoft\dotnet"
   $env:PATH = "$env:DOTNET_ROOT;$env:PATH"
   ```
4. Run automated tests:
   ```powershell
   dotnet test
   ```
   **Expected result:** All unit tests pass with zero failures.
5. Compile the debug build to verify the source code:
   ```powershell
   dotnet build
   ```
   **Expected result:** The build succeeds with zero errors.
6. Stop any active instance of Rewordly before publishing to avoid file lock errors:
   ```powershell
   Stop-Process -Name Rewordly -Force -ErrorAction SilentlyContinue
   ```
7. Publish the self-contained single-file executable for Windows x64:
   ```powershell
   .\packaging\publish.ps1 -Targets @('win-x64')
   ```
   *Alternative CLI command without the helper script:*
   ```powershell
   dotnet publish src/Rewordly.Desktop/Rewordly.Desktop.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -p:DebugType=none -p:EnableWindowsTargeting=true -o pub/win-x64
   ```
   **Expected result:** The single-file binary `pub\win-x64\Rewordly.exe` is generated.
8. Optional: Publish the executable for Windows ARM64:
   ```powershell
   .\packaging\publish.ps1 -Targets @('win-arm64')
   ```
9. Package release zip archives and compute SHA-256 checksums:
   ```powershell
   .\packaging\package.ps1 -Version "2.0.0"
   ```
   **Expected result:** The archive `dist\Rewordly-2.0.0-win-x64.zip` and the checksum file `dist\SHA256SUMS.txt` are generated.

#### Windows Binary Details
- **Binary path:** `pub\win-x64\Rewordly.exe` (or `pub\win-arm64\Rewordly.exe`)
- **Package type:** Self-contained single-file executable with embedded runtime and compressed dependencies.
- **Client runtime requirements:** None. The application runs on systems without .NET installed.
- **Size:** Approximately 46 MB.
- **Execution:** Double-click `Rewordly.exe` or execute `.\pub\win-x64\Rewordly.exe` from PowerShell or Command Prompt.

---

## Project Structure

```
├── .github/
│   ├── ISSUE_TEMPLATE/       # Bug report and feature request templates
│   ├── workflows/            # GitHub Actions CI and Release workflows
│   └── pull_request_template.md
├── packaging/
│   ├── Info.plist            # macOS bundle configuration
│   ├── install-linux.sh      # Linux user installation script
│   ├── package.ps1           # Windows PowerShell packaging script
│   ├── package.sh            # Linux/macOS bash packaging script
│   ├── publish.ps1           # Windows PowerShell publish script
│   ├── publish.sh            # Linux/macOS bash publish script
│   └── rewordly.desktop      # Linux XDG desktop entry
├── prompts/                  # Contributable prompt templates (AI agent prompt variants)
├── src/
│   ├── Rewordly.Core/        # Logic library: settings, AI clients, prompts, text utilities
│   │   ├── Providers/        # Gemini, OpenAI, OpenRouter, Groq, Ollama adapters
│   │   ├── AppSettings.cs    # Configuration model and persistence
│   │   ├── Catalog.cs        # Language, style, and explanation definitions
│   │   ├── Prompts.cs        # AI prompt templates
│   │   └── TextUtil.cs       # Text manipulation and word counting
│   └── Rewordly.Desktop/     # Avalonia UI application
│       ├── Assets/           # Application icons (.ico, .icns, .png)
│       ├── Platform/         # OS-specific hooks: input, focus, secrets, speech, autostart
│       ├── Views/            # Menus, preview window, chat window, settings window
│       └── AppController.cs  # Application workflow and lifecycle controller
├── tests/
│   └── Rewordly.Tests/       # Unit tests for core logic and HTTP mocks
├── CHANGELOG.md              # Version history
├── CONTRIBUTING.md           # Contribution guidelines
├── LICENSE                   # MIT License
├── README.md                 # Project documentation
├── Rewordly.sln              # Visual Studio solution file
└── SECURITY.md               # Security reporting guidelines
```

---

## Troubleshooting

### Windows: Hotkey Does Not Trigger In Administrative Windows
- **Cause:** Windows User Account Control blocks unprivileged applications from intercepting keystrokes in elevated windows.
- **Solution:** Right-click `Rewordly.exe` and select **Run as administrator**.

### Windows: File Lock Error During Publish (IOException: The process cannot access the file)
- **Cause:** An active background instance of `Rewordly.exe` locks the executable file in `pub\win-x64\`.
- **Solution:** Stop the active process before executing the publish command:
  ```powershell
  Stop-Process -Name Rewordly -Force -ErrorAction SilentlyContinue
  ```

### macOS: Double-Copy Does Not Open Menu
- **Cause:** Missing Accessibility or Input Monitoring permissions.
- **Solution:** Open **System Settings > Privacy & Security > Accessibility**, remove Rewordly if present, add `Rewordly.app`, and ensure the toggle switch is ON.

### Linux: Shortcuts Do Not Fire Under Wayland
- **Cause:** The Wayland display protocol restricts global keyboard hooks for security isolation.
- **Solution:** Switch to an X11 (Xorg) session at your system display manager login screen.

---

## License

This project is licensed under the [MIT License](LICENSE).
