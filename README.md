# FlaUiCli

A command-line tool for automating Windows desktop applications (WPF, WinForms, Win32) using FlaUI. Designed for AI agents to interact with and test desktop UIs.

## Features

- Automate WPF, WinForms, and Win32 applications
- Background service architecture for fast, persistent connections
- Stable element IDs within sessions
- JSON output for easy parsing by AI agents
- Screenshot capture
- Support for buttons, text inputs, checkboxes, combo boxes, tree views, and more

## Installation

### Quick Install (Recommended)

Run this command in PowerShell:

```powershell
irm https://raw.githubusercontent.com/opstudio-eu/FlaUiCli/master/install.ps1 | iex
```

This will:
- Download the latest release
- Install to `%LOCALAPPDATA%\FlaUiCli`
- Add to your PATH automatically

After installation, restart your terminal and run `flaui --help` to verify.

### Install Options

```powershell
# Install specific version
& ([scriptblock]::Create((irm https://raw.githubusercontent.com/opstudio-eu/FlaUiCli/master/install.ps1))) -Version 0.0.1

# Uninstall
& ([scriptblock]::Create((irm https://raw.githubusercontent.com/opstudio-eu/FlaUiCli/master/install.ps1))) -Uninstall
```

### Manual Download

Download the latest release from [GitHub Releases](https://github.com/opstudio-eu/FlaUiCli/releases):
1. Download `flaui-X.Y.Z-win-x64.zip`
2. Extract to a folder of your choice
3. Add the folder to your PATH

### Build from Source

```bash
git clone https://github.com/opstudio-eu/FlaUiCli.git
cd FlaUiCli
dotnet build
```

Requirements: Windows 10/11, .NET 8.0 SDK

After building, the executable is at `src/FlaUiCli/bin/Debug/net8.0-windows/flaui.exe`

## Quick Start

```bash
# Start the background service
flaui service start

# List available processes
flaui process list

# Connect to your application
flaui connect --name "YourApp"

# Find an element
flaui element find --aid "SubmitButton" --first

# Click it
flaui action click <element-id>

# Take a screenshot
flaui screenshot --output screenshot.png

# Stop the service when done
flaui service stop
```

## Getting Help

Use `--help` or `-h` on any command to see available options:

```bash
flaui --help
flaui element --help
flaui action --help
```

## For AI Agents

Add this to your agent's context to enable desktop automation:

```markdown
## Desktop UI Automation (FlaUiCli)

You have access to `flaui` CLI for automating Windows desktop applications.

### Basic workflow:
1. `flaui service start` - Start the background service
2. `flaui connect --name "AppName"` - Connect to target application  
3. `flaui element find --aid "ElementId"` - Find elements by AutomationId
4. `flaui action click <id>` - Interact with elements
5. `flaui screenshot --output path.png` - Capture screenshots

### Key commands:
- `flaui --help` - List all commands
- `flaui <command> --help` - Get help for specific command

All commands output JSON with `success`, `data`, and `error` fields.
```

## Architecture

The CLI auto-starts a background service process when needed. The service handles automation and maintains persistent connections to target applications.

```
┌─────────────┐     Named Pipes     ┌─────────────────┐
│  flaui.exe  │ ◄─────────────────► │ flaui.exe       │
│    (CLI)    │                     │ --service-mode  │
└─────────────┘                     └────────┬────────┘
                                             │
                                             │ UI Automation
                                             ▼
                                    ┌─────────────────┐
                                    │  Target App     │
                                    │  (WPF/WinForms) │
                                    └─────────────────┘
```

The service automatically shuts down after 5 minutes of inactivity.

## License

MIT License - see [LICENSE](LICENSE)
