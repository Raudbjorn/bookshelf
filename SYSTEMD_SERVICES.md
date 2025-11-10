# Systemd Services Setup Guide

Complete guide for setting up Bookshelf (Readarr fork) and rreading-glasses as systemd services with PostgreSQL.

## Table of Contents

- [Overview](#overview)
- [Prerequisites](#prerequisites)
- [Quick Start](#quick-start)
- [Detailed Setup](#detailed-setup)
- [Configuration](#configuration)
- [Service Management](#service-management)
- [Troubleshooting](#troubleshooting)
- [Security](#security)

---

## Overview

This setup provides production-ready systemd services for:

1. **Bookshelf** - Enhanced Readarr fork for book management
   - Port: 8787
   - User: `bookshelf`
   - Group: `media`
   - Database: PostgreSQL

2. **rreading-glasses** - Book metadata API service
   - Port: 8788
   - User: `rreading-glasses`
   - Database: PostgreSQL

Both services include:
- ✅ Hardened security with systemd sandboxing
- ✅ Automatic restart on failure
- ✅ Proper logging with journald
- ✅ PostgreSQL database support
- ✅ Follows systemd best practices

---

## Prerequisites

### Required Packages

```bash
# Arch Linux
sudo pacman -S dotnet-runtime-8.0 aspnet-runtime-8.0 postgresql

# Debian/Ubuntu
sudo apt install aspnetcore-runtime-8.0 postgresql postgresql-contrib

# Install Go (for rreading-glasses)
# Arch: sudo pacman -S go
# Ubuntu: sudo apt install golang-go
```

### Build Applications

**Bookshelf:**
```bash
cd /path/to/my_bookshelf
./build-optimized.sh
```

**rreading-glasses:**
```bash
cd /path/to/rreading-glasses
go build -o rggr ./cmd/rggr  # For GoodReads API
# OR
go build -o rghc ./cmd/rghc  # For Hardcover API
```

---

## Quick Start

### Automated Installation

```bash
# Install both services
cd systemd
sudo ./install-services.sh

# Or install individually
sudo ./install-services.sh --bookshelf-only
sudo ./install-services.sh --rreading-only
```

### Setup PostgreSQL

```bash
sudo ./setup-postgresql.sh
```

### Deploy Applications

```bash
# Deploy Bookshelf
sudo mkdir -p /opt/bookshelf
sudo cp -r _output/net8.0/* /opt/bookshelf/
sudo chown -R bookshelf:media /opt/bookshelf

# Deploy rreading-glasses
sudo mkdir -p /opt/rreading-glasses
sudo cp rggr /opt/rreading-glasses/  # or rghc
sudo chown -R rreading-glasses:rreading-glasses /opt/rreading-glasses
```

### Start Services

```bash
# Enable and start Bookshelf
sudo systemctl enable --now bookshelf

# Enable and start rreading-glasses
sudo systemctl enable --now rreading-glasses

# Check status
sudo systemctl status bookshelf
sudo systemctl status rreading-glasses
```

---

## Detailed Setup

### 1. Install Systemd Service Files

The installation script copies files to:

| File | Location | Purpose |
|------|----------|---------|
| `*.service` | `/etc/systemd/system/` | Service definitions |
| `*.sysusers` | `/usr/lib/sysusers.d/` | User/group creation |
| `*.tmpfiles` | `/usr/lib/tmpfiles.d/` | Directory creation |

**Manual installation:**

```bash
cd systemd

# Bookshelf
sudo install -Dm644 bookshelf.service /etc/systemd/system/
sudo install -Dm644 bookshelf.sysusers /usr/lib/sysusers.d/bookshelf.conf
sudo install -Dm644 bookshelf.tmpfiles /usr/lib/tmpfiles.d/bookshelf.conf

# rreading-glasses
sudo install -Dm644 rreading-glasses.service /etc/systemd/system/
sudo install -Dm644 rreading-glasses.sysusers /usr/lib/sysusers.d/rreading-glasses.conf
sudo install -Dm644 rreading-glasses.tmpfiles /usr/lib/tmpfiles.d/rreading-glasses.conf

# Reload systemd
sudo systemctl daemon-reload
```

### 2. Create Users and Directories

```bash
# Create users and groups
sudo systemd-sysusers

# Create directories
sudo systemd-tmpfiles --create
```

### 3. Configure PostgreSQL

The setup script creates:
- Two databases: `bookshelf` and `rreading_glasses`
- Two users with secure passwords
- Configuration files with credentials

```bash
sudo ./setup-postgresql.sh
```

**Manual PostgreSQL setup:**

```bash
sudo -u postgres psql <<EOF
CREATE DATABASE bookshelf;
CREATE USER bookshelf WITH PASSWORD 'your_secure_password';
GRANT ALL PRIVILEGES ON DATABASE bookshelf TO bookshelf;

CREATE DATABASE rreading_glasses;
CREATE USER rreading_glasses WITH PASSWORD 'another_secure_password';
GRANT ALL PRIVILEGES ON DATABASE rreading_glasses TO rreading_glasses;
EOF
```

### 4. Configure Applications

**Bookshelf PostgreSQL Configuration:**

Edit `/var/lib/bookshelf/config.xml` (after first run):

```xml
<Config>
  <PostgresHost>localhost</PostgresHost>
  <PostgresPort>5432</PostgresPort>
  <PostgresUser>bookshelf</PostgresUser>
  <PostgresPassword>password_from_/etc/bookshelf/database.conf</PostgresPassword>
  <PostgresMainDb>bookshelf</PostgresMainDb>
</Config>
```

**rreading-glasses Configuration:**

Edit `/etc/rreading-glasses/rreading-glasses.env`:

```bash
POSTGRES_HOST=localhost
POSTGRES_PORT=5432
POSTGRES_DB=rreading_glasses
POSTGRES_USER=rreading_glasses
POSTGRES_PASSWORD=your_password_here
PORT=8788
```

---

## Configuration

### Systemd Overrides

Use `systemctl edit` to customize without modifying service files:

```bash
# Bookshelf overrides
sudo systemctl edit bookshelf
```

**Common overrides:**

```ini
[Service]
# Custom group for shared media access
Group=media
UMask=002

# Media in /home (not recommended)
PrivateUsers=false
ProtectHome=false

# Custom database
Environment="POSTGRES_HOST=db.example.com"
```

### Port Configuration

**Bookshelf** (default: 8787):
```bash
sudo systemctl edit bookshelf
```
```ini
[Service]
Environment="PORT=8080"
```

**rreading-glasses** (default: 8788):
Edit `/etc/rreading-glasses/rreading-glasses.env`:
```bash
PORT=8080
```

### Log Locations

- Bookshelf: `/var/log/bookshelf/` → `/var/lib/bookshelf/logs/`
- rreading-glasses: `/var/log/rreading-glasses/` → `/var/lib/rreading-glasses/logs/`
- journald: `journalctl -u <service>`

---

## Service Management

### Basic Commands

```bash
# Start service
sudo systemctl start <service>

# Stop service
sudo systemctl stop <service>

# Restart service
sudo systemctl restart <service>

# Enable on boot
sudo systemctl enable <service>

# Disable on boot
sudo systemctl disable <service>

# Check status
sudo systemctl status <service>
```

### View Logs

```bash
# Follow logs in real-time
sudo journalctl -u bookshelf -f
sudo journalctl -u rreading-glasses -f

# View recent logs
sudo journalctl -u bookshelf -n 100

# View logs since date
sudo journalctl -u bookshelf --since "2025-01-01"

# View only errors
sudo journalctl -u bookshelf -p err
```

### Performance Monitoring

```bash
# View service resource usage
systemd-cgtop

# Detailed service info
systemd-analyze blame
systemd-analyze critical-chain <service>
```

---

## Troubleshooting

### Service Won't Start

```bash
# Check status
sudo systemctl status bookshelf

# Check detailed logs
sudo journalctl -xe -u bookshelf

# Verify binary exists
ls -la /opt/bookshelf/Readarr

# Check permissions
namei -l /opt/bookshelf/Readarr
```

### Database Connection Issues

```bash
# Test PostgreSQL connection
sudo -u bookshelf psql -h localhost -U bookshelf -d bookshelf -c '\q'

# Check if PostgreSQL is running
sudo systemctl status postgresql

# View PostgreSQL logs
sudo journalctl -u postgresql
```

### Permission Problems

```bash
# Fix ownership
sudo chown -R bookshelf:media /var/lib/bookshelf
sudo chown -R rreading-glasses:rreading-glasses /var/lib/rreading-glasses

# Check directory permissions
ls -la /var/lib/bookshelf
ls -la /var/lib/rreading-glasses
```

### Port Already in Use

```bash
# Check what's using the port
sudo ss -tulpn | grep :8787
sudo ss -tulpn | grep :8788

# Kill process using port
sudo fuser -k 8787/tcp
```

### Service Crashes

```bash
# View crash logs
sudo journalctl -u bookshelf --since "1 hour ago"

# Check for core dumps
coredumpctl list
coredumpctl info <PID>

# Disable automatic restart temporarily
sudo systemctl edit bookshelf
```
```ini
[Service]
Restart=no
```

---

## Security

### Hardening Features Enabled

Both services include comprehensive systemd hardening:

- **Capabilities**: Limited to CAP_CHOWN, CAP_FSETID, CAP_SETGID
- **Namespaces**: Restricted
- **Filesystem**:
  - `ProtectSystem=full` - Read-only /usr, /boot, /efi
  - `ProtectHome=read-only` (Bookshelf) or `true` (rreading-glasses)
  - `PrivateTmp=true` - Private /tmp
- **Network**:
  - Restricted address families (IPv4, IPv6, UNIX only)
  - Socket binding limited to configured ports
- **System Calls**: Filtered to @system-service only

### Reviewing Security

```bash
# Analyze security
systemd-analyze security bookshelf
systemd-analyze security rreading-glasses

# View effective permissions
systemctl show bookshelf | grep -i protect
```

### Adjusting Security

For specific needs, override in `/etc/systemd/system/<service>.service.d/override.conf`:

```ini
[Service]
# Allow access to /home
ProtectHome=false

# Allow all capabilities (not recommended)
CapabilityBoundingSet=

# Disable user isolation (for LXC containers)
PrivateUsers=false
```

### Database Security

```bash
# PostgreSQL authentication
# Edit /var/lib/postgres/data/pg_hba.conf
local   bookshelf           bookshelf                           scram-sha-256
local   rreading_glasses    rreading_glasses                    scram-sha-256

# Reload PostgreSQL
sudo systemctl reload postgresql
```

---

## Reverse Proxy Setup

### Nginx

```nginx
# Bookshelf
server {
    listen 80;
    server_name bookshelf.example.com;

    location / {
        proxy_pass http://localhost:8787;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
    }
}

# rreading-glasses
server {
    listen 80;
    server_name api.bookinfo.example.com;

    location / {
        proxy_pass http://localhost:8788;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
    }
}
```

### Caddy

```caddy
bookshelf.example.com {
    reverse_proxy localhost:8787
}

api.bookinfo.example.com {
    reverse_proxy localhost:8788
}
```

---

## Backup and Restore

### Bookshelf

```bash
# Backup PostgreSQL database
sudo -u postgres pg_dump bookshelf > bookshelf_backup.sql

# Backup config and data
sudo tar -czf bookshelf_data.tar.gz /var/lib/bookshelf

# Restore
sudo -u postgres psql bookshelf < bookshelf_backup.sql
sudo tar -xzf bookshelf_data.tar.gz -C /
```

### rreading-glasses

```bash
# Backup database
sudo -u postgres pg_dump rreading_glasses > rreading_backup.sql

# Restore
sudo -u postgres psql rreading_glasses < rreading_backup.sql
```

---

## Uninstallation

```bash
# Stop and disable services
sudo systemctl stop bookshelf rreading-glasses
sudo systemctl disable bookshelf rreading-glasses

# Remove service files
sudo rm -f /etc/systemd/system/bookshelf.service
sudo rm -f /etc/systemd/system/rreading-glasses.service
sudo rm -f /usr/lib/sysusers.d/bookshelf.conf
sudo rm -f /usr/lib/sysusers.d/rreading-glasses.conf
sudo rm -f /usr/lib/tmpfiles.d/bookshelf.conf
sudo rm -f /usr/lib/tmpfiles.d/rreading-glasses.conf

# Reload systemd
sudo systemctl daemon-reload

# Optional: Remove data
sudo rm -rf /var/lib/bookshelf
sudo rm -rf /var/lib/rreading-glasses
sudo rm -rf /opt/bookshelf
sudo rm -rf /opt/rreading-glasses

# Optional: Remove users
sudo userdel bookshelf
sudo userdel rreading-glasses

# Optional: Drop databases
sudo -u postgres psql -c "DROP DATABASE bookshelf;"
sudo -u postgres psql -c "DROP DATABASE rreading_glasses;"
sudo -u postgres psql -c "DROP USER bookshelf;"
sudo -u postgres psql -c "DROP USER rreading_glasses;"
```

---

## Support

- **Bookshelf**: https://github.com/Raudbjorn/bookshelf
- **rreading-glasses**: https://github.com/mr55p-dev/rreading-glasses
- **Readarr**: https://wiki.servarr.com/readarr

---

**Created**: 2025-11-10
**Last Updated**: 2025-11-10
**Version**: 1.0.0
