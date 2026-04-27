using System.Text.Json;
using CommandFramework.Core;
using Dapper;
using Npgsql;

namespace StepWise.Management.Domain.TestRuns;

public static class WorkflowRunReactions
{
    public static readonly IReadOnlyList<EventReaction> All =
    [
        EventReaction.On<RunTriggered>(async (e, tx) =>
        {
            var conn = ((NpgsqlTransaction)tx).Connection!;

            var outboxPayload = JsonSerializer.Serialize(new { runId = e.Id, workflowId = e.WorkflowId }, JsonConfig.Options);
            await conn.ExecuteAsync(
                "INSERT INTO outbox (event_type, payload) VALUES (@type, @payload::jsonb)",
                new { type = nameof(RunTriggered), payload = outboxPayload },
                tx);

            var workflowName = await conn.QueryFirstOrDefaultAsync<string>(
                "SELECT name FROM workflow_summaries WHERE id = @id",
                new { id = e.WorkflowId },
                tx) ?? "";

            await conn.ExecuteAsync(@"
                INSERT INTO test_run_summaries (id, workflow_id, workflow_name, started_at)
                VALUES (@id, @workflowId, @workflowName, @startedAt)
                ON CONFLICT (id) DO NOTHING",
                new { id = e.Id, workflowId = e.WorkflowId, workflowName, startedAt = e.TriggeredAt },
                tx);
        }),

        EventReaction.On<RunCompleted>(async (e, tx) =>
        {
            var conn = ((NpgsqlTransaction)tx).Connection!;
            await conn.ExecuteAsync(@"
                UPDATE test_run_summaries SET passed = @passed, duration_ms = @durationMs WHERE id = @id",
                new { id = e.Id, passed = e.Passed, durationMs = e.DurationMs },
                tx);
        }),

        EventReaction.On<RunFailed>(async (e, tx) =>
        {
            var conn = ((NpgsqlTransaction)tx).Connection!;
            await conn.ExecuteAsync(@"
                UPDATE test_run_summaries SET passed = false, duration_ms = @durationMs WHERE id = @id",
                new { id = e.Id, durationMs = e.DurationMs },
                tx);
        })
    ];
}
