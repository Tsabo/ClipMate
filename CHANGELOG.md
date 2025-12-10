# Changelog

All notable changes to ClipMate will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added
- QuickPaste functionality with auto-targeting
- Three-pane Explorer interface
- Clipboard monitoring and capture
- Search and filtering capabilities
- Multiple formatting string support
- Good/Bad target lists for QuickPaste
- Monaco code editor integration
- Sound effects for actions
- DevExpress modern UI controls

### Infrastructure
- Cake build system
- Dual installer strategy (standard + portable)
- GitHub Actions CI/CD pipeline
- MinVer automatic versioning
- WebView2 Fixed Version bundling (portable)
- SignPath integration (pending approval)

## [0.1.0] - TBD

### Added
- Initial pre-alpha release
- Core clipboard management features
- Basic QuickPaste implementation
- Database schema with SQLite
- Settings and configuration system

### Known Issues
- Installers are unsigned (awaiting SignPath approval)
- QuickPaste monitoring thread not yet implemented
- Window stack targeting uses simple foreground window detection
- GoBack functionality not yet implemented

---

## Release Notes Format

Each release should include:

### Added
New features and capabilities

### Changed
Changes to existing functionality

### Fixed
Bug fixes

### Deprecated
Features that will be removed in upcoming releases

### Removed
Features that have been removed

### Security
Security-related changes

---

## Version History

| Version | Date | Type | Notes |
|---------|------|------|-------|
| 0.1.0 | TBD | Pre-alpha | Initial development release |

---

**Note:** This project is in active development. Breaking changes may occur between pre-release versions.
