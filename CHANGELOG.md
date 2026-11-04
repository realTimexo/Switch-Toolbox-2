# Changelog

Notable changes to Switch Toolbox 2 are listed here. Dates use ISO format.

## V1.1.0 - 2026-11-04

### Added

- Add **Export BWAV...** to the audio editor's right-click menu for BWAV files.
- Add GitHub issue forms for bug reports and feature requests.

### Changed

- Package Windows builds with the application files directly at the ZIP root and reject nested ZIP packages.
- Update the application icon and title-bar icon to the Switch Toolbox 2 logo.
- Set the application assembly and packaged version metadata to `1.0.1.0`.
- Show release name, publication date, and release notes more clearly in the update dialog.
- Catch file-opening exceptions and report them without terminating the application.

### Verification and limitations

- The Windows CI build completed successfully, and the generated ZIP was checked for archive integrity, a root-level `Toolbox.exe`, and no nested ZIP files.
- The BFRES `ShaderInfo.SetupOptionBooleans` parser failure remains a format-compatibility limitation; the file-opening change prevents the parser exception from closing the whole application but does not fix the parser.

## V1.0.1 - 2026-11-03

- Bundle and preload architecture-matched VC runtimes required by native Tegra texture swizzling.
- Add **Save As** to the archive right-click menu and keep it separate from the existing Save action.
- Check the latest stable release in the Switch Toolbox 2 repository and install its versioned ZIP asset.
- Show release notes in the update prompt, simplify the titlebar, update the in-app tutorial link, and reduce the README logo size.
- Fixed high pitch bwav sound playback
- Compeleted _Combined.btnx file support for Switch 2

## V1.0.0 — 2026-11-03

### Added

- Added a BWAV format handler with DSP-ADPCM decoding for playback and BWAV encoding for saving.
- Added **Import WAV...** to the audio editor's right-click menu for BWAV audio.
- Added a Windows GitHub Actions build that compiles the Release configuration and packages the application as a ZIP.
- Added a manually triggered Windows release workflow. It accepts a version name and release notes, then creates a GitHub Release with a versioned `SwitchToolbox2-<version>.zip` asset.
- Restored the Switch Toolbox 2 logo in the repository root, application resources, executable icons, and README.

### Changed

- Rebranded the application as **Switch Toolbox 2**, with **Timexo** as maintainer.
- Updated the in-app GitHub and issue links to the Switch Toolbox 2 repository.
- Refreshed the README and retained credit to the original [Switch Toolbox](https://github.com/KillzXGaming/Switch-Toolbox) project and its contributors.
- Corrected audio **Save As** extension matching so the selected target format is used.
- Restored the missing upstream UV editor source file and removed its stale ignore rule so the Windows solution builds.

### Verification

- The Windows Release build completed successfully in GitHub Actions, and the generated ZIP passed an archive integrity check.
- Build verification confirms compilation and packaging; it does not confirm successful loading of every game asset or a BWAV round-trip test.

### Known issues and limitations

- Some native texture-decoding dependencies may not be present on a clean Windows installation; later builds bundle the matching VC runtime.
- BWAV uses lossy DSP-ADPCM encoding, so imported WAV audio can sound different from the source depending on the material and encoder settings.
- Compatibility with Nintendo Switch 2 game assets is not comprehensive or guaranteed; support depends on each format parser and should be validated per file.

### License

- This release continues under the repository's GNU General Public License v3.0. Third-party components retain their respective notices and licenses.
