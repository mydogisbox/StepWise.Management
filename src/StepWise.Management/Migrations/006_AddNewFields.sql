ALTER TABLE catalog_summaries ADD COLUMN IF NOT EXISTS description TEXT NOT NULL DEFAULT '';

ALTER TABLE workflow_summaries ADD COLUMN IF NOT EXISTS description TEXT NOT NULL DEFAULT '';

ALTER TABLE catalog_step_summaries ADD COLUMN IF NOT EXISTS request_shape   JSONB;
ALTER TABLE catalog_step_summaries ADD COLUMN IF NOT EXISTS response_shape  JSONB;
ALTER TABLE catalog_step_summaries ADD COLUMN IF NOT EXISTS is_polling      BOOLEAN NOT NULL DEFAULT false;
ALTER TABLE catalog_step_summaries ADD COLUMN IF NOT EXISTS retry_count     INT;
ALTER TABLE catalog_step_summaries ADD COLUMN IF NOT EXISTS retry_duration_ms INT;
