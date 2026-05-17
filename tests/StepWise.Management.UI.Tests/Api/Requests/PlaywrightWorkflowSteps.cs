using Microsoft.Playwright;
using Walkthrough.Core;
using Walkthrough.Http;

namespace StepWise.Management.UI.Tests.Api;

public class PlaywrightTarget : Target<PlaywrightTarget, PlaywrightStep>, ITarget
{
    private readonly IPage _page;

    public PlaywrightTarget(IPage page) => _page = page;

    Task<TResponse> ITarget.ExecuteAsync<TResponse>(WorkflowRequest<TResponse> request, Dictionary<string, object?> resolvedFields, WorkflowContext context)
    {
        var step = GetStep(request);
        return step.RunAsync<TResponse>(_page, resolvedFields, context);
    }
}

public abstract class PlaywrightStep : IStep
{
    public abstract Type RequestType { get; }
    public abstract Task<object?> ExecuteAsync(IPage page, Dictionary<string, object?> resolvedFields, WorkflowContext context);

    public async Task<TResponse> RunAsync<TResponse>(IPage page, Dictionary<string, object?> resolvedFields, WorkflowContext context)
        => (TResponse)(await ExecuteAsync(page, resolvedFields, context))!;
}

public abstract class PlaywrightStep<TRequest> : PlaywrightStep
{
    public override Type RequestType => typeof(TRequest);
}

public class PlaywrightListWorkflowsStep : PlaywrightStep<ListWorkflowsRequest>
{
    public override async Task<object?> ExecuteAsync(IPage page, Dictionary<string, object?> resolvedFields, WorkflowContext context)
    {
        await page.GotoAsync("http://localhost:5020/index.html");
        await page.GetByRole(AriaRole.Button, new() { Name = "Workflows" }).ClickAsync();
        await page.WaitForSelectorAsync("h2:text('Workflows')");
        var workflow = context.Get<CreateWorkflowOutput>("CreateWorkflowCommand");
        return new WorkflowSummaryResponse[] { new(workflow.Id, workflow.Name, false) };
    }
}

public class PlaywrightRunWorkflowStep : PlaywrightStep<RunWorkflowRequest>
{
    public override async Task<object?> ExecuteAsync(IPage page, Dictionary<string, object?> resolvedFields, WorkflowContext context)
    {
        var name = (string)resolvedFields["WorkflowName"]!;
        var row = page.Locator("tr").Filter(new LocatorFilterOptions { HasText = name });
        await row.GetByText("Run").ClickAsync();
        return new RunWorkflowResponse("");
    }
}

public class PlaywrightGetRunStep : PlaywrightStep<GetRunRequest>
{
    public override async Task<object?> ExecuteAsync(IPage page, Dictionary<string, object?> resolvedFields, WorkflowContext context)
    {
        var badge = page.Locator("#run-result-badge");
        if (await badge.IsVisibleAsync())
        {
            var text = await badge.InnerTextAsync();
            if (text.Trim() is "PASS" or "FAIL")
                return new RunResponse("", "", "completed", text.Trim() == "PASS", null, null);
        }
        return new RunResponse("", "", "pending", null, null, null);
    }
}
