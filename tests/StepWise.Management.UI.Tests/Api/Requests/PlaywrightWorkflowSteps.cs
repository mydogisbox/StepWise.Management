using Microsoft.Playwright;
using Walkthrough.Core;
using Walkthrough.Http;

namespace StepWise.Management.UI.Tests.Api;

// Navigates a paged UI list until the predicate is satisfied, waiting for the pager text
// to confirm each page transition before calling the predicate again.
// Leaves the browser on the page where the predicate first returned true.
// Returns true if found, false if the last page was reached without satisfaction.
internal static class PagedUiHelper
{
    internal static async Task<bool> NavigateToPageWhereAsync(IPage page, string pagerId, Func<Task<bool>> predicate)
    {
        int currentPage = 1;
        while (true)
        {
            if (await predicate()) return true;
            var nextBtn = await page.QuerySelectorAsync($"#{pagerId} button:last-child");
            if (nextBtn == null || await nextBtn.IsDisabledAsync()) return false;
            currentPage++;
            await nextBtn.ClickAsync();
            await page.WaitForFunctionAsync(
                $"document.querySelector('#{pagerId}')?.textContent?.includes('Page {currentPage} of')");
        }
    }
}

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

        var showArchived = resolvedFields.TryGetValue("ShowArchived", out var sa) && sa?.ToString() == "true";
        if (showArchived)
            await page.CheckAsync("#workflows-show-archived");

        await page.WaitForFunctionAsync("document.querySelector('#workflow-list').textContent.trim().length > 0");

        if (await page.QuerySelectorAsync("#workflow-list table") == null)
            return new PagedResponse<WorkflowSummaryResponse>([], 0, 1, 10, 0);

        var nameFilter = resolvedFields.TryGetValue("Name", out var nf) ? nf?.ToString() : null;
        List<WorkflowSummaryResponse> results = [];

        await PagedUiHelper.NavigateToPageWhereAsync(page, "pager-workflows", async () =>
        {
            var rows = await page.QuerySelectorAllAsync("#workflow-list table tbody tr");
            results = [];
            foreach (var row in rows)
            {
                var nameEl = await row.QuerySelectorAsync("td span.font-medium");
                if (nameEl == null) continue;
                var name = (await nameEl.InnerTextAsync()).Trim();
                var archivedEl = await row.QuerySelectorAsync("td span.text-yellow-700");
                results.Add(new WorkflowSummaryResponse("", name, archivedEl != null));
            }
            return string.IsNullOrEmpty(nameFilter) || results.Any(r => r.Name == nameFilter);
        });

        var filtered = string.IsNullOrEmpty(nameFilter)
            ? results
            : results.Where(r => r.Name == nameFilter).ToList();

        return new PagedResponse<WorkflowSummaryResponse>(filtered.ToArray(), filtered.Count, 1, filtered.Count, 1);
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

public class PlaywrightListTargetsStep : PlaywrightStep<ListTargetsRequest>
{
    public override async Task<object?> ExecuteAsync(IPage page, Dictionary<string, object?> resolvedFields, WorkflowContext context)
    {
        await page.GotoAsync("http://localhost:5020/index.html");
        await page.GetByRole(AriaRole.Button, new() { Name = "Targets" }).ClickAsync();

        var showArchived = resolvedFields.TryGetValue("ShowArchived", out var sa) && sa?.ToString() == "true";
        if (showArchived)
            await page.CheckAsync("#targets-show-archived");

        await page.WaitForFunctionAsync("document.querySelector('#target-list').textContent.trim().length > 0");

        if (await page.QuerySelectorAsync("#target-list table") == null)
            return Array.Empty<TargetResponse>();

        var rows = await page.QuerySelectorAllAsync("#target-list table tbody tr");
        var results = new List<TargetResponse>();
        foreach (var row in rows)
        {
            var nameEl = await row.QuerySelectorAsync("td span.font-medium");
            if (nameEl == null) continue;
            var name = (await nameEl.InnerTextAsync()).Trim();
            var archivedEl = await row.QuerySelectorAsync("td span.text-yellow-700");
            var createdAtEl = await row.QuerySelectorAsync("td:nth-child(3)");
            var createdAtText = createdAtEl != null ? (await createdAtEl.InnerTextAsync()).Trim() : null;
            results.Add(new TargetResponse("", name, "", archivedEl != null, createdAtText == "—" ? null : createdAtText));
        }
        return results.ToArray();
    }
}

public class PlaywrightListRunsStep : PlaywrightStep<ListRunsRequest>
{
    public override async Task<object?> ExecuteAsync(IPage page, Dictionary<string, object?> resolvedFields, WorkflowContext context)
    {
        await page.GotoAsync("http://localhost:5020/index.html");
        await page.GetByRole(AriaRole.Button, new() { Name = "Runs" }).ClickAsync();

        await page.WaitForFunctionAsync("document.querySelector('#runs-list').textContent.trim().length > 0");

        if (await page.QuerySelectorAsync("#runs-list table") == null)
            return new PagedResponse<RunSummaryResponse>([], 0, 1, 10, 0);

        var workflowNameFilter = resolvedFields.TryGetValue("WorkflowName", out var wn) ? wn?.ToString() : null;
        List<RunSummaryResponse> results = [];

        await PagedUiHelper.NavigateToPageWhereAsync(page, "pager-runs", async () =>
        {
            var rows = await page.QuerySelectorAllAsync("#runs-list table tbody tr");
            results = [];
            foreach (var row in rows)
            {
                var nameEl = await row.QuerySelectorAsync("td span.font-medium");
                if (nameEl == null) continue;
                var workflowName = (await nameEl.InnerTextAsync()).Trim();
                var badgeEl = await row.QuerySelectorAsync("td:nth-child(2) span");
                bool? passed = null;
                if (badgeEl != null)
                {
                    var badgeText = (await badgeEl.InnerTextAsync()).Trim();
                    if (badgeText == "PASS") passed = true;
                    else if (badgeText == "FAIL") passed = false;
                }
                results.Add(new RunSummaryResponse("", "", workflowName, passed, null));
            }
            return string.IsNullOrEmpty(workflowNameFilter) || results.Any(r => r.WorkflowName == workflowNameFilter);
        });

        var filtered = string.IsNullOrEmpty(workflowNameFilter)
            ? results
            : results.Where(r => r.WorkflowName == workflowNameFilter).ToList();

        return new PagedResponse<RunSummaryResponse>(filtered.ToArray(), filtered.Count, 1, filtered.Count, 1);
    }
}
