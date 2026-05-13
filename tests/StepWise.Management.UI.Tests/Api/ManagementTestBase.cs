using Walkthrough.Core;
using Walkthrough.Http;

namespace StepWise.Management.UI.Tests.Api;

public abstract class ManagementTestBase
{
    private readonly WorkflowRunner _runner;

    protected ManagementTestBase()
    {
        var target = new HttpTarget("http://localhost:5020")
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

        _runner = new WorkflowRunner(new WorkflowContext(), _ => target);
    }

    protected Task<TResponse> ExecuteAsync<TResponse>(HttpWorkflowRequest<TResponse> request)
        => _runner.ExecuteAsync(request);

    protected Task<TResponse> BuildAsync<TResponse>(BuildableRequest<TResponse> item)
        => _runner.BuildAsync(item);

    protected Task<object> ExecuteRawAsync<TResponse>(HttpWorkflowRequest<TResponse> request)
        => _runner.ExecuteRawAsync(request);

    protected Task<TResponse> PollAsync<TResponse>(
        HttpWorkflowRequest<TResponse> request,
        Func<TResponse, bool> until,
        int intervalMs = 200,
        int timeoutMs  = 10000)
        => _runner.PollAsync(request, until, intervalMs, timeoutMs);
}
