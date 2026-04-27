using CommandFramework.Core;
using Dapper;
using Npgsql;

namespace StepWise.Management.Domain.Workflows;

public static class WorkflowReactions
{
    public static readonly IReadOnlyList<EventReaction> All =
    [
        EventReaction.On<WorkflowCreated>(async (e, tx) =>
        {
            var conn = ((NpgsqlTransaction)tx).Connection!;
            await conn.ExecuteAsync(@"
                INSERT INTO workflow_summaries (id, name, archived)
                VALUES (@id, @name, false)
                ON CONFLICT (id) DO NOTHING",
                new { id = e.Id, name = e.Name },
                tx);
        }),

        EventReaction.On<WorkflowRenamed>(async (e, tx) =>
        {
            var conn = ((NpgsqlTransaction)tx).Connection!;
            await conn.ExecuteAsync(
                "UPDATE workflow_summaries SET name = @name, updated_at = now() WHERE id = @id",
                new { id = e.Id, name = e.Name },
                tx);
        }),

        EventReaction.On<WorkflowArchived>(async (e, tx) =>
        {
            var conn = ((NpgsqlTransaction)tx).Connection!;
            await conn.ExecuteAsync(
                "UPDATE workflow_summaries SET archived = true, updated_at = now() WHERE id = @id",
                new { id = e.Id },
                tx);
        }),

        EventReaction.On<WorkflowUnarchived>(async (e, tx) =>
        {
            var conn = ((NpgsqlTransaction)tx).Connection!;
            await conn.ExecuteAsync(
                "UPDATE workflow_summaries SET archived = false, updated_at = now() WHERE id = @id",
                new { id = e.Id },
                tx);
        }),

        EventReaction.On<WorkflowDescriptionUpdated>(async (e, tx) =>
        {
            var conn = ((NpgsqlTransaction)tx).Connection!;
            await conn.ExecuteAsync(
                "UPDATE workflow_summaries SET description = @description, updated_at = now() WHERE id = @id",
                new { id = e.Id, description = e.Description },
                tx);
        })
    ];
}
