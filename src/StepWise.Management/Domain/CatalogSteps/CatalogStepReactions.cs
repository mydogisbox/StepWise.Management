using System.Text.Json;
using CommandFramework.Core;
using Dapper;
using Npgsql;

namespace StepWise.Management.Domain.CatalogSteps;

public static class CatalogStepReactions
{
    public static readonly IReadOnlyList<EventReaction> All =
    [
        EventReaction.On<CatalogStepUpserted>(async (e, tx) =>
        {
            if (string.IsNullOrEmpty(e.Id)) return;
            var conn = ((NpgsqlTransaction)tx).Connection!;
            var defaultsJson = e.Defaults.HasValue ? e.Defaults.Value.GetRawText() : null;
            await conn.ExecuteAsync(@"
                INSERT INTO catalog_step_summaries (id, catalog_id, target_id, step_name, method, path, defaults, is_archived)
                VALUES (@id, @catalogId, @targetId, @stepName, @method, @path, @defaults::jsonb, false)
                ON CONFLICT (id) DO UPDATE SET
                    catalog_id = EXCLUDED.catalog_id,
                    target_id  = EXCLUDED.target_id,
                    step_name  = EXCLUDED.step_name,
                    method     = EXCLUDED.method,
                    path       = EXCLUDED.path,
                    defaults   = EXCLUDED.defaults",
                new { id = e.Id, catalogId = e.CatalogId, targetId = e.TargetId, stepName = e.StepName, method = e.Method, path = e.Path, defaults = defaultsJson },
                tx);
        }),

        EventReaction.On<CatalogStepArchived>(async (e, tx) =>
        {
            if (string.IsNullOrEmpty(e.Id)) return;
            var conn = ((NpgsqlTransaction)tx).Connection!;
            await conn.ExecuteAsync(
                "UPDATE catalog_step_summaries SET is_archived = true WHERE id = @id",
                new { id = e.Id },
                tx);
        })
    ];
}
