using CommandFramework.Core;
using Dapper;
using Npgsql;

namespace StepWise.Management.Domain.Catalogs;

public static class CatalogReactions
{
    public static readonly IReadOnlyList<EventReaction> All =
    [
        EventReaction.On<CatalogCreated>(async (e, tx) =>
        {
            if (string.IsNullOrEmpty(e.Id)) return;
            var conn = ((NpgsqlTransaction)tx).Connection!;
            await conn.ExecuteAsync(@"
                INSERT INTO catalog_summaries (id, name, is_archived)
                VALUES (@id, @name, false)
                ON CONFLICT (id) DO NOTHING",
                new { id = e.Id, name = e.Name },
                tx);
        }),

        EventReaction.On<CatalogArchived>(async (e, tx) =>
        {
            if (string.IsNullOrEmpty(e.Id)) return;
            var conn = ((NpgsqlTransaction)tx).Connection!;
            await conn.ExecuteAsync(
                "UPDATE catalog_summaries SET is_archived = true WHERE id = @id",
                new { id = e.Id },
                tx);
        })
    ];
}
