#!/bin/bash
# PostgreSQL setup script for Bookshelf and rreading-glasses
# Run as root or with sudo

set -e

# Colors
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

echo -e "${BLUE}╔════════════════════════════════════════════════╗${NC}"
echo -e "${BLUE}║  PostgreSQL Setup for Bookshelf & rreading-glasses  ║${NC}"
echo -e "${BLUE}╚════════════════════════════════════════════════╝${NC}"
echo ""

# Check if running as root or with sudo
if [[ $EUID -ne 0 ]]; then
   echo -e "${RED}This script must be run as root or with sudo${NC}"
   exit 1
fi

# Check if PostgreSQL is installed
if ! command -v psql &> /dev/null; then
    echo -e "${RED}PostgreSQL is not installed!${NC}"
    echo "Install it with:"
    echo "  Arch: sudo pacman -S postgresql"
    echo "  Debian/Ubuntu: sudo apt install postgresql postgresql-contrib"
    exit 1
fi

# Check if PostgreSQL is running
if ! systemctl is-active --quiet postgresql; then
    echo -e "${YELLOW}PostgreSQL is not running. Starting it...${NC}"
    systemctl start postgresql
    systemctl enable postgresql
fi

echo -e "${GREEN}[1/4] Setting up Bookshelf database...${NC}"

# Check if bookshelf user exists and generate password only if needed
BOOKSHELF_USER_EXISTS=$(sudo -u postgres psql -tAc "SELECT 1 FROM pg_catalog.pg_roles WHERE rolname='bookshelf'")
if [[ -z "$BOOKSHELF_USER_EXISTS" ]]; then
    BOOKSHELF_PASSWORD=$(openssl rand -hex 32)
    BOOKSHELF_USER_NEW=true
    echo "  Creating new bookshelf user..."
else
    BOOKSHELF_PASSWORD=""  # Will not be used
    BOOKSHELF_USER_NEW=false
    echo "  Bookshelf user already exists, skipping password generation"
fi

# Create bookshelf database and user (idempotent)
sudo -u postgres psql <<EOF
-- Create bookshelf user if it doesn't exist
DO \$\$
BEGIN
  IF NOT EXISTS (SELECT FROM pg_catalog.pg_roles WHERE rolname = 'bookshelf') THEN
    CREATE USER bookshelf WITH PASSWORD '$BOOKSHELF_PASSWORD';
  END IF;
END
\$\$;

-- Create bookshelf database if it doesn't exist
SELECT 'CREATE DATABASE bookshelf'
WHERE NOT EXISTS (SELECT FROM pg_database WHERE datname = 'bookshelf')\gexec

-- Grant privileges
GRANT ALL PRIVILEGES ON DATABASE bookshelf TO bookshelf;

-- Connect to bookshelf database
\c bookshelf

-- Grant schema privileges (PostgreSQL 15+)
GRANT ALL ON SCHEMA public TO bookshelf;
EOF

echo -e "  ✓ Created/verified database: bookshelf"
echo -e "  ✓ Created/verified user: bookshelf"
echo ""

echo -e "${GREEN}[2/4] Setting up rreading-glasses database...${NC}"

# Check if rreading-glasses user exists and generate password only if needed
RREADING_USER_EXISTS=$(sudo -u postgres psql -tAc "SELECT 1 FROM pg_catalog.pg_roles WHERE rolname='rreading_glasses'")
if [[ -z "$RREADING_USER_EXISTS" ]]; then
    RREADING_PASSWORD=$(openssl rand -hex 32)
    RREADING_USER_NEW=true
    echo "  Creating new rreading-glasses user..."
else
    RREADING_PASSWORD=""  # Will not be used
    RREADING_USER_NEW=false
    echo "  rreading-glasses user already exists, skipping password generation"
fi

# Create rreading-glasses database and user (idempotent)
sudo -u postgres psql <<EOF
-- Create rreading-glasses user if it doesn't exist
DO \$\$
BEGIN
  IF NOT EXISTS (SELECT FROM pg_catalog.pg_roles WHERE rolname = 'rreading_glasses') THEN
    CREATE USER rreading_glasses WITH PASSWORD '$RREADING_PASSWORD';
  END IF;
END
\$\$;

-- Create rreading-glasses database if it doesn't exist
SELECT 'CREATE DATABASE rreading_glasses'
WHERE NOT EXISTS (SELECT FROM pg_database WHERE datname = 'rreading_glasses')\gexec

-- Grant privileges
GRANT ALL PRIVILEGES ON DATABASE rreading_glasses TO rreading_glasses;

-- Connect to rreading_glasses database
\c rreading_glasses

-- Grant schema privileges (PostgreSQL 15+)
GRANT ALL ON SCHEMA public TO rreading_glasses;
EOF

echo -e "  ✓ Created/verified database: rreading_glasses"
echo -e "  ✓ Created/verified user: rreading_glasses"
echo ""

echo -e "${GREEN}[3/4] Saving configuration files...${NC}"

# Create config directory for bookshelf
mkdir -p /etc/bookshelf

# Only write config file if user was newly created (to avoid credential mismatch)
if [[ "$BOOKSHELF_USER_NEW" == "true" ]] || [[ ! -f /etc/bookshelf/database.conf ]]; then
    cat > /etc/bookshelf/database.conf <<EOF
# Bookshelf PostgreSQL Configuration
# This file is sourced by the systemd service

POSTGRES_HOST=localhost
POSTGRES_PORT=5432
POSTGRES_DB=bookshelf
POSTGRES_USER=bookshelf
POSTGRES_PASSWORD=$BOOKSHELF_PASSWORD
EOF
    chmod 600 /etc/bookshelf/database.conf

    # Set ownership only if user and group exist
    if id -u bookshelf >/dev/null 2>&1 && getent group media >/dev/null 2>&1; then
        if ! chown bookshelf:media /etc/bookshelf/database.conf; then
            echo -e "  ${YELLOW}Warning: Failed to set ownership for /etc/bookshelf/database.conf${NC}" >&2
        fi
    else
        echo -e "  ${YELLOW}Warning: User 'bookshelf' or group 'media' does not exist. Ownership not changed for /etc/bookshelf/database.conf${NC}" >&2
    fi

    echo -e "  ✓ Saved: /etc/bookshelf/database.conf"
else
    echo -e "  ⚠ Config exists and user exists: /etc/bookshelf/database.conf (skipping to avoid credential mismatch)"
fi

# Create config for rreading-glasses
mkdir -p /etc/rreading-glasses

# Only write config file if user was newly created (to avoid credential mismatch)
if [[ "$RREADING_USER_NEW" == "true" ]] || [[ ! -f /etc/rreading-glasses/rreading-glasses.env ]]; then
    cat > /etc/rreading-glasses/rreading-glasses.env <<EOF
# rreading-glasses PostgreSQL Configuration

POSTGRES_HOST=localhost
POSTGRES_PORT=5432
POSTGRES_DB=rreading_glasses
POSTGRES_USER=rreading_glasses
POSTGRES_PASSWORD=$RREADING_PASSWORD

# API Configuration
PORT=8788
EOF
    chmod 600 /etc/rreading-glasses/rreading-glasses.env

    # Set ownership only if user exists
    if id -u rreading-glasses >/dev/null 2>&1; then
        if ! chown rreading-glasses:rreading-glasses /etc/rreading-glasses/rreading-glasses.env; then
            echo -e "  ${YELLOW}Warning: Failed to set ownership for /etc/rreading-glasses/rreading-glasses.env${NC}" >&2
        fi
    else
        echo -e "  ${YELLOW}Warning: User 'rreading-glasses' does not exist. Ownership not set for /etc/rreading-glasses/rreading-glasses.env${NC}" >&2
    fi

    echo -e "  ✓ Saved: /etc/rreading-glasses/rreading-glasses.env"
else
    echo -e "  ⚠ Config exists and user exists: /etc/rreading-glasses/rreading-glasses.env (skipping to avoid credential mismatch)"
fi
echo ""

echo -e "${GREEN}[4/4] Testing connections...${NC}"

# Test bookshelf connection
if sudo -u postgres psql -d bookshelf -c '\q' 2>/dev/null; then
    echo -e "  ✓ Bookshelf database accessible"
else
    echo -e "  ${RED}✗ Bookshelf database connection failed${NC}"
fi

# Test rreading-glasses connection
if sudo -u postgres psql -d rreading_glasses -c '\q' 2>/dev/null; then
    echo -e "  ✓ rreading-glasses database accessible"
else
    echo -e "  ${RED}✗ rreading-glasses database connection failed${NC}"
fi

echo ""
echo -e "${BLUE}╔════════════════════════════════════════════════╗${NC}"
echo -e "${BLUE}║            Setup Complete!                      ║${NC}"
echo -e "${BLUE}╚════════════════════════════════════════════════╝${NC}"
echo ""
echo -e "${GREEN}✓ PostgreSQL databases configured successfully!${NC}"
echo ""
echo -e "${YELLOW}Configuration Summary:${NC}"
echo -e "  Bookshelf:"
echo -e "    Database: bookshelf"
echo -e "    User: bookshelf"
echo -e "    Config: /etc/bookshelf/database.conf"
echo ""
echo -e "  rreading-glasses:"
echo -e "    Database: rreading_glasses"
echo -e "    User: rreading_glasses"
echo -e "    Config: /etc/rreading-glasses/rreading-glasses.env"
echo ""
echo -e "${YELLOW}Next Steps:${NC}"
echo -e "  1. Configure Bookshelf to use PostgreSQL:"
echo -e "     Edit /var/lib/bookshelf/config.xml"
echo -e "     Set PostgresHost=localhost, PostgresPort=5432"
echo -e "     Set PostgresUser=bookshelf, PostgresPassword from /etc/bookshelf/database.conf"
echo ""
echo -e "  2. Start services:"
echo -e "     ${BLUE}sudo systemctl start bookshelf${NC}"
echo -e "     ${BLUE}sudo systemctl start rreading-glasses${NC}"
echo ""
echo -e "${GREEN}Happy reading! 📚${NC}"
