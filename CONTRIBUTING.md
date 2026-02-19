# Contributing to NoMercy MediaServer

Thank you for your interest in contributing to NoMercy MediaServer! This document outlines the process for contributing to this project.

## Branch Structure

| Branch | Purpose | CI |
|--------|---------|-----|
| `wip` | Work in progress, not ready | None |
| `dev` | Push here to release | Full pipeline |
| `main` | Releases only | Automated |

**Maintainers**: Push to `dev` when ready to release, `wip` when saving work.

**External contributors**: Submit PRs to `dev`.

## Getting Started (External Contributors)

### 1. Fork the Repository

Click the "Fork" button on the [GitHub repository](https://github.com/NoMercyTV/NoMercyMediaServer) to create your own copy.

### 2. Clone Your Fork

```bash
git clone https://github.com/YOUR-USERNAME/NoMercyMediaServer.git
cd NoMercyMediaServer
```

### 3. Set Up Upstream Remote

```bash
git remote add upstream https://github.com/NoMercyTV/NoMercyMediaServer.git
```

### 4. Create a Feature Branch

Always create a new branch for your changes:

```bash
git checkout -b feature/your-feature-name
```

Use descriptive branch names:
- `feature/add-dark-mode` - for new features
- `fix/video-encoding-bug` - for bug fixes
- `refactor/cleanup-api` - for refactoring

## Development

### Prerequisites

- .NET 10.0 SDK

### Building

```bash
dotnet restore
dotnet build
```

### Running Tests

```bash
dotnet test
```

### Running the Server

```bash
dotnet run --project src/NoMercy.Service
```

## Commit Messages

Write clear, descriptive commit messages that explain what changed. No special format required.

Examples:
```
Add AV1 encoding support
Fix null reference in playlist endpoint
Update API documentation
```

## Submitting Changes

1. Push your branch to your fork
2. Open a Pull Request targeting the `dev` branch
3. Fill out the description with what changed and why
4. Address any review feedback

### Code Review

- Address any feedback from reviewers
- Push additional commits to your branch as needed
- Once approved, a maintainer will merge your PR

## What Happens After Merge

When your PR is merged to `dev`:

1. Automated tests run
2. Version is automatically incremented
3. Changes are squash-merged to `main` with a changelog
4. A new release is created with built executables
5. `dev` is synced back to `main`

Your contribution will appear in the release notes!

## Code Style

Please follow the existing code style in the project:

- Use explicit types (avoid `var`)
- Use PascalCase for public members
- Use camelCase with `_` prefix for private fields
- Use primary constructors where appropriate
- Keep methods focused and small

See [CLAUDE.md](CLAUDE.md) for detailed code style and tooling guidelines.

## Questions?

If you have questions or need help:

- Open a [Discussion](https://github.com/NoMercyTV/NoMercyMediaServer/discussions)
- Check existing [Issues](https://github.com/NoMercyTV/NoMercyMediaServer/issues)

## License and Contributor Agreement

By submitting a pull request or other contribution to this project, you
explicitly agree that:

1. Your contribution is licensed under the
   [NoMercy MediaServer License](LICENSE)
2. You assign all copyright in your contribution to
   NoMercy Entertainment
3. You have the legal right to make this assignment

This is required to ensure consistent licensing and long-term maintenance
of the project. If you do not agree with these terms, please do not submit
a contribution.