#!/bin/bash

# Bookshelf - Unified Build & Deploy Script
# Handles backend (.NET), frontend (React), and deployment with style

set -e  # Exit on error

# Color codes for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
PURPLE='\033[0;35m'
CYAN='\033[0;36m'
NC='\033[0m' # No Color

# Emojis for better UX
ROCKET="🚀"
CHECK="✅"
CROSS="❌"
WARNING="⚠️"
GEAR="⚙️"
PACKAGE="📦"
BOOK="📚"
CLEAN="🧹"
DEPLOY="🎯"

# Configuration
PROJECT_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
DEPLOY_DIR="/opt/bookshelf"
BUILD_OUTPUT="_output"
BACKEND_RUNTIME="net8.0/linux-x64"

# Print functions
print_header() {
    echo -e "\n${PURPLE}╔═══════════════════════════════════════════════════════════════════════════════╗${NC}"
    echo -e "${PURPLE}║                           Bookshelf Build System                              ║${NC}"
    echo -e "${PURPLE}║                         (Readarr Multi-Provider Fork)                         ║${NC}"
    echo -e "${PURPLE}╚═══════════════════════════════════════════════════════════════════════════════╝${NC}"

    # Show git/GitHub status warnings
    check_git_status
}

print_section() {
    echo -e "\n${CYAN}${GEAR} $1${NC}"
    echo -e "${CYAN}$(printf '%.0s─' {1..80})${NC}"
}

print_success() {
    echo -e "${GREEN}${CHECK} $1${NC}"
}

print_error() {
    echo -e "${RED}${CROSS} $1${NC}" >&2
}

print_warning() {
    echo -e "${YELLOW}${WARNING} $1${NC}"
}

print_info() {
    echo -e "${BLUE}${GEAR} $1${NC}"
}

# Command existence check
command_exists() {
    command -v "$1" >/dev/null 2>&1
}

# Git repository status check
check_git_status() {
    if ! git rev-parse --git-dir > /dev/null 2>&1; then
        return 0  # Not a git repo, skip checks
    fi

    local warnings=()

    # Check for uncommitted changes
    local uncommitted=$(git status --porcelain 2>/dev/null | wc -l)
    if [ "$uncommitted" -gt 20 ]; then
        warnings+=("🔄 You have $uncommitted uncommitted changes - consider committing before deploying")
    elif [ "$uncommitted" -gt 5 ]; then
        warnings+=("📝 You have $uncommitted uncommitted changes")
    fi

    # Check for unpushed commits
    local current_branch=$(git branch --show-current 2>/dev/null || echo "")
    if [ -n "$current_branch" ]; then
        local unpushed=$(git rev-list --count @{u}..HEAD 2>/dev/null || echo "0")
        if [ "$unpushed" -gt 0 ]; then
            warnings+=("📤 You have $unpushed unpushed commits on branch '$current_branch'")
        fi

        # Check if branch is behind develop
        check_branch_divergence warnings "$current_branch"
    fi

    # Check for unmerged pull requests (if gh CLI is available)
    if command_exists gh; then
        check_github_status warnings
    fi

    # Display warnings if any
    if [ ${#warnings[@]} -gt 0 ]; then
        echo -e "\n${YELLOW}${WARNING} Git Status Notifications:${NC}"
        for warning in "${warnings[@]}"; do
            echo -e "  ${YELLOW}$warning${NC}"
        done
        echo ""
    fi
}

check_branch_divergence() {
    local -n warnings_ref=$1
    local current_branch=$2

    # Skip if we're on develop
    if [[ "$current_branch" == "develop" ]]; then
        return 0
    fi

    # Check how far behind develop we are
    if git show-ref --verify --quiet refs/heads/develop; then
        local behind=$(git rev-list --count HEAD..develop 2>/dev/null || echo "0")
        local ahead=$(git rev-list --count develop..HEAD 2>/dev/null || echo "0")

        if [ "$behind" -gt 20 ]; then
            warnings_ref+=("📉 Branch '$current_branch' is $behind commits behind 'develop' - consider rebasing")
        elif [ "$behind" -gt 5 ]; then
            warnings_ref+=("📋 Branch '$current_branch' is $behind commits behind 'develop'")
        fi
    fi
}

check_github_status() {
    local -n warnings_ref=$1

    # Check if we're in a GitHub repo
    local github_repo=$(gh repo view --json nameWithOwner --jq .nameWithOwner 2>/dev/null || echo "")
    if [ -z "$github_repo" ]; then
        return 0
    fi

    # Check for open pull requests
    local pr_count=$(gh pr list --state open --json number 2>/dev/null | jq length 2>/dev/null || echo "0")

    if [ "$pr_count" -gt 0 ]; then
        warnings_ref+=("🔀 There are $pr_count open pull request(s) in $github_repo")
    fi
}

# Show comprehensive status
show_status() {
    print_section "Repository Status"

    if ! git rev-parse --git-dir > /dev/null 2>&1; then
        print_warning "Not in a git repository"
        return 0
    fi

    # Basic git status
    echo -e "${BLUE}Git Status:${NC}"
    local current_branch=$(git branch --show-current 2>/dev/null || echo "detached")
    local uncommitted=$(git status --porcelain 2>/dev/null | wc -l)
    local unpushed=$(git rev-list --count @{u}..HEAD 2>/dev/null || echo "unknown")
    local last_commit=$(git log -1 --pretty=format:"%h - %s (%ar)" 2>/dev/null)

    echo -e "  Branch: ${CYAN}$current_branch${NC}"
    echo -e "  Uncommitted changes: ${CYAN}$uncommitted${NC}"
    echo -e "  Unpushed commits: ${CYAN}$unpushed${NC}"
    echo -e "  Last commit: ${CYAN}$last_commit${NC}"

    # Backend build info
    if [ -d "$BUILD_OUTPUT/$BACKEND_RUNTIME" ]; then
        echo -e "\n${BLUE}Backend Build:${NC}"
        local backend_time=$(stat -c %y "$BUILD_OUTPUT/$BACKEND_RUNTIME/Readarr.dll" 2>/dev/null | cut -d. -f1)
        if [ -n "$backend_time" ]; then
            echo -e "  Last built: ${CYAN}$backend_time${NC}"
        fi
    fi

    # Frontend build info
    if [ -d "$BUILD_OUTPUT/UI" ]; then
        echo -e "\n${BLUE}Frontend Build:${NC}"
        local frontend_time=$(stat -c %y "$BUILD_OUTPUT/UI/index.html" 2>/dev/null | cut -d. -f1)
        if [ -n "$frontend_time" ]; then
            echo -e "  Last built: ${CYAN}$frontend_time${NC}"
        fi
    fi

    # Deployment info
    if [ -d "$DEPLOY_DIR" ]; then
        echo -e "\n${BLUE}Deployment Status:${NC}"
        echo -e "  Deploy directory: ${CYAN}$DEPLOY_DIR${NC}"

        if pgrep -f 'dotnet.*Readarr' > /dev/null; then
            local pid=$(pgrep -f 'dotnet.*Readarr')
            local uptime=$(ps -p $pid -o etime= | tr -d ' ')
            echo -e "  Service status: ${GREEN}Running${NC} (PID: $pid, uptime: $uptime)"

            # Try to get version from API
            local version=$(curl -s http://127.0.0.1:8787/api/v1/system/status 2>/dev/null | jq -r '.version' 2>/dev/null || echo "unknown")
            if [ "$version" != "unknown" ] && [ -n "$version" ]; then
                echo -e "  Version: ${CYAN}$version${NC}"
            fi
        else
            echo -e "  Service status: ${RED}Not running${NC}"
        fi
    fi

    # GitHub status if available
    if command_exists gh; then
        local github_repo=$(gh repo view --json nameWithOwner --jq .nameWithOwner 2>/dev/null || echo "")
        if [ -n "$github_repo" ]; then
            echo -e "\n${BLUE}GitHub Status (${CYAN}$github_repo${BLUE}):${NC}"

            local open_prs=$(gh pr list --state open --json number 2>/dev/null | jq length 2>/dev/null || echo "0")
            local open_issues=$(gh issue list --state open --json number 2>/dev/null | jq length 2>/dev/null || echo "0")

            echo -e "  Open pull requests: ${CYAN}$open_prs${NC}"
            echo -e "  Open issues: ${CYAN}$open_issues${NC}"
        fi
    fi
}

# Build backend
build_backend() {
    print_section "Building .NET Backend"

    cd "$PROJECT_ROOT"

    print_info "Building backend with ./build.sh --backend..."
    ./build.sh --backend

    print_success "Backend built successfully"

    # Show build output info
    if [ -f "$BUILD_OUTPUT/$BACKEND_RUNTIME/Readarr.dll" ]; then
        local size=$(du -sh "$BUILD_OUTPUT/$BACKEND_RUNTIME" | cut -f1)
        print_info "Build output size: $size"
    fi
}

# Build frontend
build_frontend() {
    print_section "Building React Frontend"

    cd "$PROJECT_ROOT/frontend"

    print_info "Installing frontend dependencies..."
    yarn install --frozen-lockfile --silent

    print_info "Building frontend with yarn build..."
    yarn build

    cd "$PROJECT_ROOT"
    print_success "Frontend built successfully"

    # Show build output info
    if [ -d "$BUILD_OUTPUT/UI" ]; then
        local size=$(du -sh "$BUILD_OUTPUT/UI" | cut -f1)
        print_info "Build output size: $size"
    fi
}

# Deploy backend (preserves UI)
deploy_backend() {
    print_section "Deploying Backend to Production"

    if [ ! -d "$BUILD_OUTPUT/$BACKEND_RUNTIME" ]; then
        print_error "Backend build not found. Run: $0 build-backend first"
        return 1
    fi

    print_info "Deploying backend files (preserving UI directory)..."

    # SAFEGUARD: Backup UI directory before deployment to prevent accidental deletion
    UI_BACKUP_DIR="/tmp/bookshelf-ui-backup-$$"
    if [ -d "${DEPLOY_DIR}/UI" ]; then
        print_info "🛡️  Backing up UI directory..."
        sudo cp -a "${DEPLOY_DIR}/UI" "${UI_BACKUP_DIR}"
    fi

    # Deploy backend files WITHOUT --delete to preserve UI directory
    # Using --exclude to skip UI and prevent accidental overwrite
    sudo rsync -av \
        --exclude 'UI/' \
        --exclude 'UI' \
        "${BUILD_OUTPUT}/${BACKEND_RUNTIME}/" \
        "${DEPLOY_DIR}/"

    # SAFEGUARD: Restore UI directory if it was deleted or damaged
    if [ -d "${UI_BACKUP_DIR}" ]; then
        if [ ! -d "${DEPLOY_DIR}/UI" ] || [ -z "$(ls -A ${DEPLOY_DIR}/UI 2>/dev/null)" ]; then
            print_warning "UI directory missing! Restoring from backup..."
            sudo cp -a "${UI_BACKUP_DIR}" "${DEPLOY_DIR}/UI"
        fi
        sudo rm -rf "${UI_BACKUP_DIR}"
        print_success "UI directory preserved successfully"
    fi

    # Fix the runtime config to use available .NET version
    print_info "Configuring .NET runtime..."
    sudo tee "${DEPLOY_DIR}/Readarr.runtimeconfig.json" > /dev/null << 'EOF'
{
  "runtimeOptions": {
    "tfm": "net8.0",
    "rollForward": "LatestMinor",
    "frameworks": [
      {
        "name": "Microsoft.NETCore.App",
        "version": "8.0.20"
      },
      {
        "name": "Microsoft.AspNetCore.App",
        "version": "8.0.20"
      }
    ],
    "configProperties": {
      "System.Reflection.Metadata.MetadataUpdater.IsSupported": false,
      "System.Runtime.Serialization.EnableUnsafeBinaryFormatterSerialization": false
    }
  }
}
EOF

    sudo chown -R bookshelf:bookshelf "${DEPLOY_DIR}"

    print_success "Backend deployed successfully"
}

# Deploy frontend
deploy_frontend() {
    print_section "Deploying Frontend to Production"

    if [ ! -d "$BUILD_OUTPUT/UI" ]; then
        print_error "Frontend build not found. Run: $0 build-frontend first"
        return 1
    fi

    print_info "Deploying frontend files..."

    # Deploy UI files
    sudo rsync -av --delete \
        "${BUILD_OUTPUT}/UI/" \
        "${DEPLOY_DIR}/UI/"

    sudo chown -R bookshelf:bookshelf "${DEPLOY_DIR}/UI"

    print_success "Frontend deployed successfully"
}

# Restart service
restart_service() {
    print_section "Restarting Readarr Service"

    # Check if already running
    if pgrep -f 'dotnet.*Readarr' > /dev/null; then
        print_info "Stopping existing Readarr process..."
        sudo pkill -f 'dotnet.*Readarr.dll' || true
        sleep 2
    fi

    print_info "Starting Readarr..."
    cd "${DEPLOY_DIR}"
    sudo -u bookshelf nohup dotnet Readarr.dll -nobrowser -data=/var/lib/bookshelf > /dev/null 2>&1 &

    sleep 5

    # Verify it started
    if pgrep -f 'dotnet.*Readarr' > /dev/null; then
        local pid=$(pgrep -f 'dotnet.*Readarr')
        print_success "Readarr started successfully (PID: $pid)"

        # Try to get version from API
        sleep 3
        local version=$(curl -s http://127.0.0.1:8787/api/v1/system/status 2>/dev/null | jq -r '.version' 2>/dev/null || echo "")
        if [ -n "$version" ]; then
            print_info "Running version: $version"
        fi
    else
        print_error "Failed to start Readarr"
        print_info "Check logs at: /var/lib/bookshelf/logs/readarr.txt"
        return 1
    fi
}

# Check service status
check_service() {
    print_section "Checking Readarr Service"

    if pgrep -f 'dotnet.*Readarr' > /dev/null; then
        local pid=$(pgrep -f 'dotnet.*Readarr')
        local uptime=$(ps -p $pid -o etime= | tr -d ' ')
        local mem=$(ps -p $pid -o rss= | awk '{print int($1/1024)"M"}')

        print_success "Readarr is running"
        echo -e "  ${BLUE}PID:${NC} $pid"
        echo -e "  ${BLUE}Uptime:${NC} $uptime"
        echo -e "  ${BLUE}Memory:${NC} $mem"

        # Check API
        print_info "Checking API..."
        if curl -s http://127.0.0.1:8787/api/v1/system/status > /dev/null 2>&1; then
            local version=$(curl -s http://127.0.0.1:8787/api/v1/system/status 2>/dev/null | jq -r '.version' 2>/dev/null || echo "unknown")
            print_success "API is responding"
            echo -e "  ${BLUE}Version:${NC} $version"
        else
            print_warning "API is not responding (may still be starting up)"
        fi

        # Check logs
        if [ -f "/var/lib/bookshelf/logs/readarr.txt" ]; then
            local errors=$(tail -50 /var/lib/bookshelf/logs/readarr.txt | grep -i error | wc -l)
            if [ "$errors" -gt 0 ]; then
                print_warning "Found $errors errors in recent logs"
                echo -e "  ${YELLOW}Check: tail -f /var/lib/bookshelf/logs/readarr.txt${NC}"
            else
                print_success "No recent errors in logs"
            fi
        fi
    else
        print_error "Readarr is not running"
        echo -e "  ${YELLOW}Start with: $0 restart${NC}"
    fi
}

# View logs
view_logs() {
    print_section "Viewing Readarr Logs"

    local log_file="/var/lib/bookshelf/logs/readarr.txt"
    local lines="${1:-50}"

    if [ ! -f "$log_file" ]; then
        print_error "Log file not found: $log_file"
        return 1
    fi

    if [ "$lines" = "follow" ]; then
        print_info "Following logs (Ctrl+C to stop)..."
        tail -f "$log_file"
    else
        print_info "Last $lines lines:"
        tail -n "$lines" "$log_file"
    fi
}

# Clean build artifacts
clean_all() {
    print_section "Cleaning Build Artifacts"

    cd "$PROJECT_ROOT"

    print_info "Cleaning backend artifacts..."
    rm -rf "$BUILD_OUTPUT"
    rm -rf "_tests"
    print_success "Backend artifacts cleaned"

    print_info "Cleaning frontend artifacts..."
    if [ -d "frontend/node_modules" ]; then
        rm -rf frontend/node_modules
    fi
    if [ -d "frontend/_output" ]; then
        rm -rf frontend/_output
    fi
    print_success "Frontend artifacts cleaned"

    print_success "All build artifacts cleaned"
}

# Help/Usage
show_help() {
    print_header
    echo -e "\n${BLUE}Usage: $0 [command]${NC}\n"

    echo -e "${YELLOW}Build Commands:${NC}"
    echo -e "  ${GREEN}build-backend${NC}     Build .NET backend only"
    echo -e "  ${GREEN}build-frontend${NC}    Build React frontend only"
    echo -e "  ${GREEN}build-all${NC}         Build both backend and frontend"
    echo ""

    echo -e "${YELLOW}Deployment Commands:${NC}"
    echo -e "  ${GREEN}deploy-backend${NC}    Deploy backend to production (preserves UI)"
    echo -e "  ${GREEN}deploy-frontend${NC}   Deploy frontend to production"
    echo -e "  ${GREEN}deploy-all${NC}        Deploy both backend and frontend"
    echo ""

    echo -e "${YELLOW}Service Management:${NC}"
    echo -e "  ${GREEN}restart${NC}           Restart Readarr service"
    echo -e "  ${GREEN}status${NC}            Show detailed status and git info"
    echo -e "  ${GREEN}check${NC}             Check if Readarr service is running"
    echo -e "  ${GREEN}logs [N|follow]${NC}   View last N lines of logs (default: 50) or follow"
    echo ""

    echo -e "${YELLOW}Utility Commands:${NC}"
    echo -e "  ${GREEN}clean${NC}             Remove all build artifacts"
    echo -e "  ${GREEN}help${NC}              Show this help message"
    echo ""

    echo -e "${YELLOW}Common Workflows:${NC}"
    echo -e "  ${CYAN}$0 build-all && $0 deploy-all && $0 restart${NC}"
    echo -e "    ${BLUE}→${NC} Full build, deploy, and restart"
    echo ""
    echo -e "  ${CYAN}$0 build-backend && $0 deploy-backend && $0 restart${NC}"
    echo -e "    ${BLUE}→${NC} Update backend only (preserves frontend)"
    echo ""
    echo -e "  ${CYAN}$0 build-frontend && $0 deploy-frontend${NC}"
    echo -e "    ${BLUE}→${NC} Update frontend only (no restart needed)"
    echo ""
    echo -e "  ${CYAN}$0 status && $0 check${NC}"
    echo -e "    ${BLUE}→${NC} Full status check"
    echo ""
    echo -e "  ${CYAN}$0 logs follow${NC}"
    echo -e "    ${BLUE}→${NC} Watch logs in real-time"
    echo ""

    echo -e "${YELLOW}Quick Reference:${NC}"
    echo -e "  ${BLUE}Build output:${NC}     $BUILD_OUTPUT"
    echo -e "  ${BLUE}Deploy location:${NC}  $DEPLOY_DIR"
    echo -e "  ${BLUE}Data directory:${NC}   /var/lib/bookshelf"
    echo -e "  ${BLUE}Log file:${NC}         /var/lib/bookshelf/logs/readarr.txt"
    echo -e "  ${BLUE}Public URL:${NC}       https://readarr.s8n.is"
    echo ""
}

# Main command dispatcher
cd "$PROJECT_ROOT"

case "${1:-help}" in
    "build-backend"|"backend")
        print_header
        build_backend
        ;;

    "build-frontend"|"frontend")
        print_header
        build_frontend
        ;;

    "build-all"|"build")
        print_header
        build_backend
        build_frontend
        print_success "${ROCKET} All components built successfully!"
        ;;

    "deploy-backend")
        print_header
        deploy_backend
        print_info "Remember to restart the service: $0 restart"
        ;;

    "deploy-frontend")
        print_header
        deploy_frontend
        print_success "${DEPLOY} Frontend deployed (no restart needed)"
        ;;

    "deploy-all"|"deploy")
        print_header
        deploy_backend
        deploy_frontend
        print_success "${DEPLOY} All components deployed!"
        print_info "Remember to restart the service: $0 restart"
        ;;

    "restart")
        print_header
        restart_service
        ;;

    "status")
        print_header
        show_status
        ;;

    "check")
        print_header
        check_service
        ;;

    "logs")
        print_header
        view_logs "${2:-50}"
        ;;

    "clean")
        print_header
        clean_all
        ;;

    "help"|"-h"|"--help")
        show_help
        ;;

    *)
        print_error "Unknown command: $1"
        echo ""
        show_help
        exit 1
        ;;
esac
