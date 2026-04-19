# StepWise.Management

A persistent web backend for managing StepWise API workflow definitions, catalogs, and test runs.

## Database Setup

### Option A: Docker (recommended)

Start a Postgres container:

```bash
docker run -d \
  --name stepwise-postgres \
  -e POSTGRES_DB=stepwise_management \
  -e POSTGRES_USER=postgres \
  -e POSTGRES_PASSWORD=postgres \
  -p 5433:5432 \
  postgres:latest
```

Wait a few seconds for the container to initialize, then run the migrations inside the container:

```bash
docker exec -i stepwise-postgres psql -U postgres stepwise_management \
  < src/StepWise.Management/Migrations/001_CreateEvents.sql
docker exec -i stepwise-postgres psql -U postgres stepwise_management \
  < src/StepWise.Management/Migrations/002_CreateOutbox.sql
docker exec -i stepwise-postgres psql -U postgres stepwise_management \
  < src/StepWise.Management/Migrations/003_CreateReadModels.sql
```

Stop/start the container later:

```bash
docker stop stepwise-postgres
docker start stepwise-postgres
```

### Option B: Local Postgres

```bash
createdb stepwise_management
psql stepwise_management < src/StepWise.Management/Migrations/001_CreateEvents.sql
psql stepwise_management < src/StepWise.Management/Migrations/002_CreateOutbox.sql
psql stepwise_management < src/StepWise.Management/Migrations/003_CreateReadModels.sql
```

Connection string (configured in `appsettings.json`):
```
Host=localhost;Database=stepwise_management;Username=postgres;Password=postgres
```

## Running

```bash
dotnet run --project src/StepWise.Management
```

Then open http://localhost:5000 in your browser.

## API

### Aggregate Commands

- `POST /catalogs/commands` — Create catalogs, upsert/remove steps
- `GET /catalogs/{id}` — Get catalog state
- `POST /workflows/commands` — Create/edit workflows
- `GET /workflows/{id}` — Get workflow state
- `POST /runs/commands` — Record test runs
- `GET /runs/{id}` — Get test run result

### Read Endpoints

- `GET /api/catalogs` — List all catalogs
- `GET /api/workflows` — List all workflows
- `GET /api/runs` — List latest 100 test runs

### Run a Workflow

```
POST /api/workflows/{id}/run
Content-Type: application/json

{ "targetOverrides": { "api": "http://staging.example.com" } }
```
