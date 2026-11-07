# Changelog

Notable changes to Switch Toolbox 2 are listed here. Dates use ISO format.

## V2.2.0 - 2026-11-07

### Added

- Add a heart-image toolbar button that opens Timexo's donation page.
- Allow files and folders to be dropped onto the main window or MDI workspace; folders open in the existing recursive file explorer.
- Document AI assistance in the README.

### Notes

- The existing Open Folder command remains available alongside drag-and-drop folder opening.
- The bundled MKW music-editor program is a Windows binary without its source project. Its feature set has not been ported into Switch Toolbox 2.
- This application targets Windows Forms on .NET Framework 4.8. This release does not provide a native Linux build.

## V2.1.3 - 2026-11-06

### Fixed

- Preserve the final character of edited MSBT messages in game by writing an encoding-correct null terminator after edited TXT2 strings. Trailing NUL padding is hidden in the editor and normalized when a string is edited.

### Verification notes

- MSBT TXT2 messages are null-terminated. Edited strings now receive an encoding-correct terminator; an in-game test is still needed to confirm behavior in each target title.

## V2.1.2 - 2026-11-06

### Added

- Register `.msarc` with the existing SARC archive reader/writer so MSARC archives can use the supported SARC/MSBT editing and repacking paths.
- Preserve the compound inner-format/compression suffix when saving `.zs` files (for example, `asset.bntx.zs`) so the selected BNTX filter no longer changes the extension order.
- Apply the replacement Save, Update, and AAMP icons supplied for the project.

### Verification notes

- MSARC uses the existing SARC serializer and MSBT handler. Windows application-level editing and repacking tests are required for end-to-end verification.
- BWAV playback differences in game have not been confirmed as fixed; verify playback with the intended game and hardware before treating that report as resolved.

## V1.1.2 - 2026-11-04

### Fixed

- Rebuild loaded audio streams when the Windows output device changes, and preserve playback position when switching between the stereo mix and individual channel previews.
- Open the Loop Editor for BWAV files without relying on a VGAudio configuration object that BWAV does not provide. Normalize full-track loop-end sentinels for editing.
- Preview stereo BWAV files as an interleaved stereo signal rather than treating channel 0 as the entire mix.

### Added

- Add BWAV export settings for sample rate, mono/stereo output, and loop points. Export resamples PCM when the selected sample rate differs and writes the requested channel mapping and loop metadata.
- Initialize the settings editor from the current BWAV metadata.

### Format notes

- BWAV stores DSP-ADPCM audio, sample rate, channel pan, and loop data. PCM16 is not a BWAV codec, and playback backends such as OpenAL or cubeb are not serialized in the file. The settings dialog explains these limitations rather than writing unsupported fields.
- BWAV loop metadata uses a full-track sentinel in some files; the editor displays that sentinel as the end of the track.
- A full Nintendo Switch 2 game playback test is not available in this build environment; game-specific pitch/double-playback behavior remains to be verified in-game.

## V1.1.1 - 2026-11-04

### Fixed

- Pad encoded DSP-ADPCM channel data to the complete 8-byte frame length required by the BWAV sample count. This fixes BWAV exports whose final channel ended one byte before the declared frame boundary.

### Changed

- Replace the Save, Update, and AAMP UI icons with the designs supplied for this update.
- Set the application assembly and packaged version metadata to `1.1.1.0` / `1.1.1`, above the released v1.1.0.

### Verification

- The supplied failing BWAV declares 7,618,560 samples per channel, for which the decoder requires 4,353,464 encoded bytes. Its final channel contained 4,353,463 bytes; the encoder now pads the shortfall to the required 8-byte frame boundary.
- Windows compilation and BWAV round-trip testing are pending for this update.

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
