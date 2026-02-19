# NoMercy MediaServer Shell Configuration
# Source this file to apply development aliases and paths

# Claude CLI convenience alias
alias clauded="claude --dangerously-skip-permissions"

# Ensure .local/bin is in PATH for Claude and other tools
export PATH="${HOME}/.local/bin:${PATH}"
