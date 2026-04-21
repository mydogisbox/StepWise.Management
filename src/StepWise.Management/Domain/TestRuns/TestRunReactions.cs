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
            var payload = JsonSerializer.Serialize(new { runId = e.Id, workflowId = e.WorkflowId }, JsonConfig.Options);
            await conn.ExecuteAsync(
                "INSERT INTO outbox (event_type, payload) VALUES (@type, @payload::jsonb)",
                new { type = nameof(RunTriggered), payload },
                tx);
        })
    ];
}
