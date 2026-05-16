#!/bin/bash

set -eo pipefail

API_URL="http://localhost:5020"
API_PROJECT="src/StepWise.Management"
EXAMPLE_URL="http://localhost:5010"
EXAMPLE_PROJECT="ExampleApi"
UI_TEST_PROJECT="tests/StepWise.Management.UI.Tests"

DB_CONTAINER="stepwise-management-db"
DB_PORT=5433
DB_NAME="stepwise_management"
DB_USER="postgres"
DB_PASS="postgres"

MGMT_DLL="src/StepWise.Management/bin/Debug/net10.0/StepWise.Management.dll"
EXAMPLE_DLL="ExampleApi/bin/Debug/net10.0/ExampleApi.dll"
TEST_DLL="tests/StepWise.Management.UI.Tests/bin/Debug/net10.0/StepWise.Management.UI.Tests.dll"

kill_apis() {
  pkill -f "project src/StepWise.Management" 2>/dev/null || true
  pkill -f "project ExampleApi" 2>/dev/null || true
}

apis_up() {
  curl -fs "$API_URL/api/ping" >/dev/null 2>&1 && \
  curl -fs "$EXAMPLE_URL/products" >/dev/null 2>&1
}

needs_rebuild() {
  local dll="$1" src_dir="$2"
  [ ! -f "$dll" ] && return 0
  [ -n "$(find "$src_dir" \( -name "*.cs" -o -name "*.csproj" \) -newer "$dll" -print -quit)" ]
}

# ── Database ──────────────────────────────────────────────────────────────────

ensure_db() {
  if docker ps --filter "name=^${DB_CONTAINER}$" --format '{{.Names}}' | grep -q "^${DB_CONTAINER}$"; then
    return
  fi

  if docker ps -a --filter "name=^${DB_CONTAINER}$" --format '{{.Names}}' | grep -q "^${DB_CONTAINER}$"; then
    echo "→ Starting existing database container..."
    docker start "$DB_CONTAINER"
  else
    echo "→ Creating database container..."
    docker run -d \
      --name "$DB_CONTAINER" \
      -e POSTGRES_DB="$DB_NAME" \
      -e POSTGRES_USER="$DB_USER" \
      -e POSTGRES_PASSWORD="$DB_PASS" \
      -p "${DB_PORT}:5432" \
      postgres:16
  fi

  echo -n "  Waiting for Postgres"
  for i in {1..30}; do
    if docker exec "$DB_CONTAINER" pg_isready -U "$DB_USER" -d "$DB_NAME" >/dev/null 2>&1; then
      echo " ready"
      return
    fi
    echo -n "."
    sleep 1
    if [ $i -eq 30 ]; then echo " timed out"; exit 1; fi
  done
}

run_migrations() {
  docker exec "$DB_CONTAINER" psql -U "$DB_USER" -d "$DB_NAME" -q -c "
    CREATE TABLE IF NOT EXISTS schema_migrations (
      version TEXT PRIMARY KEY,
      applied_at TIMESTAMPTZ NOT NULL DEFAULT now()
    );"

  local migrations_dir applied
  migrations_dir="$(dirname "$0")/src/StepWise.Management/Migrations"
  applied=$(docker exec "$DB_CONTAINER" psql -U "$DB_USER" -d "$DB_NAME" -t -c \
    "SELECT version FROM schema_migrations;" | tr -d '[:space:]')

  for sql_file in "$migrations_dir"/*.sql; do
    version=$(basename "$sql_file")
    if echo "$applied" | grep -qF "$version"; then
      echo "  ✓ $version"
    else
      echo "  → Applying $version..."
      docker exec -i "$DB_CONTAINER" psql -U "$DB_USER" -d "$DB_NAME" -q < "$sql_file"
      docker exec "$DB_CONTAINER" psql -U "$DB_USER" -d "$DB_NAME" -q -c \
        "INSERT INTO schema_migrations (version) VALUES ('$version');"
    fi
  done
}

wait_for() {
  local name="$1" url="$2"
  echo -n "  Waiting for $name"
  for i in {1..30}; do
    if curl -fs "$url" >/dev/null; then
      echo " ready"
      return 0
    fi
    echo -n "."
    sleep 1
    if [ $i -eq 30 ]; then echo " timed out"; return 1; fi
  done
}

# ── Build ─────────────────────────────────────────────────────────────────────

if needs_rebuild "$MGMT_DLL"    "$API_PROJECT" || \
   needs_rebuild "$EXAMPLE_DLL" "$EXAMPLE_PROJECT"; then
  echo "→ Building..."
  dotnet build StepWise.Management.sln -nologo -v q
  REBUILT=true
elif needs_rebuild "$TEST_DLL" "$UI_TEST_PROJECT"; then
  echo "→ Test sources changed — rebuilding tests only..."
  dotnet build "$UI_TEST_PROJECT" -nologo -v q
  REBUILT=false
else
  echo "→ No source changes — skipping build."
  REBUILT=false
fi

# ── Database + APIs ───────────────────────────────────────────────────────────

ensure_db

if apis_up && [ "$REBUILT" = false ]; then
  echo "→ APIs already running and up to date."
  echo "→ Running migrations..."
  run_migrations
else
  echo "→ Stopping any running APIs..."
  kill_apis

  echo "→ Resetting database..."
  docker exec "$DB_CONTAINER" psql -U "$DB_USER" -d postgres -q -c \
    "SELECT pg_terminate_backend(pid) FROM pg_stat_activity WHERE datname = '$DB_NAME' AND pid <> pg_backend_pid();"
  docker exec "$DB_CONTAINER" psql -U "$DB_USER" -d postgres -q -c "DROP DATABASE IF EXISTS $DB_NAME;"
  docker exec "$DB_CONTAINER" psql -U "$DB_USER" -d postgres -q -c "CREATE DATABASE $DB_NAME;"

  echo "→ Running migrations..."
  run_migrations

  echo "→ Starting APIs..."
  dotnet run --project "$API_PROJECT"     --no-build >test-api.log     2>&1 &
  dotnet run --project "$EXAMPLE_PROJECT" --no-build --urls "$EXAMPLE_URL" >test-example.log 2>&1 &

  wait_for "API"         "$API_URL/api/ping"    &
  W1=$!
  wait_for "Example API" "$EXAMPLE_URL/products" &
  W2=$!
  wait $W1 $W2
fi

# ── Tests ─────────────────────────────────────────────────────────────────────

echo "→ Running tests..."
set +e
dotnet test "$UI_TEST_PROJECT" --no-build --filter "FullyQualifiedName~.Api."
TEST_EXIT=$?
set -e

exit $TEST_EXIT
