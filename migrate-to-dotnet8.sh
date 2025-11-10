#!/bin/bash
# .NET 8 Migration Script for Bookshelf
# This script automates the .NET 6 → .NET 8 migration
set -e

# Colors
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

echo -e "${BLUE}╔════════════════════════════════════════╗${NC}"
echo -e "${BLUE}║  .NET 8 Migration Script for Bookshelf ║${NC}"
echo -e "${BLUE}╚════════════════════════════════════════╝${NC}"
echo ""

# Check if on clean branch
if [[ -n $(git status -s) ]]; then
    echo -e "${RED}Warning: You have uncommitted changes!${NC}"
    echo "Please commit or stash your changes before migrating."
    read -p "Continue anyway? (y/N): " -n 1 -r
    echo
    if [[ ! $REPLY =~ ^[Yy]$ ]]; then
        exit 1
    fi
fi

# Create migration branch
echo -e "${GREEN}[1/8] Creating migration branch...${NC}"
git checkout -b feature/dotnet8
echo -e "  ✓ Branch created: feature/dotnet8"
echo ""

# Update project files
echo -e "${GREEN}[2/8] Updating .csproj files (25 files)...${NC}"
count=$(find src -name "*.csproj" -exec sed -i 's/<TargetFrameworks>net6\.0<\/TargetFrameworks>/<TargetFrameworks>net8.0<\/TargetFrameworks>/g' {} \; -print | wc -l)
echo -e "  ✓ Updated ${count} project files"
echo ""

# Update Directory.Build.props
echo -e "${GREEN}[3/8] Updating Directory.Build.props...${NC}"
sed -i 's/net6\.0/net8.0/g' src/Directory.Build.props
echo -e "  ✓ Updated framework references"
echo ""

# Update build scripts
echo -e "${GREEN}[4/8] Updating build scripts...${NC}"
if [ -f build.sh ]; then
    sed -i "s/_framework='net6\.0'/_framework='net8.0'/g" build.sh
    sed -i 's/net6\.0/net8.0/g' build.sh
    echo -e "  ✓ Updated build.sh"
fi
if [ -f build-optimized.sh ]; then
    sed -i 's/FRAMEWORK="net6\.0"/FRAMEWORK="net8.0"/g' build-optimized.sh
    sed -i "s/_framework='net6\.0'/_framework='net8.0'/g" build-optimized.sh
    sed -i 's/net6\.0/net8.0/g' build-optimized.sh
    echo -e "  ✓ Updated build-optimized.sh"
fi
echo ""

# Update package dependencies
echo -e "${GREEN}[5/8] Updating package dependencies...${NC}"
if [ -f src/Directory.Packages.props ]; then
    # Update Microsoft packages from 6.x to 8.x
    sed -i 's/\(Microsoft\.AspNetCore\.[^"]*" Version="\)6\./\18./g' src/Directory.Packages.props
    sed -i 's/\(Microsoft\.Extensions\.[^"]*" Version="\)6\./\18./g' src/Directory.Packages.props
    sed -i 's/\(System\.[^"]*" Version="\)6\.0\./\18.0./g' src/Directory.Packages.props
    echo -e "  ✓ Updated Microsoft.* packages to 8.x"
    echo -e "  ${YELLOW}Note: Review Directory.Packages.props for any manual adjustments${NC}"
fi
echo ""

# Remove lock files
echo -e "${GREEN}[6/8] Removing package lock files (will regenerate)...${NC}"
lock_count=$(find src -name "packages.lock.json" -delete -print | wc -l)
echo -e "  ✓ Removed ${lock_count} lock files"
echo ""

# Restore packages
echo -e "${GREEN}[7/8] Restoring .NET packages...${NC}"
export DOTNET_CLI_TELEMETRY_OPTOUT=1
export DOTNET_NOLOGO=1
if dotnet restore src/Readarr.sln 2>&1 | grep -i "error"; then
    echo -e "${RED}  ✗ Package restore failed!${NC}"
    echo -e "${YELLOW}  Check output above for errors. You may need to manually update some packages.${NC}"
    exit 1
else
    echo -e "  ✓ Packages restored successfully"
fi
echo ""

# Build
echo -e "${GREEN}[8/8] Building with .NET 8...${NC}"
if dotnet build src/Readarr.sln --configuration Release 2>&1 | grep -i "Build FAILED"; then
    echo -e "${RED}  ✗ Build failed!${NC}"
    echo -e "${YELLOW}  Review the errors above and fix manually.${NC}"
    echo -e "${YELLOW}  Common issues:${NC}"
    echo -e "    - API changes in System.Text.Json"
    echo -e "    - Incompatible package versions"
    echo -e "    - Breaking changes in LINQ"
    exit 1
else
    echo -e "  ✓ Build successful!"
fi
echo ""

# Summary
echo -e "${BLUE}╔════════════════════════════════════════╗${NC}"
echo -e "${BLUE}║           Migration Complete!           ║${NC}"
echo -e "${BLUE}╚════════════════════════════════════════╝${NC}"
echo ""
echo -e "${GREEN}✓ Successfully migrated to .NET 8!${NC}"
echo ""
echo -e "${YELLOW}Next Steps:${NC}"
echo -e "  1. Review changes: ${BLUE}git diff develop${NC}"
echo -e "  2. Run tests: ${BLUE}dotnet test src/Readarr.sln${NC}"
echo -e "  3. Build frontend: ${BLUE}yarn run build --env production${NC}"
echo -e "  4. Test manually: ${BLUE}./_output/net8.0/linux-x64/publish/Readarr${NC}"
echo -e "  5. Commit changes: ${BLUE}git add -A && git commit -m 'feat: migrate to .NET 8'${NC}"
echo ""
echo -e "${YELLOW}Testing Checklist:${NC}"
echo -e "  [ ] Application starts"
echo -e "  [ ] Database migrations work"
echo -e "  [ ] Book import/search works"
echo -e "  [ ] API endpoints respond"
echo -e "  [ ] Frontend loads correctly"
echo -e "  [ ] Download clients work"
echo -e "  [ ] Calibre integration works"
echo ""
echo -e "${YELLOW}Documentation:${NC}"
echo -e "  See DOTNET8_MIGRATION.md for detailed information"
echo ""
echo -e "${GREEN}Happy testing! 🚀${NC}"
