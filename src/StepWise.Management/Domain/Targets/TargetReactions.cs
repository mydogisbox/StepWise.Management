using CommandFramework.Core;
using Dapper;
using Npgsql;

namespace StepWise.Management.Domain.Targets;

public static class TargetReactions
{
    public static readonly IReadOnlyList<EventReaction> All =
    [
        EventReaction.On<TargetCreated>(async (e, tx) =>
        {
            var conn = ((NpgsqlTransaction)tx).Connection!;
            await conn.ExecuteAsync(@"
                INSERT INTO target_summaries (id, name, base_url, is_archived)
                VALUES (@id, @name, @baseUrl, false)
                ON CONFLICT (id) DO NOTHING",
                new { id = e.Id, name = e.Name, baseUrl = e.BaseUrl },
                tx);
        }),

        EventReaction.On<TargetUpdated>(async (e, tx) =>
        {
            var conn = ((NpgsqlTransaction)tx).Connection!;
            await conn.ExecuteAsync(
                "UPDATE target_summaries SET name = @name, base_url = @baseUrl WHERE id = @id",
                new { id = e.Id, name = e.Name, baseUrl = e.BaseUrl },
                tx);
        }),

        EventReaction.On<TargetArchived>(async (e, tx) =>
        {
            var conn = ((NpgsqlTransaction)tx).Connection!;
            await conn.ExecuteAsync(
                "UPDATE target_summaries SET is_archived = true WHERE id = @id",
                new { id = e.Id },
                tx);
        }),

        EventReaction.On<TargetUnarchived>(async (e, tx) =>
        {
            var conn = ((NpgsqlTransaction)tx).Connection!;
            await conn.ExecuteAsync(
                "UPDATE target_summaries SET is_archived = false WHERE id = @id",
                new { id = e.Id },
                tx);
        })
    ];
}
