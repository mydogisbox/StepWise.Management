using CommandFramework.Core;
using Dapper;
using Npgsql;

namespace StepWise.Management.Domain.TestRuns;

public static class TestRunReactions
{
    public static readonly IReadOnlyList<EventReaction> All =
    [
        EventReaction.On<RunRecorded>(async (e, tx) =>
        {
            var conn = ((NpgsqlTransaction)tx).Connection!;
            await conn.ExecuteAsync(@"
                INSERT INTO test_run_summaries (id, workflow_id, workflow_name, passed, started_at, duration_ms)
                VALUES (@id, @workflowId, @workflowName, @passed, @startedAt, @durationMs)
                ON CONFLICT (id) DO NOTHING",
                new
                {
                    id = e.Id,
                    workflowId = e.WorkflowId,
                    workflowName = e.WorkflowName,
                    passed = e.Passed,
                    startedAt = e.StartedAt,
                    durationMs = e.DurationMs
                },
                tx);
        })
    ];
}
