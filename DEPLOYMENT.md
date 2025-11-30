# Deployment Guide

## Overview

This project has separate build processes for backend (C#) and frontend (React). The deployment script ensures that backend and frontend deployments don't interfere with each other.

## Quick Reference

```bash
# Build and deploy backend only
./build.sh --backend
./deploy.sh --backend --restart

# Build and deploy frontend only
cd frontend && yarn build && cd ..
./deploy.sh --frontend

# Build and deploy everything
./build.sh --backend
cd frontend && yarn build && cd ..
./deploy.sh --all --restart
```

## Deployment Script Usage

The `deploy.sh` script safely handles deployment without accidentally deleting files:

### Backend Deployment
```bash
./deploy.sh --backend [--restart]
```
- Deploys backend files from `_output/net8.0/linux-x64/`
- **Preserves** the `UI/` directory (frontend files)
- Automatically fixes runtime config for .NET 8.0.20
- Optional `--restart` flag to restart Readarr

### Frontend Deployment
```bash
./deploy.sh --frontend
```
- Deploys frontend files from `_output/UI/`
- Does not affect backend files
- No restart needed (static files only)

### Full Deployment
```bash
./deploy.sh --all --restart
```
- Deploys both backend and frontend
- Restarts Readarr service

## Build Process

### Backend
```bash
./build.sh --backend
```
- Builds C# backend for all platforms
- Output: `_output/net8.0/{runtime}/`
- Includes all .NET assemblies and dependencies

### Frontend
```bash
cd frontend
yarn install --frozen-lockfile
yarn build
```
- Builds React frontend
- Output: `_output/UI/`
- Includes bundled JS, CSS, and static assets

## Manual Deployment (Not Recommended)

If you need to deploy manually:

### Backend (Preserving UI)
```bash
sudo rsync -av --exclude 'UI/' --exclude 'UI' --delete-excluded \
    _output/net8.0/linux-x64/ /opt/bookshelf/
sudo chown -R bookshelf:bookshelf /opt/bookshelf
```

### Frontend
```bash
sudo rsync -av --delete _output/UI/ /opt/bookshelf/UI/
sudo chown -R bookshelf:bookshelf /opt/bookshelf/UI
```

## Why This Approach?

The previous manual deployment with `rsync --delete` would delete the UI directory when deploying the backend, because:

1. Backend build output (`_output/net8.0/linux-x64/`) doesn't contain `UI/`
2. `rsync --delete` removes files at destination that don't exist in source
3. This caused the frontend to be deleted during backend deployment

The new deployment scripts (`deploy.sh` and `build-deploy.sh`) prevent this with **multiple safeguards**:

1. **Automatic UI Backup**: Before every backend deployment, the UI directory is backed up to `/tmp/`
2. **Automatic UI Restore**: If the UI directory is deleted or damaged during deployment, it's automatically restored from backup
3. **Explicit Exclusion**: Using `--exclude 'UI/'` to prevent rsync from touching the UI directory
4. **Separate Deployments**: Backend and frontend have separate deployment commands
5. **Runtime Config Fix**: Automatically fixes .NET 8.0.20 compatibility
6. **Safe Restart**: Optional service restart functionality

**The UI directory can NEVER be permanently lost** - even if rsync deletes it, it will be immediately restored from the automatic backup.

## Troubleshooting

### Frontend Returns 404
If you get 404 errors when accessing the site:
```bash
# Rebuild and redeploy frontend
cd frontend && yarn build && cd ..
./deploy.sh --frontend
```

### Backend Won't Start
Check for .NET version issues:
```bash
# The deploy script automatically fixes this, but if needed:
cd /opt/bookshelf
cat Readarr.runtimeconfig.json
# Should show "rollForward": "LatestMinor" and version "8.0.20"
```

### Check If Service Is Running
```bash
ps aux | grep '[d]otnet.*Readarr'
curl -s http://127.0.0.1:8787/api/v1/system/status | jq -r '.version'
```

## Production Deployment Location

- **Deploy Directory**: `/opt/bookshelf/`
- **Data Directory**: `/var/lib/bookshelf/`
- **Logs**: `/var/lib/bookshelf/logs/readarr.txt`
- **User**: `bookshelf`
- **Port**: `8787` (localhost only, proxied via nginx)
- **Public URL**: `https://readarr.s8n.is`
