#!/usr/bin/env bash
set -euo pipefail

pkill -f "project src/StepWise.Management" 2>/dev/null && echo "→ Stopped StepWise.Management" || echo "→ StepWise.Management was not running"
pkill -f "project ExampleApi"              2>/dev/null && echo "→ Stopped ExampleApi"           || echo "→ ExampleApi was not running"

rm -f .api-started
