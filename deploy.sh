#!/usr/bin/env bash
set -e

# Deployment script for bookshelf
# Usage: ./deploy.sh [--backend] [--frontend] [--all]

DEPLOY_DIR="/opt/bookshelf"
BUILD_OUTPUT="_output"
BACKEND_RUNTIME="net8.0/linux-x64"

deploy_backend() {
    echo "📦 Deploying backend..."

    # SAFEGUARD: Backup UI directory before deployment to prevent accidental deletion
    UI_BACKUP_DIR="/tmp/bookshelf-ui-backup-$$"
    if [ -d "${DEPLOY_DIR}/UI" ]; then
        echo "🛡️  Backing up UI directory to ${UI_BACKUP_DIR}..."
        sudo cp -a "${DEPLOY_DIR}/UI" "${UI_BACKUP_DIR}"
    fi

    # Deploy backend files ONLY (explicitly exclude UI and use --delete to clean old backend files)
    sudo rsync -av \
        --exclude 'UI' \
        --delete \
        --delete-excluded \
        "${BUILD_OUTPUT}/${BACKEND_RUNTIME}/" \
        "${DEPLOY_DIR}/"

    # SAFEGUARD: Restore UI directory if it was deleted or damaged
    if [ -d "${UI_BACKUP_DIR}" ]; then
        if [ ! -d "${DEPLOY_DIR}/UI" ] || [ -z "$(ls -A ${DEPLOY_DIR}/UI 2>/dev/null)" ]; then
            echo "🔧 Restoring UI directory from backup..."
            sudo cp -a "${UI_BACKUP_DIR}" "${DEPLOY_DIR}/UI"
        fi
        sudo rm -rf "${UI_BACKUP_DIR}"
        echo "✅ UI directory preserved successfully"
    fi

    # Fix the runtime config to use available .NET version
    echo "🔧 Fixing runtime config..."
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
    echo "✅ Backend deployed successfully"
}

deploy_frontend() {
    echo "📦 Deploying frontend..."

    # Deploy UI files
    sudo rsync -av --delete \
        "${BUILD_OUTPUT}/UI/" \
        "${DEPLOY_DIR}/UI/"

    sudo chown -R bookshelf:bookshelf "${DEPLOY_DIR}/UI"
    echo "✅ Frontend deployed successfully"
}

restart_service() {
    echo "🔄 Restarting Readarr..."

    # Kill existing process
    sudo pkill -f 'dotnet.*Readarr.dll' || true
    sleep 2

    # Start new process
    cd "${DEPLOY_DIR}"
    sudo -u bookshelf nohup dotnet Readarr.dll -nobrowser -data=/var/lib/bookshelf > /dev/null 2>&1 &

    sleep 5

    # Verify it started
    if pgrep -f 'dotnet.*Readarr' > /dev/null; then
        echo "✅ Readarr started successfully"
    else
        echo "❌ Failed to start Readarr"
        exit 1
    fi
}

# Parse arguments
DEPLOY_BACKEND=NO
DEPLOY_FRONTEND=NO
RESTART=NO

if [ $# -eq 0 ]; then
    echo "Usage: $0 [--backend] [--frontend] [--all] [--restart]"
    echo "  --backend   Deploy backend only"
    echo "  --frontend  Deploy frontend only"
    echo "  --all       Deploy both backend and frontend"
    echo "  --restart   Restart Readarr after deployment"
    exit 1
fi

while [[ $# -gt 0 ]]; do
    case $1 in
        --backend)
            DEPLOY_BACKEND=YES
            shift
            ;;
        --frontend)
            DEPLOY_FRONTEND=YES
            shift
            ;;
        --all)
            DEPLOY_BACKEND=YES
            DEPLOY_FRONTEND=YES
            shift
            ;;
        --restart)
            RESTART=YES
            shift
            ;;
        *)
            echo "Unknown option: $1"
            exit 1
            ;;
    esac
done

# Execute deployment
if [ "$DEPLOY_BACKEND" = "YES" ]; then
    deploy_backend
fi

if [ "$DEPLOY_FRONTEND" = "YES" ]; then
    deploy_frontend
fi

if [ "$RESTART" = "YES" ]; then
    restart_service
fi

echo "🎉 Deployment complete!"
