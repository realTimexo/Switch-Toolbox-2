# Switch Toolbox 2 cross-platform client (work in progress)

This folder is the first isolated native-client porting milestone. It targets .NET 10 and Avalonia so the new client can be built for Linux x86_64 and Windows without changing the existing WinForms/.NET Framework 4.8 application.

## Current scope

- Cross-platform desktop application shell.
- Native file and folder picker dialogs.
- Lazy folder-tree browsing for files and nested folders.
- The selected file path is displayed, but no Nintendo format parser/editor is connected yet.

This is **not a release build** and does not yet replace the Windows application. Do not publish it as a complete Switch Toolbox 2 port. Workflows such as archive/MSBT editing, texture/model rendering, audio playback, batch export, and the MKW Music Editor integration still need separate ports and tests. The original Windows project remains unchanged in this phase.

## Build locally

Install the .NET 10 SDK, then run:

```sh
dotnet restore LinuxPort/SwitchToolbox.CrossPlatform.csproj
dotnet build LinuxPort/SwitchToolbox.CrossPlatform.csproj -c Release
```

Linux package output (AppImage and tar.gz) will be added only after the application connects the relevant file-format services and passes Linux CI/smoke tests.
