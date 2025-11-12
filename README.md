# ClipMate

A modern Windows desktop clipboard manager built with .NET 10 and WPF, featuring intelligent text/image storage, search capabilities, and customizable templates.

## Features

- **Clipboard History**: Automatically capture and store text and image clipboard entries
- **Smart Search**: Full-text search across clipboard history with highlighting
- **Collections**: Organize clipboard items into custom collections
- **Templates**: Create reusable text templates with variable substitution
- **Global Hotkeys**: Quick access via customizable keyboard shortcuts
- **Sound Feedback**: Optional audio cues for clipboard operations
- **Content Filters**: Exclude sensitive applications from clipboard capture
- **Modern UI**: Clean WPF interface with MVVM architecture

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) (required)
- Windows 10/11 (required for Win32 clipboard APIs)
- Visual Studio 2025+ or VS Code with C# Dev Kit (recommended)

## Architecture

ClipMate follows a clean, modular architecture with separation of concerns:

```
Source/
├── src/
│   ├── ClipMate.App/              # WPF application (UI layer)
│   ├── ClipMate.Core/             # Business logic & domain models
│   ├── ClipMate.Data/             # LiteDB data access layer
│   └── ClipMate.Platform/         # Windows-specific Win32 interop
└── tests/
    ├── ClipMate.Tests.Unit/       # Unit tests (90%+ coverage)
    └── ClipMate.Tests.Integration/ # Integration tests
```

### Technology Stack

- **UI Framework**: WPF (Windows Presentation Foundation)
- **MVVM Toolkit**: CommunityToolkit.Mvvm 8.3.2 with source generators
- **Database**: Entity Framework Core 9.0.0 + SQLite
- **Audio**: NAudio 2.2.1 for sound playback (pending implementation)
- **DI Container**: Microsoft.Extensions.DependencyInjection 9.0.0
- **Logging**: Microsoft.Extensions.Logging 9.0.0 (Console + Debug providers)
- **Testing**: xUnit 2.9.2, Moq 4.20.72, FluentAssertions 6.12.1

### Package Management

This project uses **Central Package Management** (CPM) via `Directory.Packages.props`. All package versions are centrally defined at the repository root, ensuring consistent versioning across all projects.

## Getting Started

### Clone the Repository

```powershell
git clone https://github.com/yourusername/ClipMate.git
cd ClipMate
```

### Build the Solution

```powershell
cd Source
dotnet restore
dotnet build
```

### Run the Application

```powershell
cd src/ClipMate.App
dotnet run
```

### Run Tests

Run all tests:
```powershell
cd Source
dotnet test
```

Run unit tests only:
```powershell
dotnet test tests/ClipMate.Tests.Unit
```

Run integration tests only:
```powershell
dotnet test tests/ClipMate.Tests.Integration
```

Run tests with coverage:
```powershell
dotnet test --collect:"XPlat Code Coverage"
```

## Development

### Code Style

This project uses `.editorconfig` to enforce consistent code formatting. Key conventions:

- **Nullable Reference Types**: Enabled project-wide (C# 14)
- **Naming**: Interfaces start with `I`, private fields start with `_`
- **Indentation**: 4 spaces for C#, 2 spaces for XML/JSON
- **Braces**: Always use braces for control statements (Allman style)

### Testing Approach

ClipMate follows **Test-Driven Development (TDD)** with a minimum **90% code coverage** requirement:

1. Write failing test first (Red)
2. Write minimal code to pass test (Green)
3. Refactor while maintaining green tests (Refactor)

Use xUnit for test framework, Moq for mocking, and FluentAssertions for readable assertions.

### Project References

```
ClipMate.App
├── ClipMate.Core (business logic)
├── ClipMate.Data (database access)
└── ClipMate.Platform (Win32 interop)

ClipMate.Data
└── ClipMate.Core

ClipMate.Platform
└── ClipMate.Core

Tests (Unit & Integration)
├── ClipMate.Core
├── ClipMate.Data
└── ClipMate.Platform
```

## Solution Structure

The project uses the modern `.slnx` solution format (JSON-based). You can open `Source/ClipMate.sln` in Visual Studio 2025+ or use the CLI:

```powershell
dotnet sln Source/ClipMate.sln list
```

## Database

ClipMate uses **LiteDB**, a serverless NoSQL database stored as a single file:

- **Location**: `%LOCALAPPDATA%\ClipMate\clipmate.db`
- **Schema**: Document-based with BSON serialization
- **Migrations**: Handled automatically by repository layer

## Contributing

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/amazing-feature`)
3. Write tests for your changes (TDD approach)
4. Ensure all tests pass and coverage meets 90% threshold
5. Commit your changes (`git commit -m 'Add amazing feature'`)
6. Push to the branch (`git push origin feature/amazing-feature`)
7. Open a Pull Request

### Commit Message Format

Follow [Conventional Commits](https://www.conventionalcommits.org/):

```
<type>(<scope>): <subject>

[optional body]

[optional footer]
```

Types: `feat`, `fix`, `docs`, `style`, `refactor`, `test`, `chore`

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## Roadmap

- [x] Phase 1: Project Setup & Infrastructure
- [ ] Phase 2: Foundational Infrastructure (Core Models, Repositories, Services)
- [ ] Phase 3: Clipboard Capture (User Story 1)
- [ ] Phase 4: Clipboard History UI (User Story 2)
- [ ] Phase 5: Search & Filtering (User Story 3)
- [ ] Phase 6: Collections Management (User Story 4)
- [ ] Phase 7: Templates System (User Story 5)
- [ ] Phase 8: Global Hotkeys (User Story 6)
- [ ] Phase 9: Sound Feedback (User Story 7)
- [ ] Phase 10: Content Filters (User Story 8)
- [ ] Phase 11: Final Integration & Polish

## Support

For issues, questions, or feature requests, please [open an issue](https://github.com/yourusername/ClipMate/issues).

---

**Built with ❤️ using .NET 10 and WPF**
- **Script Type**: PowerShell
- **Git**: Initialized

---

Ready to begin! Start with /speckit.constitution in GitHub Copilot Chat.
