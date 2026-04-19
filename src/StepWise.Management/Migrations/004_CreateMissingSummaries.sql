ALTER TABLE catalog_summaries ADD COLUMN IF NOT EXISTS is_archived BOOLEAN NOT NULL DEFAULT false;

CREATE TABLE IF NOT EXISTS target_summaries (
    id          TEXT    PRIMARY KEY,
    name        TEXT    NOT NULL,
    base_url    TEXT    NOT NULL,
    is_archived BOOLEAN NOT NULL DEFAULT false
);

CREATE TABLE IF NOT EXISTS catalog_step_summaries (
    id          TEXT    PRIMARY KEY,
    catalog_id  TEXT    NOT NULL,
    target_id   TEXT    NOT NULL,
    step_name   TEXT    NOT NULL,
    method      TEXT    NOT NULL,
    path        TEXT    NOT NULL,
    defaults    JSONB,
    is_archived BOOLEAN NOT NULL DEFAULT false
);

CREATE INDEX IF NOT EXISTS idx_catalog_step_summaries_catalog_id ON catalog_step_summaries (catalog_id);
