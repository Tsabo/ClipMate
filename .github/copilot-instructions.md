# ClipMate Development Guidelines

Auto-generated from all feature plans. Last updated: 2025-11-11

## Active Technologies

- C# 14+ with .NET 10.0 SDK, nullable reference types enabled (001-clipboard-manager)

## Project Structure

```text
Source/
   src/
   tests/
```

## Commands

# Add commands for C# 14+ with .NET 10.0 SDK, nullable reference types enabled

## Code Style

C# 14+ with .NET 10.0 SDK, nullable reference types enabled: Follow standard conventions

## Implementation Policy

**Before implementing any new component or functionality:**

1. **Research Microsoft's Built-in Solutions First**
   - Check if .NET Framework/SDK provides the functionality out of the box
   - Prefer standard Microsoft libraries and patterns (e.g., `Microsoft.Extensions.*`)
   - Do NOT re-implement functionality that Microsoft already provides

2. **Evaluate Third-Party Solutions**
   - If Microsoft doesn't provide a solution, research popular open-source NuGet packages
   - Consider: maturity, maintenance status, download count, community support
   - Document findings and present options

3. **Require Approval for Third-Party Dependencies**
   - Present researched options with pros/cons
   - Wait for explicit approval before adding third-party packages
   - Document the decision rationale

4. **Custom Implementation as Last Resort**
   - Only build custom solutions when no suitable alternative exists
   - Justify why existing solutions are insufficient
   - Ensure custom code is well-documented and tested

**Examples:**
- ✅ Use `Microsoft.Extensions.Logging` instead of custom logger interfaces
- ✅ Use `Microsoft.Extensions.DependencyInjection` for IoC
- ✅ Research Serilog/NLog before building custom file logger
- ❌ Don't create custom implementations of standard .NET patterns

## Recent Changes

- 001-clipboard-manager: Added C# 14+ with .NET 10.0 SDK, nullable reference types enabled

<!-- MANUAL ADDITIONS START -->
<!-- MANUAL ADDITIONS END -->
