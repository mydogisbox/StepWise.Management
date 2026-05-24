using Walkthrough.Core;
using Walkthrough.Http;

namespace StepWise.Management.UI.Tests.Api;

public abstract class ManagementTestBase
{
    private WorkflowRunner? _runner;
    protected WorkflowRunner Runner => _runner ??= BuildRunner();

    protected readonly ITarget ApiTarget = new HttpTarget("http://localhost:5020")
        .Register<PostTargetCommandsStep>()
        .Register<GetTargetStep>()
        .Register<ListTargetsStep>()
        .Register<PostCatalogCommandsStep>()
        .Register<GetCatalogStep>()
        .Register<ListCatalogsStep>()
        .Register<PostCatalogStepCommandsStep>()
        .Register<GetCatalogStepStep>()
        .Register<ListCatalogStepsStep>()
        .Register<PostWorkflowCommandsStep>()
        .Register<GetWorkflowStep>()
        .Register<ListWorkflowsStep>()
        .Register<RunWorkflowStep>()
        .Register<GetRunStep>()
        .Register<ListRunsStep>();

    protected virtual WorkflowRunner BuildRunner() =>
        new WorkflowRunner(new WorkflowContext(), ApiTarget);

    protected Task<TResponse> ExecuteAsync<TResponse, TSelf>(WorkflowRequest<TResponse, TSelf> request)
        where TSelf : WorkflowRequest<TResponse, TSelf>, IWorkflowRequest
        => Runner.ExecuteAsync(request);

    protected Task<TResponse> BuildAsync<TResponse>(BuildableRequest<TResponse> item)
        => Runner.BuildAsync(item);

    protected Task<object> ExecuteRawAsync<TResponse, TSelf>(WorkflowRequest<TResponse, TSelf> request)
        where TSelf : WorkflowRequest<TResponse, TSelf>, IWorkflowRequest
        => Runner.ExecuteRawAsync(request);

    protected Task<TResponse> PollAsync<TResponse, TSelf>(
        WorkflowRequest<TResponse, TSelf> request,
        Func<TResponse, bool> until,
        int intervalMs = 200,
        int timeoutMs  = 10000)
        where TSelf : WorkflowRequest<TResponse, TSelf>, IWorkflowRequest
        => Runner.PollAsync(request, until, intervalMs, timeoutMs);

    protected static RunStepResult GetStep(RunResult result, string stepName)
        => result.Steps.Single(s => s.StepName == stepName);
}
