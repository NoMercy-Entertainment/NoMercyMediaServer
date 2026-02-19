# Claude CLI in DevContainer

The official Claude CLI is installed via the installation script from https://claude.ai/install.sh

## Automatic Installation

The Claude CLI is automatically installed when the devcontainer is created via the `postCreateCommand`. The alias `clauded` is also configured automatically.

## Manual Installation

If you need to install or reinstall the Claude CLI manually:

```bash
# Run the installation script
chmod +x .devcontainer/install-claude.sh
./.devcontainer/install-claude.sh
```

Or install directly using the official installer:
```bash
curl -fsSL https://claude.ai/install.sh | bash
```

## Usage

After installation, you can use Claude CLI:

```bash
# Standard usage
claude --help
claude "your prompt here"

# Using the alias (skips permission checks)
clauded "your prompt here"
```

## Alias Configuration

The `clauded` alias is automatically added to your `.bashrc`:
```bash
alias clauded="claude --dangerously-skip-permissions"
```

To use the alias in your current shell without restarting:
```bash
source ~/.bashrc
```

## References
- Claude Code Documentation: https://code.claude.com/docs/en/overview
- Claude CLI Installation: https://claude.ai/install.sh
- Anthropic API Documentation: https://docs.anthropic.com
