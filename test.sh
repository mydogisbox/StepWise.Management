#!/bin/bash

set -eo pipefail

API_URL="http://localhost:5000"
API_PROJECT="src/StepWise.Management"
EXAMPLE_URL="http://localhost:5010"
EXAMPLE_PROJECT="ExampleApi"
TEST_PROJECT="tests/StepWise.Management.Tests"

DB_CONTAINER="stepwise-management-db"
DB_PORT=5433
DB_NAME="stepwise_management"
DB_USER="postgres"
DB_PASS="postgres"

kill_apis() {
  pkill -f "project src/StepWise.Management" 2>/dev/null || true
  pkill -f "project ExampleApi" 2>/dev/null || true
}

# ── Database ─────────────────────────────────────────────────────────────────

ensure_db() {
  if docker ps --filter "name=^${DB_CONTAINER}$" --format '{{.Names}}' | grep -q "^${DB_CONTAINER}$"; then
    echo "→ Database already running."
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
    if [ $i -eq 30 ]; then
      echo " timed out"
      exit 1
    fi
  done
}

ensure_db

# ── Migrations ────────────────────────────────────────────────────────────────

run_migrations() {
  docker exec "$DB_CONTAINER" psql -U "$DB_USER" -d "$DB_NAME" -q -c "
    CREATE TABLE IF NOT EXISTS schema_migrations (
      version TEXT PRIMARY KEY,
      applied_at TIMESTAMPTZ NOT NULL DEFAULT now()
    );"

  local migrations_dir
  migrations_dir="$(dirname "$0")/src/StepWise.Management/Migrations"

  for sql_file in "$migrations_dir"/*.sql; do
    version=$(basename "$sql_file")
    already_applied=$(docker exec "$DB_CONTAINER" psql -U "$DB_USER" -d "$DB_NAME" -t -c \
      "SELECT 1 FROM schema_migrations WHERE version = '$version';" | tr -d '[:space:]')

    if [ "$already_applied" = "1" ]; then
      echo "  ✓ $version (already applied)"
    else
      echo "  → Applying $version..."
      docker exec -i "$DB_CONTAINER" psql -U "$DB_USER" -d "$DB_NAME" -q < "$sql_file"
      docker exec "$DB_CONTAINER" psql -U "$DB_USER" -d "$DB_NAME" -q -c \
        "INSERT INTO schema_migrations (version) VALUES ('$version');"
    fi
  done
}

# ── Reset ─────────────────────────────────────────────────────────────────────

echo "→ Stopping any running APIs..."
kill_apis

echo "→ Resetting database..."
docker exec "$DB_CONTAINER" psql -U "$DB_USER" -d postgres -q -c \
  "SELECT pg_terminate_backend(pid) FROM pg_stat_activity WHERE datname = '$DB_NAME' AND pid <> pg_backend_pid();"
docker exec "$DB_CONTAINER" psql -U "$DB_USER" -d postgres -q -c "DROP DATABASE IF EXISTS $DB_NAME;"
docker exec "$DB_CONTAINER" psql -U "$DB_USER" -d postgres -q -c "CREATE DATABASE $DB_NAME;"

# ── Migrations ────────────────────────────────────────────────────────────────

echo "→ Running migrations..."
run_migrations

# ── API ───────────────────────────────────────────────────────────────────────

echo "→ Starting API..."
dotnet run --project "$API_PROJECT" &

echo -n "  Waiting for API"
for i in {1..30}; do
  if curl -fs "$API_URL/api/ping" >/dev/null; then
    echo " ready"
    break
  fi
  echo -n "."
  sleep 1
  if [ $i -eq 30 ]; then
    echo " timed out"
    exit 1
  fi
done

echo "→ Starting Example API..."
dotnet run --project "$EXAMPLE_PROJECT" --urls "$EXAMPLE_URL" &

echo -n "  Waiting for Example API"
for i in {1..30}; do
  if curl -fs "$EXAMPLE_URL/products" >/dev/null; then
    echo " ready"
    break
  fi
  echo -n "."
  sleep 1
  if [ $i -eq 30 ]; then
    echo " timed out"
    exit 1
  fi
done

# ── Tests ─────────────────────────────────────────────────────────────────────

echo "→ Running tests..."
set +e
dotnet test "$TEST_PROJECT" #--logger "console;verbosity=normal"
TEST_EXIT=$?
set -e

echo "→ Stopping APIs..."
kill_apis

exit $TEST_EXIT
