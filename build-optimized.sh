#!/bin/bash
# Optimized build script based on AUR PKGBUILD best practices
set -e

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

echo -e "${GREEN}Building Bookshelf (Readarr fork) with AUR optimizations${NC}"

# Dotnet telemetry opt-out and optimization flags
export DOTNET_CLI_TELEMETRY_OPTOUT=1
export DOTNET_NOLOGO=1
export DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1

# Architecture detection
case $(uname -m) in
  x86_64) CARCH='x64' ;;
  aarch64) CARCH='arm64' ;;
  armv7l) CARCH='arm' ;;
  *) echo -e "${RED}Unsupported architecture: $(uname -m)${NC}"; exit 1 ;;
esac

FRAMEWORK='net6.0'
RUNTIME="linux-${CARCH}"
OUTPUT="_output"
ARTIFACTS="${OUTPUT}/${FRAMEWORK}/${RUNTIME}/publish"

echo -e "${YELLOW}Configuration:${NC}"
echo "  Framework: ${FRAMEWORK}"
echo "  Runtime: ${RUNTIME}"
echo "  Architecture: ${CARCH}"
echo ""

# Clean previous build
if [ -d "${OUTPUT}" ]; then
  echo -e "${YELLOW}Cleaning previous build...${NC}"
  rm -rf "${OUTPUT}"
fi

# Step 1: Restore dependencies
echo -e "${GREEN}[1/4] Restoring .NET dependencies...${NC}"
dotnet restore src/Readarr.sln \
  --runtime ${RUNTIME} \
  --locked-mode

# Step 2: Install frontend dependencies
echo -e "${GREEN}[2/4] Installing frontend dependencies...${NC}"
yarn install --frozen-lockfile --network-timeout 120000

# Step 3: Build backend
echo -e "${GREEN}[3/4] Building backend...${NC}"
dotnet build src/Readarr.sln \
  --framework ${FRAMEWORK} \
  --runtime ${RUNTIME} \
  --no-self-contained \
  --no-restore \
  --configuration Release \
  -p:Platform=Posix \
  -p:RuntimeIdentifiers=${RUNTIME} \
  -t:PublishAllRids

# Post-build cleanup (AUR optimization)
echo -e "${YELLOW}Removing Windows-specific and service helper files...${NC}"
if [ -d "${ARTIFACTS}" ]; then
  rm -f "${ARTIFACTS}/ServiceInstall"*
  rm -f "${ARTIFACTS}/ServiceUninstall"*
  rm -f "${ARTIFACTS}/Readarr.Windows."*
  echo "  - Removed ServiceInstall/ServiceUninstall"
  echo "  - Removed Readarr.Windows.*"
fi

# Step 4: Build frontend
echo -e "${GREEN}[4/4] Building frontend...${NC}"
yarn run build --env production

echo ""
echo -e "${GREEN}✓ Build complete!${NC}"
echo ""
echo -e "${YELLOW}Build artifacts:${NC}"
echo "  Backend: ${ARTIFACTS}/"
echo "  Frontend: ${OUTPUT}/UI/"
echo ""
echo -e "${YELLOW}To disable built-in updater (recommended for package management):${NC}"
echo "  cp package_info ${ARTIFACTS}/"
echo ""
echo -e "${YELLOW}Optional: Strip debug symbols to reduce size:${NC}"
echo "  find ${ARTIFACTS} -name '*.dll' -exec strip --strip-unneeded {} \\;"
echo "  find ${ARTIFACTS} -name '*.so' -exec strip --strip-unneeded {} \\;"
