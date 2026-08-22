# Contributing to NoMercy MediaServer

Thank you for your interest in contributing to NoMercy MediaServer! This document outlines the process for contributing to this project.

## Branch Structure

| Branch | Purpose | CI |
|--------|---------|-----|
| `wip` | Work in progress, not ready | None |
| `dev` | Integration branch; push here to trigger a release | Full pipeline |
| `master` | Release mirror of `dev`, byte-identical after every release; never built directly | Automated |

**Maintainers**: Push to `dev` when ready to release, `wip` when saving work.

**External contributors**: Submit PRs to `dev`.

## Getting Started (External Contributors)

### 1. Fork the Repository

Click the "Fork" button on the [GitHub repository](https://github.com/NoMercy-Entertainment/nomercy-media-server) to create your own copy.

### 2. Clone Your Fork

```bash
git clone https://github.com/YOUR-USERNAME/NoMercyMediaServer.git
cd NoMercyMediaServer
```

### 3. Set Up Upstream Remote

```bash
git remote add upstream https://github.com/NoMercy-Entertainment/nomercy-media-server.git
```

### 4. Create a Feature Branch

Always create a new branch for your changes:

```bash
git checkout -b feat/your-feature-name
```

Use descriptive branch names:
- `feat/add-dark-mode` - for new features
- `fix/video-encoding-bug` - for bug fixes

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

Use Conventional Commits, `type(scope): description`.

Examples:
```
feat(encoder): add AV1 encoding support
fix(api): fix null reference in playlist endpoint
docs: update API documentation
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
3. `dev`'s tree is made byte-identical to `master` with a changelog commit
4. A new release is created with built executables, targeting `master`
5. `master` is never built directly; it exists only as the release mirror

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

- Open a [Discussion](https://github.com/NoMercy-Entertainment/nomercy-media-server/discussions)
- Check existing [Issues](https://github.com/NoMercy-Entertainment/nomercy-media-server/issues)

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