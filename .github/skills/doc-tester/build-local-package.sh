#!/bin/bash
#
# build-local-package.sh
#
# Builds a local development NuGet package of Hex1b for documentation testing.
# This allows code examples from documentation to be tested against the current
# source code rather than published packages.
#
# Usage:
#   ./build-local-package.sh          # Build local package
#   ./build-local-package.sh --clean  # Remove local packages only
#   ./build-local-package.sh --help   # Show usage
#

set -e

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

# Find repository root (where apphost.cs lives)
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../../.." && pwd)"

# Validate we're in the right place
if [[ ! -f "$REPO_ROOT/apphost.cs" ]]; then
    echo -e "${RED}Error: Cannot find repository root (apphost.cs not found)${NC}"
    echo "Script directory: $SCRIPT_DIR"
    echo "Expected repo root: $REPO_ROOT"
    exit 1
fi

# Configuration
PACKAGE_OUTPUT_DIR="$REPO_ROOT/.doc-tester-packages"
PROJECT_PATH="$REPO_ROOT/src/Hex1b/Hex1b.csproj"
TIMESTAMP=$(date +%Y%m%d%H%M%S)
VERSION_SUFFIX="local.$TIMESTAMP"

# Parse arguments
case "${1:-}" in
    --clean)
        echo "Cleaning local packages..."
        if [[ -d "$PACKAGE_OUTPUT_DIR" ]]; then
            rm -rf "$PACKAGE_OUTPUT_DIR"
            echo -e "${GREEN}✓ Removed $PACKAGE_OUTPUT_DIR${NC}"
        else
            echo "No local packages to clean."
        fi
        exit 0
        ;;
    --help|-h)
        echo "Usage: $0 [--clean|--help]"
        echo ""
        echo "Builds a local NuGet package of Hex1b for documentation testing."
        echo ""
        echo "Options:"
        echo "  --clean    Remove local packages directory"
        echo "  --help     Show this help message"
        echo ""
        echo "Output:"
        echo "  .doc-tester-packages/Hex1b.<version>.nupkg"
        echo "  .doc-tester-packages/NuGet.config"
        exit 0
        ;;
esac

echo "Building local Hex1b package..."
echo "Repository root: $REPO_ROOT"

# Clean previous packages
if [[ -d "$PACKAGE_OUTPUT_DIR" ]]; then
    echo "Cleaning previous packages..."
    rm -rf "$PACKAGE_OUTPUT_DIR"
fi

# Create output directory
mkdir -p "$PACKAGE_OUTPUT_DIR"

# Get the base version from the project (or use a default)
# Try to extract from any existing package references or use a reasonable default
BASE_VERSION=$(grep -oP '(?<=<Version>)[^<]+' "$PROJECT_PATH" 2>/dev/null || echo "")
if [[ -z "$BASE_VERSION" ]]; then
    # Check Directory.Build.props for version
    if [[ -f "$REPO_ROOT/Directory.Build.props" ]]; then
        BASE_VERSION=$(grep -oP '(?<=<Version>)[^<]+' "$REPO_ROOT/Directory.Build.props" 2>/dev/null || echo "")
    fi
fi
if [[ -z "$BASE_VERSION" ]]; then
    # Default version if not found
    BASE_VERSION="0.0.0"
fi

FULL_VERSION="${BASE_VERSION}-${VERSION_SUFFIX}"

echo "Version: $FULL_VERSION"

# Build and pack
echo "Running dotnet pack..."
dotnet pack "$PROJECT_PATH" \
    --configuration Release \
    --output "$PACKAGE_OUTPUT_DIR" \
    -p:VersionSuffix="$VERSION_SUFFIX" \
    --no-restore 2>/dev/null || \
dotnet pack "$PROJECT_PATH" \
    --configuration Release \
    --output "$PACKAGE_OUTPUT_DIR" \
    -p:VersionSuffix="$VERSION_SUFFIX"

# Find the generated package
PACKAGE_FILE=$(find "$PACKAGE_OUTPUT_DIR" -name "Hex1b.*.nupkg" -type f | head -1)

if [[ -z "$PACKAGE_FILE" ]]; then
    echo -e "${RED}Error: Package was not created${NC}"
    exit 1
fi

# Extract actual version from package filename
ACTUAL_VERSION=$(basename "$PACKAGE_FILE" | sed 's/Hex1b\.\(.*\)\.nupkg/\1/')

# Create NuGet.config for test projects
# Use absolute path so the config works when copied to any test project directory
cat > "$PACKAGE_OUTPUT_DIR/NuGet.config" << EOF
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="doc-tester-local" value="$PACKAGE_OUTPUT_DIR" />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
  </packageSources>
  <packageSourceMapping>
    <packageSource key="doc-tester-local">
      <package pattern="Hex1b" />
    </packageSource>
    <packageSource key="nuget.org">
      <package pattern="*" />
    </packageSource>
  </packageSourceMapping>
</configuration>
EOF

# Output summary
echo ""
echo -e "${GREEN}✓ Built Hex1b ${ACTUAL_VERSION}${NC}"
echo -e "${GREEN}✓ Package: ${PACKAGE_FILE}${NC}"
echo -e "${GREEN}✓ NuGet config: ${PACKAGE_OUTPUT_DIR}/NuGet.config${NC}"
echo ""
echo -e "${YELLOW}To use in a test project:${NC}"
echo "  1. Copy NuGet.config to your test project directory"
echo "  2. Run: dotnet add package Hex1b --version ${ACTUAL_VERSION}"
echo ""
echo -e "${YELLOW}Or set NUGET_PACKAGES path:${NC}"
echo "  export NUGET_PACKAGES=$PACKAGE_OUTPUT_DIR"
