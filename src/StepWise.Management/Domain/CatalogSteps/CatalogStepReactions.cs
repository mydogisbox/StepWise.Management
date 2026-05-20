using System.Text.Json;
using CommandFramework.Core;
using Dapper;
using Npgsql;
using static StepWise.Management.Domain.CatalogSteps.CatalogStepEvent;

namespace StepWise.Management.Domain.CatalogSteps;

public static class CatalogStepReactions
{
    public static readonly IReadOnlyList<EventReaction> All =
    [
        EventReaction.On<CatalogStepUpserted>(async (e, tx) =>
        {
            var conn = ((NpgsqlTransaction)tx).Connection!;
            var defaultsJson = e.Defaults.HasValue ? e.Defaults.Value.GetRawText() : null;
            var requestShapeJson = e.RequestShape.HasValue ? e.RequestShape.Value.GetRawText() : null;
            var responseShapeJson = e.ResponseShape.HasValue ? e.ResponseShape.Value.GetRawText() : null;
            await conn.ExecuteAsync(@"
                INSERT INTO catalog_step_summaries
                    (id, catalog_id, target_id, step_name, method, path, defaults, is_archived,
                     request_shape, response_shape, is_polling, retry_count, retry_duration_ms)
                VALUES
                    (@id, @catalogId, @targetId, @stepName, @method, @path, @defaults::jsonb, false,
                     @requestShape::jsonb, @responseShape::jsonb, @isPolling, @retryCount, @retryDurationMs)
                ON CONFLICT (id) DO UPDATE SET
                    catalog_id        = EXCLUDED.catalog_id,
                    target_id         = EXCLUDED.target_id,
                    step_name         = EXCLUDED.step_name,
                    method            = EXCLUDED.method,
                    path              = EXCLUDED.path,
                    defaults          = EXCLUDED.defaults,
                    request_shape     = EXCLUDED.request_shape,
                    response_shape    = EXCLUDED.response_shape,
                    is_polling        = EXCLUDED.is_polling,
                    retry_count       = EXCLUDED.retry_count,
                    retry_duration_ms = EXCLUDED.retry_duration_ms",
                new
                {
                    id = e.Id, catalogId = e.CatalogId, targetId = e.TargetId, stepName = e.StepName,
                    method = e.Method, path = e.Path, defaults = defaultsJson,
                    requestShape = requestShapeJson, responseShape = responseShapeJson,
                    isPolling = e.IsPolling, retryCount = e.RetryCount, retryDurationMs = e.RetryDurationMs
                },
                tx);
        }),

        EventReaction.On<CatalogStepArchived>(async (e, tx) =>
        {
            var conn = ((NpgsqlTransaction)tx).Connection!;
            await conn.ExecuteAsync(
                "UPDATE catalog_step_summaries SET is_archived = true WHERE id = @id",
                new { id = e.Id },
                tx);
        }),

        EventReaction.On<CatalogStepUnarchived>(async (e, tx) =>
        {
            var conn = ((NpgsqlTransaction)tx).Connection!;
            await conn.ExecuteAsync(
                "UPDATE catalog_step_summaries SET is_archived = false WHERE id = @id",
                new { id = e.Id },
                tx);
        })
    ];
}
