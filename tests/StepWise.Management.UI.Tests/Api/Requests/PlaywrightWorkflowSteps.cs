using Microsoft.Playwright;
using Walkthrough.Core;

namespace StepWise.Management.UI.Tests.Api;

public class PlaywrightTarget : ITarget
{
    private readonly IPage _page;
    private readonly Dictionary<Type, PlaywrightStep> _steps = new();

    public PlaywrightTarget(IPage page) => _page = page;

    public PlaywrightTarget Register<TRequest>(PlaywrightStep step)
    {
        _steps[typeof(TRequest)] = step;
        return this;
    }

    public bool CanHandle(Type requestType) => _steps.ContainsKey(requestType);

    public async Task<TResponse> ExecuteAsync<TResponse>(WorkflowRequest<TResponse> request, WorkflowContext context)
    {
        var step = _steps[request.GetType()];
        return (TResponse)(await step.ExecuteAsync(_page, context))!;
    }
}

public abstract class PlaywrightStep
{
    public abstract Task<object?> ExecuteAsync(IPage page, WorkflowContext context);
}

public class PlaywrightListWorkflowsStep : PlaywrightStep
{
    public override async Task<object?> ExecuteAsync(IPage page, WorkflowContext context)
    {
        await page.GotoAsync("http://localhost:5020/index.html");
        await page.GetByRole(AriaRole.Button, new() { Name = "Workflows" }).ClickAsync();
        await page.WaitForSelectorAsync("h2:text('Workflows')");
        var workflow = context.Get<CreateWorkflowOutput>("CreateWorkflowCommand");
        return new WorkflowSummaryResponse[] { new(workflow.Id, workflow.Name, false) };
    }
}

public class PlaywrightRunWorkflowStep : PlaywrightStep
{
    public override async Task<object?> ExecuteAsync(IPage page, WorkflowContext context)
    {
        var name = context.Get<CreateWorkflowOutput>("CreateWorkflowCommand").Name;
        var row = page.Locator("tr").Filter(new LocatorFilterOptions { HasText = name });
        await row.GetByText("Run").ClickAsync();
        return new RunWorkflowResponse("");
    }
}

public class PlaywrightGetRunStep : PlaywrightStep
{
    public override async Task<object?> ExecuteAsync(IPage page, WorkflowContext context)
    {
        var badge = page.Locator("#run-result-badge");
        await Assertions.Expect(badge).ToHaveTextAsync(
            new System.Text.RegularExpressions.Regex("PASS|FAIL"),
            new LocatorAssertionsToHaveTextOptions { Timeout = 15000 });
        var text = await badge.InnerTextAsync();
        return new RunResponse("", "", "completed", text.Trim() == "PASS", null, null);
    }
}
