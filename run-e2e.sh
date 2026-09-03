#!/bin/bash
# =============================================================================
# Run E2E Tests with Podman Compose
# =============================================================================
# This script runs Playwright E2E tests against MyBlog using containers.
# Designed for Fedora with SELinux and Podman.
#
# Usage:
#   ./run-e2e.sh              # Run E2E tests (builds only if images missing)
#   ./run-e2e.sh --build      # Force rebuild containers before running
#   ./run-e2e.sh --no-build   # Skip build entirely (use existing images)
#   ./run-e2e.sh --clean      # Clean up and remove volumes
# =============================================================================

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$SCRIPT_DIR"

COMPOSE_FILE="docker-compose.e2e.yml"

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

log_info() {
    echo -e "${GREEN}[INFO]${NC} $1"
}

log_warn() {
    echo -e "${YELLOW}[WARN]${NC} $1"
}

log_error() {
    echo -e "${RED}[ERROR]${NC} $1"
}

# Parse arguments
BUILD_FLAG=""
NO_BUILD_FLAG=""
CLEAN_FLAG=""

for arg in "$@"; do
    case $arg in
        --build)
            BUILD_FLAG="true"
            ;;
        --no-build)
            NO_BUILD_FLAG="true"
            ;;
        --clean)
            CLEAN_FLAG="true"
            ;;
    esac
done

# Clean up if requested
if [ "$CLEAN_FLAG" == "true" ]; then
    log_info "Cleaning up containers and volumes..."
    podman-compose -f "$COMPOSE_FILE" down -v --remove-orphans 2>/dev/null || true
    podman system prune -f 2>/dev/null || true
    log_info "Cleanup complete"
    exit 0
fi

# Create test results directory
mkdir -p test-results
# Set SELinux context if on Fedora with SELinux
if command -v chcon &> /dev/null && getenforce 2>/dev/null | grep -q "Enforcing"; then
    log_info "Setting SELinux context for test-results directory..."
    chcon -Rt svirt_sandbox_file_t test-results 2>/dev/null || true
fi

log_info "Starting E2E test environment..."

# -------------------------------------------------------------------------
# Ensure clean container state before starting.
# This prevents crun "exec.fifo: No such file or directory" errors caused
# by stale container runtime state from a previous run.
# -------------------------------------------------------------------------
log_info "Removing any leftover containers from previous runs..."
podman-compose -f "$COMPOSE_FILE" down --remove-orphans 2>/dev/null || true

# -------------------------------------------------------------------------
# Build logic:
#   --build      → always rebuild
#   --no-build   → never rebuild (fail fast if images missing)
#   (default)    → rebuild only if images are missing
# -------------------------------------------------------------------------
images_exist() {
    podman image exists localhost/myblog_myblog-web:latest 2>/dev/null && \
    podman image exists localhost/myblog_myblog-e2e:latest 2>/dev/null
}

if [ "$BUILD_FLAG" == "true" ]; then
    log_info "Force rebuilding containers..."
    podman-compose -f "$COMPOSE_FILE" build
elif [ "$NO_BUILD_FLAG" == "true" ]; then
    if ! images_exist; then
        log_error "Container images not found. Run without --no-build first, or use --build."
        exit 1
    fi
    log_info "Using existing container images (--no-build)..."
elif ! images_exist; then
    log_info "Container images not found. Building..."
    podman-compose -f "$COMPOSE_FILE" build
else
    log_info "Using cached container images (pass --build to force rebuild)..."
fi

log_info "Starting MyBlog web service..."
podman-compose -f "$COMPOSE_FILE" up -d myblog-web

# Wait for web service to be healthy
log_info "Waiting for MyBlog to be ready..."
RETRIES=30
until podman exec myblog-web curl -sf http://localhost:5000/ > /dev/null 2>&1; do
    RETRIES=$((RETRIES - 1))
    if [ $RETRIES -eq 0 ]; then
        log_error "MyBlog failed to start within timeout"
        podman-compose -f "$COMPOSE_FILE" logs myblog-web
        podman-compose -f "$COMPOSE_FILE" down -v
        exit 1
    fi
    echo -n "."
    sleep 2
done
echo ""
log_info "MyBlog is ready!"

# Run E2E tests
log_info "Running E2E tests..."
podman-compose -f "$COMPOSE_FILE" up myblog-e2e
E2E_EXIT_CODE=$?

# Capture logs
log_info "Capturing logs..."
podman-compose -f "$COMPOSE_FILE" logs myblog-web > test-results/myblog-web.log 2>&1
podman-compose -f "$COMPOSE_FILE" logs myblog-e2e > test-results/myblog-e2e.log 2>&1

# Clean up
log_info "Cleaning up..."
podman-compose -f "$COMPOSE_FILE" down -v

if [ $E2E_EXIT_CODE -eq 0 ]; then
    log_info "E2E tests passed! ✓"
else
    log_error "E2E tests failed with exit code $E2E_EXIT_CODE"
    log_info "Check test-results/ directory for logs"
fi

exit $E2E_EXIT_CODE
