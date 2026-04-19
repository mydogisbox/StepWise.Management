CREATE TABLE IF NOT EXISTS catalog_summaries (
    id         TEXT        PRIMARY KEY,
    name       TEXT        NOT NULL,
    step_count INT         NOT NULL DEFAULT 0,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now()
);

-- Helper table for accurate step count tracking
CREATE TABLE IF NOT EXISTS catalog_steps (
    catalog_id TEXT NOT NULL,
    step_name  TEXT NOT NULL,
    PRIMARY KEY (catalog_id, step_name)
);

CREATE TABLE IF NOT EXISTS workflow_summaries (
    id          TEXT        PRIMARY KEY,
    name        TEXT        NOT NULL,
    catalog_ids JSONB       NOT NULL DEFAULT '[]',
    archived    BOOLEAN     NOT NULL DEFAULT false,
    created_at  TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at  TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE TABLE IF NOT EXISTS test_run_summaries (
    id            TEXT        PRIMARY KEY,
    workflow_id   TEXT        NOT NULL,
    workflow_name TEXT        NOT NULL,
    passed        BOOLEAN     NOT NULL,
    started_at    TIMESTAMPTZ NOT NULL,
    duration_ms   BIGINT      NOT NULL
);

CREATE INDEX IF NOT EXISTS idx_test_run_summaries_workflow_id ON test_run_summaries (workflow_id);
CREATE INDEX IF NOT EXISTS idx_test_run_summaries_started_at ON test_run_summaries (started_at DESC);
