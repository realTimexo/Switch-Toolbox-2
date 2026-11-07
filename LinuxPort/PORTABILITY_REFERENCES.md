# Cross-platform porting references

These sources inform the development-only Avalonia/.NET 10 client. They do not establish that Switch Toolbox 2's format editors are already Linux-compatible.

- Microsoft, [Windows Forms Overview](https://learn.microsoft.com/en-us/dotnet/desktop/winforms/overview/): Windows Forms is a UI framework for Windows desktop apps; the .NET Framework 4 implementation is Windows-only. The existing app targets .NET Framework 4.8 and cannot become a native Linux GUI by changing packaging alone.
- Avalonia, [Supported platforms](https://docs.avaloniaui.net/docs/supported-platforms): Avalonia supports desktop Windows and Linux, with desktop targets on .NET 8 or later. Linux support includes X11; the documentation lists X11/fontconfig native packages, glibc requirements, and notes that support tiers differ by distribution. A native Wayland backend is documented as an opt-in starting with Avalonia 12.1.0.
- Microsoft, [.NET 10 downloads](https://dotnet.microsoft.com/en-us/download/dotnet/10.0): official .NET 10 SDK/runtime downloads are available for Linux x64; the new client targets .NET 10.
- AppImage, [Packaging Guide](https://docs.appimage.org/packaging-guide/index.html): AppImages are built from an AppDir and need their application entry point, desktop metadata, icon, and dependency/runtime packaging verified. AppImage output must be tested separately from merely compiling the app.

## Current target and known limitations

The separate `LinuxPort/` client targets `net10.0` and Avalonia 12.1.5. Its first milestone is native file/folder browsing only. It does not yet reuse the WinForms format-editor libraries, whose UI and native dependencies need staged extraction and platform-specific replacements. The existing Windows application remains the supported full-featured client while porting proceeds.
