#!/bin/bash
# Installation script for Bookshelf and rreading-glasses systemd services
# Run as root or with sudo

set -e

# Colors
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

echo -e "${BLUE}╔════════════════════════════════════════════════╗${NC}"
echo -e "${BLUE}║  Systemd Services Installation                 ║${NC}"
echo -e "${BLUE}║  Bookshelf & rreading-glasses                  ║${NC}"
echo -e "${BLUE}╚════════════════════════════════════════════════╝${NC}"
echo ""

# Check if running as root
if [[ $EUID -ne 0 ]]; then
   echo -e "${RED}This script must be run as root or with sudo${NC}"
   exit 1
fi

# Parse arguments
INSTALL_BOOKSHELF=1
INSTALL_RREADING=1

while [[ $# -gt 0 ]]; do
    case $1 in
        --bookshelf-only)
            INSTALL_RREADING=0
            shift
            ;;
        --rreading-only)
            INSTALL_BOOKSHELF=0
            shift
            ;;
        --help)
            echo "Usage: $0 [OPTIONS]"
            echo ""
            echo "Options:"
            echo "  --bookshelf-only   Install only Bookshelf service"
            echo "  --rreading-only    Install only rreading-glasses service"
            echo "  --help             Show this help message"
            exit 0
            ;;
        *)
            echo "Unknown option: $1"
            echo "Use --help for usage information"
            exit 1
            ;;
    esac
done

# Install Bookshelf
if [[ $INSTALL_BOOKSHELF -eq 1 ]]; then
    echo -e "${GREEN}[1/3] Installing Bookshelf systemd service...${NC}"

    # Create user and group
    if ! getent group media > /dev/null 2>&1; then
        echo "  Creating group: media"
        groupadd -r media
    fi

    if ! id -u bookshelf > /dev/null 2>&1; then
        echo "  Creating user: bookshelf"
        useradd -r -g media -d /var/lib/bookshelf -s /usr/bin/nologin -c "Bookshelf (Readarr Fork) Daemon" bookshelf
    fi

    # Install sysusers config (alternative method)
    install -Dm644 "${SCRIPT_DIR}/bookshelf.sysusers" /usr/lib/sysusers.d/bookshelf.conf
    echo "  ✓ Installed sysusers config"

    # Install tmpfiles config
    install -Dm644 "${SCRIPT_DIR}/bookshelf.tmpfiles" /usr/lib/tmpfiles.d/bookshelf.conf
    echo "  ✓ Installed tmpfiles config"

    # Install systemd service
    install -Dm644 "${SCRIPT_DIR}/bookshelf.service" /etc/systemd/system/bookshelf.service
    echo "  ✓ Installed systemd service"

    # Create directories
    systemd-tmpfiles --create bookshelf.conf
    echo "  ✓ Created directories"

    # Set ownership
    chown -R bookshelf:media /var/lib/bookshelf 2>/dev/null || true

    echo ""
fi

# Install rreading-glasses
if [[ $INSTALL_RREADING -eq 1 ]]; then
    echo -e "${GREEN}[2/3] Installing rreading-glasses systemd service...${NC}"

    # Create user and group
    if ! id -u rreading-glasses > /dev/null 2>&1; then
        echo "  Creating user: rreading-glasses"
        useradd -r -d /var/lib/rreading-glasses -s /usr/bin/nologin -c "rreading-glasses Book Metadata API" rreading-glasses
    fi

    # Install sysusers config
    install -Dm644 "${SCRIPT_DIR}/rreading-glasses.sysusers" /usr/lib/sysusers.d/rreading-glasses.conf
    echo "  ✓ Installed sysusers config"

    # Install tmpfiles config
    install -Dm644 "${SCRIPT_DIR}/rreading-glasses.tmpfiles" /usr/lib/tmpfiles.d/rreading-glasses.conf
    echo "  ✓ Installed tmpfiles config"

    # Install systemd service
    install -Dm644 "${SCRIPT_DIR}/rreading-glasses.service" /etc/systemd/system/rreading-glasses.service
    echo "  ✓ Installed systemd service"

    # Install example env file
    if [[ ! -f /etc/rreading-glasses/rreading-glasses.env ]]; then
        mkdir -p /etc/rreading-glasses
        install -Dm600 "${SCRIPT_DIR}/rreading-glasses.env.example" /etc/rreading-glasses/rreading-glasses.env
        echo "  ✓ Installed example config (EDIT THIS!)"
    else
        echo "  ⚠ Config exists: /etc/rreading-glasses/rreading-glasses.env"
    fi

    # Create directories
    systemd-tmpfiles --create rreading-glasses.conf
    echo "  ✓ Created directories"

    # Set ownership
    chown -R rreading-glasses:rreading-glasses /var/lib/rreading-glasses 2>/dev/null || true

    echo ""
fi

echo -e "${GREEN}[3/3] Reloading systemd...${NC}"
systemctl daemon-reload
echo "  ✓ Systemd reloaded"
echo ""

echo -e "${BLUE}╔════════════════════════════════════════════════╗${NC}"
echo -e "${BLUE}║            Installation Complete!               ║${NC}"
echo -e "${BLUE}╚════════════════════════════════════════════════╝${NC}"
echo ""

if [[ $INSTALL_BOOKSHELF -eq 1 ]]; then
    echo -e "${YELLOW}Bookshelf:${NC}"
    echo -e "  Service installed: ${GREEN}bookshelf.service${NC}"
    echo -e "  User: bookshelf"
    echo -e "  Group: media"
    echo -e "  Data directory: /var/lib/bookshelf"
    echo -e "  Logs: /var/log/bookshelf"
    echo ""
    echo -e "  ${BLUE}Configure:${NC}"
    echo -e "    1. Install Bookshelf to /opt/bookshelf/"
    echo -e "    2. Setup PostgreSQL: sudo ${SCRIPT_DIR}/setup-postgresql.sh"
    echo -e "    3. Enable: ${GREEN}sudo systemctl enable bookshelf${NC}"
    echo -e "    4. Start: ${GREEN}sudo systemctl start bookshelf${NC}"
    echo ""
fi

if [[ $INSTALL_RREADING -eq 1 ]]; then
    echo -e "${YELLOW}rreading-glasses:${NC}"
    echo -e "  Service installed: ${GREEN}rreading-glasses.service${NC}"
    echo -e "  User: rreading-glasses"
    echo -e "  Data directory: /var/lib/rreading-glasses"
    echo -e "  Logs: /var/log/rreading-glasses"
    echo ""
    echo -e "  ${BLUE}Configure:${NC}"
    echo -e "    1. Install rreading-glasses to /opt/rreading-glasses/"
    echo -e "    2. Edit: ${YELLOW}/etc/rreading-glasses/rreading-glasses.env${NC}"
    echo -e "    3. Setup PostgreSQL: sudo ${SCRIPT_DIR}/setup-postgresql.sh"
    echo -e "    4. Enable: ${GREEN}sudo systemctl enable rreading-glasses${NC}"
    echo -e "    5. Start: ${GREEN}sudo systemctl start rreading-glasses${NC}"
    echo ""
fi

echo -e "${YELLOW}View logs:${NC}"
if [[ $INSTALL_BOOKSHELF -eq 1 ]]; then
    echo -e "  Bookshelf: ${BLUE}journalctl -u bookshelf -f${NC}"
fi
if [[ $INSTALL_RREADING -eq 1 ]]; then
    echo -e "  rreading-glasses: ${BLUE}journalctl -u rreading-glasses -f${NC}"
fi
echo ""

echo -e "${GREEN}Installation successful! 🚀${NC}"
