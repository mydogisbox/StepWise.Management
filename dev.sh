#!/bin/bash
# Start the DB, run migrations, then launch the API.
# Open http://localhost:5000 after it starts.

set -eo pipefail

DB_CONTAINER="stepwise-management-db"
DB_PORT=5433
DB_NAME="stepwise_management"
DB_USER="postgres"
DB_PASS="postgres"

# ── Database ──────────────────────────────────────────────────────────────────

if docker ps --filter "name=^${DB_CONTAINER}$" --format '{{.Names}}' | grep -q "^${DB_CONTAINER}$"; then
  echo "→ Database already running."
elif docker ps -a --filter "name=^${DB_CONTAINER}$" --format '{{.Names}}' | grep -q "^${DB_CONTAINER}$"; then
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
  echo -n "  Waiting for Postgres"
  for i in {1..30}; do
    if docker exec "$DB_CONTAINER" pg_isready -U "$DB_USER" -d "$DB_NAME" >/dev/null 2>&1; then echo " ready"; break; fi
    echo -n "."; sleep 1
    [ $i -eq 30 ] && echo " timed out" && exit 1
  done
fi

# ── Migrations ────────────────────────────────────────────────────────────────

echo "→ Running migrations..."
docker exec "$DB_CONTAINER" psql -U "$DB_USER" -d "$DB_NAME" -q -c "
  CREATE TABLE IF NOT EXISTS schema_migrations (
    version TEXT PRIMARY KEY,
    applied_at TIMESTAMPTZ NOT NULL DEFAULT now()
  );" 2>/dev/null

for sql_file in "$(dirname "$0")/src/StepWise.Management/Migrations"/*.sql; do
  version=$(basename "$sql_file")
  applied=$(docker exec "$DB_CONTAINER" psql -U "$DB_USER" -d "$DB_NAME" -t -c \
    "SELECT 1 FROM schema_migrations WHERE version = '$version';" | tr -d '[:space:]')
  if [ "$applied" = "1" ]; then
    echo "  ✓ $version"
  else
    echo "  → Applying $version..."
    docker exec -i "$DB_CONTAINER" psql -U "$DB_USER" -d "$DB_NAME" -q < "$sql_file"
    docker exec "$DB_CONTAINER" psql -U "$DB_USER" -d "$DB_NAME" -q -c \
      "INSERT INTO schema_migrations (version) VALUES ('$version');"
  fi
done

# ── API ───────────────────────────────────────────────────────────────────────

PID_FILE="/tmp/stepwise-management-api.pid"
API_URL="http://localhost:5000"

if [ -f "$PID_FILE" ]; then
  OLD_PID=$(cat "$PID_FILE")
  if kill -0 "$OLD_PID" 2>/dev/null; then
    echo "→ Stopping previous API instance (pid $OLD_PID)..."
    kill "$OLD_PID"
    sleep 1
  fi
  rm "$PID_FILE"
fi

echo "→ Starting API..."
dotnet run --project src/StepWise.Management &
API_PID=$!
echo $API_PID > "$PID_FILE"

echo -n "  Waiting for API"
for i in {1..30}; do
  if curl -fs "$API_URL/catalogs" >/dev/null 2>&1; then
    echo " ready"
    break
  fi
  echo -n "."
  sleep 1
  if [ $i -eq 30 ]; then
    echo " timed out"
    kill $API_PID
    exit 1
  fi
done

# ── Example API ───────────────────────────────────────────────────────────────

EXAMPLE_PID_FILE="/tmp/stepwise-example-api.pid"
EXAMPLE_URL="http://localhost:5010"

if [ -f "$EXAMPLE_PID_FILE" ]; then
  OLD_PID=$(cat "$EXAMPLE_PID_FILE")
  if kill -0 "$OLD_PID" 2>/dev/null; then
    echo "→ Stopping previous Example API instance (pid $OLD_PID)..."
    kill "$OLD_PID"
    sleep 1
  fi
  rm "$EXAMPLE_PID_FILE"
fi

echo "→ Starting Example API..."
dotnet run --project ExampleApi --urls "$EXAMPLE_URL" &
EXAMPLE_PID=$!
echo $EXAMPLE_PID > "$EXAMPLE_PID_FILE"

echo -n "  Waiting for Example API"
for i in {1..30}; do
  if curl -fs "$EXAMPLE_URL/products" >/dev/null 2>&1; then
    echo " ready"
    break
  fi
  echo -n "."
  sleep 1
  if [ $i -eq 30 ]; then
    echo " timed out"
    kill $EXAMPLE_PID
    exit 1
  fi
done

echo "  Management: http://localhost:5000"
echo "  Example API: http://localhost:5010"
wait $API_PID
