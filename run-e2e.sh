#!/usr/bin/env bash
set -euo pipefail

cd "$(dirname "$(readlink -f "${BASH_SOURCE[0]}")")"

COMPOSE_FILE="docker-compose.e2e.yml"
RESULTS_DIR="test-results"
BUILD=true

log() { printf '\033[0;32m[INFO]\033[0m %s\n' "$1"; }
fail() { printf '\033[0;31m[ERROR]\033[0m %s\n' "$1" >&2; }

compose() { podman-compose -f "$COMPOSE_FILE" "$@"; }

for arg in "$@"; do
    case "$arg" in
        --no-build) BUILD=false ;;
        --clean)
            log "Removing E2E containers, networks and volumes..."
            compose down -v --remove-orphans || true
            exit 0
            ;;
        *)
            fail "Unknown argument: $arg (supported: --no-build, --clean)"
            exit 2
            ;;
    esac
done

cleanup() {
    mkdir -p "$RESULTS_DIR"
    compose logs myblog-web > "$RESULTS_DIR/myblog-web.log" 2>&1 || true
    compose logs myblog-e2e > "$RESULTS_DIR/myblog-e2e.log" 2>&1 || true
    compose down -v --remove-orphans > /dev/null 2>&1 || true
}

compose down -v --remove-orphans > /dev/null 2>&1 || true
trap cleanup EXIT

if [[ "$BUILD" == true ]]; then
    log "Building images (layer cache makes unchanged builds fast)..."
    compose build
else
    log "Skipping build (--no-build); using existing images"
fi

log "Starting MyBlog..."
compose up -d myblog-web

for attempt in $(seq 1 60); do
    if podman exec myblog-web curl -sf http://localhost:5000/ > /dev/null 2>&1; then
        log "MyBlog is ready"
        break
    fi
    if [[ "$attempt" -eq 60 ]]; then
        fail "MyBlog failed to start within 120 seconds"
        compose logs myblog-web || true
        exit 1
    fi
    sleep 2
done

log "Running E2E tests..."
compose up --no-deps myblog-e2e || true

E2E_EXIT_CODE="$(podman inspect --format '{{.State.ExitCode}}' myblog-e2e 2>/dev/null || echo 1)"

if [[ "$E2E_EXIT_CODE" -eq 0 ]]; then
    log "E2E tests passed"
else
    fail "E2E tests failed with exit code $E2E_EXIT_CODE (logs in $RESULTS_DIR/)"
fi

exit "$E2E_EXIT_CODE"
