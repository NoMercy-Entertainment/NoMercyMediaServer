#!/bin/bash
# Export/Import NoMercy MediaServer development configuration
# Allows sharing Claude CLI preferences and environment setup between containers

usage() {
    echo "Usage: $0 {export|import}"
    echo ""
    echo "Commands:"
    echo "  export  - Export current Claude settings, permissions, and shell config"
    echo "  import  - Import Claude settings into current container"
    exit 1
}

export_config() {
    echo "Exporting configuration..."
    
    # Create config directory
    mkdir -p .devcontainer/config
    
    # Export Claude settings if they exist
    if [ -f ~/.claude/settings.json ]; then
        cp ~/.claude/settings.json .devcontainer/config/claude-settings.json
        echo "✓ Exported Claude settings (permissions, preferences)"
    else
        echo "⚠ No Claude settings found to export"
    fi
    
    # Export Claude credentials (be careful with this!)
    if [ -f ~/.claude/.credentials.json ]; then
        cp ~/.claude/.credentials.json .devcontainer/config/claude-credentials.json
        chmod 600 .devcontainer/config/claude-credentials.json
        echo "✓ Exported Claude credentials (restricted access)"
    else
        echo "⚠ No Claude credentials found to export"
    fi
    
    # Create environment setup file
    cat > .devcontainer/config/shell-aliases.sh << 'EOF'
# NoMercy MediaServer Shell Configuration
# Source this file to apply development aliases and paths

# Claude CLI convenience alias
alias clauded="claude --dangerously-skip-permissions"

# Ensure .local/bin is in PATH for Claude and other tools
export PATH="${HOME}/.local/bin:${PATH}"
EOF
    echo "✓ Exported shell configuration"
    
    echo ""
    echo "Configuration exported to .devcontainer/config/"
    echo "Files:"
    ls -lh .devcontainer/config/
}

import_config() {
    if [ ! -d ".devcontainer/config" ]; then
        echo "✗ Configuration directory not found: .devcontainer/config"
        return 1
    fi
    
    echo "Importing configuration..."
    
    # Ensure Claude directory exists
    mkdir -p ~/.claude
    
    # Import Claude settings
    if [ -f ".devcontainer/config/claude-settings.json" ]; then
        cp .devcontainer/config/claude-settings.json ~/.claude/settings.json
        echo "✓ Imported Claude settings (permissions, preferences)"
    fi
    
    # Import Claude credentials (if present)
    if [ -f ".devcontainer/config/claude-credentials.json" ]; then
        cp .devcontainer/config/claude-credentials.json ~/.claude/.credentials.json
        chmod 600 ~/.claude/.credentials.json
        echo "✓ Imported Claude credentials"
    fi
    
    # Apply shell configuration
    if [ -f ".devcontainer/config/shell-aliases.sh" ]; then
        # Only add if not already present
        if ! grep -q "alias clauded=" ~/.bashrc; then
            echo "" >> ~/.bashrc
            echo "# NoMercy MediaServer configuration (auto-generated)" >> ~/.bashrc
            cat .devcontainer/config/shell-aliases.sh >> ~/.bashrc
            echo "✓ Imported shell aliases and paths"
        else
            echo "✓ Shell aliases already configured"
        fi
    fi
    
    echo ""
    echo "✓ Configuration imported successfully"
    echo ""
    echo "Your Claude preferences are now active:"
    echo "  - Permissions: Loaded from settings"
    echo "  - Alias: 'clauded' available (runs with --dangerously-skip-permissions)"
}

if [ $# -ne 1 ]; then
    usage
fi

case "$1" in
    export)
        export_config
        ;;
    import)
        import_config
        ;;
    *)
        echo "Unknown command: $1"
        usage
        ;;
esac
