CREATE TABLE IF NOT EXISTS outbox (
    id          BIGSERIAL   PRIMARY KEY,
    event_type  TEXT        NOT NULL,
    payload     JSONB       NOT NULL,
    created_at  TIMESTAMPTZ NOT NULL DEFAULT now(),
    processed_at TIMESTAMPTZ
);

CREATE INDEX IF NOT EXISTS idx_outbox_processed_at ON outbox (processed_at) WHERE processed_at IS NULL;
