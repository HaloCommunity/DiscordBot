# GitHub Copilot Instructions for HaloCommunityBot

Essential guidelines for AI code generation on this project.

## CRITICAL: Version Management

Never manually edit `src/HaloCommunityBot/HaloCommunityBot.csproj` version or `CHANGELOG.md` version entries.

Always use the VersionManager tool. Build it before use:

```bash
# Step 1: Build VersionManager as Release
dotnet build tools/VersionManager/VersionManager.csproj -c Release

# Step 2 (optional): Check if version bump is needed based on git commits
dotnet artifacts/bin/VersionManager/release/VersionManager.dll check-commits

# Step 3: Bump version in both csproj and changelog
dotnet artifacts/bin/VersionManager/release/VersionManager.dll bump --version X.Y.Z --type patch --message "Your description"

# Step 4: Validate consistency
dotnet artifacts/bin/VersionManager/release/VersionManager.dll validate

# Step 5: Build main project
dotnet build
```

Build validation enforces version consistency. If `HaloCommunityBot.csproj` and `CHANGELOG.md` versions differ, build fails.

## Commit Message Format

Use Conventional Commits:

```text
type(scope): description
```

Types: `feat`, `fix`, `refactor`, `chore`, `docs`

Example: `feat(status): add richer /about command details`

## Build Verification

After any code changes:

```bash
dotnet build
```

## Logging

Use structured logging:

```csharp
_logger.LogInformation("Operation started for {UserId}", userId);
_logger.LogError(ex, "Failed request to {Url}", apiUrl);
```

Critical startup phases should log entry and exit.

## Quick Checklist Before Commit

* \[ ] Code compiles with `dotnet build`
* \[ ] Version bumped with VersionManager if shipping a release
* \[ ] `CHANGELOG.md` updated via VersionManager
* \[ ] Conventional Commit message used
* \[ ] No manual version edits
