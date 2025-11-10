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

# Generate random passwords
BOOKSHELF_PASSWORD=$(openssl rand -base64 32 | tr -d "=+/" | cut -c1-25)
RREADING_PASSWORD=$(openssl rand -base64 32 | tr -d "=+/" | cut -c1-25)

# Create bookshelf database and user
sudo -u postgres psql <<EOF
-- Create bookshelf database
CREATE DATABASE bookshelf;

-- Create bookshelf user
CREATE USER bookshelf WITH PASSWORD '$BOOKSHELF_PASSWORD';

-- Grant privileges
GRANT ALL PRIVILEGES ON DATABASE bookshelf TO bookshelf;

-- Connect to bookshelf database
\c bookshelf

-- Grant schema privileges (PostgreSQL 15+)
GRANT ALL ON SCHEMA public TO bookshelf;
EOF

echo -e "  ✓ Created database: bookshelf"
echo -e "  ✓ Created user: bookshelf"
echo ""

echo -e "${GREEN}[2/4] Setting up rreading-glasses database...${NC}"

# Create rreading-glasses database and user
sudo -u postgres psql <<EOF
-- Create rreading-glasses database
CREATE DATABASE rreading_glasses;

-- Create rreading-glasses user
CREATE USER rreading_glasses WITH PASSWORD '$RREADING_PASSWORD';

-- Grant privileges
GRANT ALL PRIVILEGES ON DATABASE rreading_glasses TO rreading_glasses;

-- Connect to rreading_glasses database
\c rreading_glasses

-- Grant schema privileges (PostgreSQL 15+)
GRANT ALL ON SCHEMA public TO rreading_glasses;
EOF

echo -e "  ✓ Created database: rreading_glasses"
echo -e "  ✓ Created user: rreading_glasses"
echo ""

echo -e "${GREEN}[3/4] Saving configuration files...${NC}"

# Create config directory for bookshelf
mkdir -p /etc/bookshelf
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
chown bookshelf:media /etc/bookshelf/database.conf 2>/dev/null || true

echo -e "  ✓ Saved: /etc/bookshelf/database.conf"

# Create config for rreading-glasses
mkdir -p /etc/rreading-glasses
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
chown rreading-glasses:rreading-glasses /etc/rreading-glasses/rreading-glasses.env 2>/dev/null || true

echo -e "  ✓ Saved: /etc/rreading-glasses/rreading-glasses.env"
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
