#!/bin/bash

set -eo pipefail

API_URL="http://localhost:5000"
API_PROJECT="src/StepWise.Management"
EXAMPLE_URL="http://localhost:3001"
EXAMPLE_PROJECT="ExampleApi"
TEST_PROJECT="tests/StepWise.Management.Tests"
PID_FILE="/tmp/stepwise-management-api.pid"
EXAMPLE_PID_FILE="/tmp/stepwise-example-api.pid"

DB_CONTAINER="stepwise-management-db"
DB_PORT=5433
DB_NAME="stepwise_management"
DB_USER="postgres"
DB_PASS="postgres"

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
  # Ensure migration tracking table exists
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

echo "→ Running migrations..."
run_migrations

# ── API ───────────────────────────────────────────────────────────────────────

# Kill any previously started API instance tracked by this script
if [ -f "$PID_FILE" ]; then
  OLD_PID=$(cat "$PID_FILE")
  if kill -0 "$OLD_PID" 2>/dev/null; then
    echo "→ Stopping previous API instance (pid $OLD_PID)..."
    kill "$OLD_PID"
    sleep 1
  fi
  rm "$PID_FILE"
fi

# Start the management API fresh
echo "→ Starting API (latest build)..."
dotnet run --project "$API_PROJECT" &
API_PID=$!
echo $API_PID > "$PID_FILE"

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
    kill $API_PID
    rm "$PID_FILE"
    exit 1
  fi
done

# Kill any previously started Example API instance
if [ -f "$EXAMPLE_PID_FILE" ]; then
  OLD_PID=$(cat "$EXAMPLE_PID_FILE")
  if kill -0 "$OLD_PID" 2>/dev/null; then
    echo "→ Stopping previous Example API instance (pid $OLD_PID)..."
    kill "$OLD_PID"
    sleep 1
  fi
  rm "$EXAMPLE_PID_FILE"
fi

# Start the Example API
echo "→ Starting Example API..."
dotnet run --project "$EXAMPLE_PROJECT" &
EXAMPLE_PID=$!
echo $EXAMPLE_PID > "$EXAMPLE_PID_FILE"

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
    kill $EXAMPLE_PID
    rm "$EXAMPLE_PID_FILE"
    kill $(cat "$PID_FILE")
    rm "$PID_FILE"
    exit 1
  fi
done

# ── Tests ─────────────────────────────────────────────────────────────────────

echo "→ Running tests..."
set +e
dotnet test "$TEST_PROJECT" #--logger "console;verbosity=normal"
TEST_EXIT=$?
set -e

# Shut down both APIs
echo "→ Stopping APIs..."
kill $(cat "$PID_FILE") && rm "$PID_FILE"
kill $(cat "$EXAMPLE_PID_FILE") && rm "$EXAMPLE_PID_FILE"

exit $TEST_EXIT
