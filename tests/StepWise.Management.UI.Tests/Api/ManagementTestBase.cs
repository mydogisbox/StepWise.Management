using Walkthrough.Core;
using Walkthrough.Http;

namespace StepWise.Management.UI.Tests.Api;

public abstract class ManagementTestBase
{
    private WorkflowRunner? _runner;
    private WorkflowRunner Runner => _runner ??= BuildRunner();

    protected readonly ITarget ApiTarget = new HttpTarget("http://localhost:5020")
        .Register(new PostTargetCommandsStep())
        .Register(new GetTargetStep())
        .Register(new ListTargetsStep())
        .Register(new PostCatalogCommandsStep())
        .Register(new GetCatalogStep())
        .Register(new ListCatalogsStep())
        .Register(new PostCatalogStepCommandsStep())
        .Register(new GetCatalogStepStep())
        .Register(new ListCatalogStepsStep())
        .Register(new PostWorkflowCommandsStep())
        .Register(new GetWorkflowStep())
        .Register(new ListWorkflowsStep())
        .Register(new RunWorkflowStep())
        .Register(new GetRunStep())
        .Register(new ListRunsStep());

    protected virtual WorkflowRunner BuildRunner() =>
        new WorkflowRunner(new WorkflowContext(), ApiTarget);

    protected Task<TResponse> ExecuteAsync<TResponse>(HttpWorkflowRequest<TResponse> request)
        => Runner.ExecuteAsync(request);

    protected Task<TResponse> BuildAsync<TResponse>(BuildableRequest<TResponse> item)
        => Runner.BuildAsync(item);

    protected Task<object> ExecuteRawAsync<TResponse>(HttpWorkflowRequest<TResponse> request)
        => Runner.ExecuteRawAsync(request);

    protected Task<TResponse> PollAsync<TResponse>(
        HttpWorkflowRequest<TResponse> request,
        Func<TResponse, bool> until,
        int intervalMs = 200,
        int timeoutMs  = 10000)
        => Runner.PollAsync(request, until, intervalMs, timeoutMs);
}
