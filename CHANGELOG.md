# Changelog

Notable changes to Switch Toolbox 2 are listed here. Dates use ISO format.

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

- The supplied `TestFile2Model.zs` still encounters the BFRES parser exception in `MaterialParserV10.ShaderInfo.SetupOptionBooleans`.
- The reported BNTX texture-loading hang for `TitleLogoL_00^w` from `TestFile1.zs` remains unresolved.
- BWAV playback and conversion code compiles, but WAV↔BWAV output has not yet been round-trip tested against representative files.
- Compatibility with Nintendo Switch 2 game assets is not comprehensive or guaranteed; support depends on each format parser and should be validated per file.

### License

- This release continues under the repository's GNU General Public License v3.0. Third-party components retain their respective notices and licenses.
