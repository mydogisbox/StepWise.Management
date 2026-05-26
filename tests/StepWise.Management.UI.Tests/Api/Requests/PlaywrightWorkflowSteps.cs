using System.Text.RegularExpressions;
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
            var nextBtn = page.Locator($"#{pagerId} button:last-child");
            if (await nextBtn.CountAsync() == 0 || await nextBtn.IsDisabledAsync()) return false;
            currentPage++;
            await nextBtn.ClickAsync();
            await page.WaitForFunctionAsync(
                $"document.querySelector('#{pagerId}')?.textContent?.includes('Page {currentPage} of')");
        }
    }

    internal static PagerInfo ParsePagerInfo(string text)
    {
        var m = Regex.Match(text, @"Page (\d+) of (\d+)");
        return m.Success
            ? new PagerInfo(int.Parse(m.Groups[1].Value), int.Parse(m.Groups[2].Value))
            : new PagerInfo(1, 1);
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

public class PlaywrightListCatalogsStep : PlaywrightStep<ListCatalogsRequest>
{
    public override async Task<object?> ExecuteAsync(IPage page, Dictionary<string, object?> resolvedFields, WorkflowContext context)
    {

        await page.GetByRole(AriaRole.Button, new() { Name = "Catalogs" }).ClickAsync();
        await page.WaitForSelectorAsync("h2:text('Catalogs')");

        var nameFilter = resolvedFields.TryGetValue("Name", out var nf) ? nf?.ToString() : null;

        if (!string.IsNullOrEmpty(nameFilter))
        {
            var filterDone = page.WaitForResponseAsync(r => r.Url.Contains("name=") && r.Request.Method == "GET");
            await page.FillAsync("#catalogs-name-filter", nameFilter);
            await filterDone;
        }
        else
        {
            await page.WaitForFunctionAsync("document.querySelector('#catalog-list').textContent.trim().length > 0");
        }

        if (await page.QuerySelectorAsync("#catalog-list table") == null)
            return new PagedResponse<CatalogResponse>([], 0, 1, 10, 0);

        List<CatalogResponse> results = [];

        await PagedUiHelper.NavigateToPageWhereAsync(page, "pager-catalogs", async () =>
        {
            var rows = await page.QuerySelectorAllAsync("#catalog-list table tbody tr");
            results = [];
            foreach (var row in rows)
            {
                var nameEl = await row.QuerySelectorAsync("td span.font-medium");
                if (nameEl == null) continue;
                var name = (await nameEl.InnerTextAsync()).Trim();
                var descEl = await row.QuerySelectorAsync("td:nth-child(2) span");
                var desc = descEl != null ? (await descEl.InnerTextAsync()).Trim() : null;
                results.Add(new CatalogResponse("", name, desc == "—" ? null : desc, false, null));
            }
            return string.IsNullOrEmpty(nameFilter) || results.Any(r => r.Name == nameFilter);
        });

        var filtered = string.IsNullOrEmpty(nameFilter)
            ? results
            : results.Where(r => r.Name == nameFilter).ToList();

        return new PagedResponse<CatalogResponse>(filtered.ToArray(), filtered.Count, 1, filtered.Count, 1);
    }
}

public class PlaywrightListWorkflowsStep : PlaywrightStep<ListWorkflowsRequest>
{
    public override async Task<object?> ExecuteAsync(IPage page, Dictionary<string, object?> resolvedFields, WorkflowContext context)
    {

        await page.GetByRole(AriaRole.Button, new() { Name = "Workflows" }).ClickAsync();
        await page.WaitForSelectorAsync("h2:text('Workflows')");

        var showArchived = resolvedFields.TryGetValue("ShowArchived", out var sa) && sa?.ToString() == "true";
        if (showArchived)
            await page.CheckAsync("#workflows-show-archived");

        var nameFilter = resolvedFields.TryGetValue("Name", out var nf) ? nf?.ToString() : null;

        if (!string.IsNullOrEmpty(nameFilter))
        {
            var filterDone = page.WaitForResponseAsync(r => r.Url.Contains("name=") && r.Request.Method == "GET");
            await page.FillAsync("#workflows-name-filter", nameFilter);
            await filterDone;
        }
        else
        {
            await page.WaitForFunctionAsync("document.querySelector('#workflow-list').textContent.trim().length > 0");
        }

        if (await page.QuerySelectorAsync("#workflow-list table") == null)
            return new PagedResponse<WorkflowSummaryResponse>([], 0, 1, 10, 0);

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
                var runCountEl = await row.QuerySelectorAsync("td:nth-child(3)");
                var passRateEl = await row.QuerySelectorAsync("td:nth-child(4)");
                var runCountText = runCountEl != null ? (await runCountEl.InnerTextAsync()).Trim() : null;
                var passRateText = passRateEl != null ? (await passRateEl.InnerTextAsync()).Trim() : null;
                int? runCount = int.TryParse(runCountText, out var rc) ? rc : null;
                results.Add(new WorkflowSummaryResponse("", name, archivedEl != null, runCount,
                    passRateText is null or "—" ? null : passRateText));
            }
            return string.IsNullOrEmpty(nameFilter) || results.Any(r => r.Name == nameFilter);
        });

        var filtered = string.IsNullOrEmpty(nameFilter)
            ? results
            : results.Where(r => r.Name == nameFilter).ToList();

        return new PagedResponse<WorkflowSummaryResponse>(filtered.ToArray(), filtered.Count, 1, filtered.Count, 1);
    }
}

public class PlaywrightListTargetsStep : PlaywrightStep<ListTargetsRequest>
{
    public override async Task<object?> ExecuteAsync(IPage page, Dictionary<string, object?> resolvedFields, WorkflowContext context)
    {

        await page.GetByRole(AriaRole.Button, new() { Name = "Targets" }).ClickAsync();

        var showArchived = resolvedFields.TryGetValue("ShowArchived", out var sa) && sa?.ToString() == "true";
        if (showArchived)
            await page.CheckAsync("#targets-show-archived");

        var nameFilter = resolvedFields.TryGetValue("Name", out var nf) ? nf?.ToString() : null;

        if (!string.IsNullOrEmpty(nameFilter))
        {
            var filterDone = page.WaitForResponseAsync(r => r.Url.Contains("name=") && r.Request.Method == "GET");
            await page.FillAsync("#targets-name-filter", nameFilter);
            await filterDone;
        }
        else
        {
            await page.WaitForFunctionAsync("document.querySelector('#target-list').textContent.trim().length > 0");
        }

        if (await page.QuerySelectorAsync("#target-list table") == null)
            return new PagedResponse<TargetResponse>([], 0, 1, 10, 0);

        List<TargetResponse> results = [];

        await PagedUiHelper.NavigateToPageWhereAsync(page, "pager-targets", async () =>
        {
            var rows = await page.QuerySelectorAllAsync("#target-list table tbody tr");
            results = [];
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
            return string.IsNullOrEmpty(nameFilter) || results.Any(r => r.Name == nameFilter);
        });

        var filtered = string.IsNullOrEmpty(nameFilter)
            ? results
            : results.Where(r => r.Name == nameFilter).ToList();

        return new PagedResponse<TargetResponse>(filtered.ToArray(), filtered.Count, 1, filtered.Count, 1);
    }
}


public class PlaywrightListRunsStep : PlaywrightStep<ListRunsRequest>
{
    public override async Task<object?> ExecuteAsync(IPage page, Dictionary<string, object?> resolvedFields, WorkflowContext context)
    {

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

public class PlaywrightOpenCatalogDetailStep : PlaywrightStep<OpenCatalogDetailRequest>
{
    public override async Task<object?> ExecuteAsync(IPage page, Dictionary<string, object?> resolvedFields, WorkflowContext context)
    {
        var name = (string)resolvedFields["Name"]!;

        var filterDone = page.WaitForResponseAsync(r => r.Url.Contains("name=") && r.Request.Method == "GET");
        await page.FillAsync("#catalogs-name-filter", name);
        await filterDone;
        await page.Locator("#catalog-list tr").Filter(new LocatorFilterOptions { HasText = name }).ClickAsync();
        await page.WaitForFunctionAsync("!document.querySelector('#catalog-detail').classList.contains('hidden')");
        return new UiActionResponse();
    }
}

public class PlaywrightOpenWorkflowDetailStep : PlaywrightStep<OpenWorkflowDetailRequest>
{
    public override async Task<object?> ExecuteAsync(IPage page, Dictionary<string, object?> resolvedFields, WorkflowContext context)
    {
        var name = (string)resolvedFields["Name"]!;

        var filterDone = page.WaitForResponseAsync(r => r.Url.Contains("name=") && r.Request.Method == "GET");
        await page.FillAsync("#workflows-name-filter", name);
        await filterDone;
        await page.Locator("#workflow-list tr").Filter(new LocatorFilterOptions { HasText = name }).GetByText("Edit").ClickAsync();
        await page.WaitForFunctionAsync("!document.querySelector('#workflow-detail').classList.contains('hidden')");
        return new UiActionResponse();
    }
}

public class PlaywrightArchiveCatalogStep : PlaywrightStep<ArchiveCatalogViaUiRequest>
{
    public override async Task<object?> ExecuteAsync(IPage page, Dictionary<string, object?> resolvedFields, WorkflowContext context)
    {
        await page.ClickAsync("#catalog-archive-btn");
        await page.WaitForFunctionAsync("document.querySelector('#catalog-archive-btn')?.textContent?.trim() === 'Unarchive'");
        return new UiActionResponse();
    }
}

public class PlaywrightArchiveWorkflowStep : PlaywrightStep<ArchiveWorkflowViaUiRequest>
{
    public override async Task<object?> ExecuteAsync(IPage page, Dictionary<string, object?> resolvedFields, WorkflowContext context)
    {
        await page.ClickAsync("#workflow-archive-btn");
        await page.WaitForFunctionAsync("document.querySelector('#workflow-archive-btn')?.textContent?.trim() === 'Unarchive'");
        return new UiActionResponse();
    }
}

public class PlaywrightArchiveTargetStep : PlaywrightStep<ArchiveTargetViaUiRequest>
{
    public override async Task<object?> ExecuteAsync(IPage page, Dictionary<string, object?> resolvedFields, WorkflowContext context)
    {
        var name = (string)resolvedFields["Name"]!;
        var row = page.Locator("#target-list tr").Filter(new LocatorFilterOptions { HasText = name });
        await row.GetByText("Archive").ClickAsync();
        await page.WaitForFunctionAsync(
            $"!document.querySelector('#target-list')?.innerText?.includes('{name.Replace("'", "\\'")}')");
        return new UiActionResponse();
    }
}

public class PlaywrightCreateWorkflowViaFormStep : PlaywrightStep<CreateWorkflowViaFormRequest>
{
    public override async Task<object?> ExecuteAsync(IPage page, Dictionary<string, object?> resolvedFields, WorkflowContext context)
    {
        var name = (string)resolvedFields["Name"]!;
        await page.GetByRole(AriaRole.Button, new() { Name = "+ New Workflow" }).ClickAsync();
        await page.WaitForSelectorAsync("h3:text('New Workflow')");
        await page.FillAsync("#new-workflow-name", name);
        await page.GetByRole(AriaRole.Button, new() { Name = "Create" }).ClickAsync();
        await page.WaitForFunctionAsync("!document.querySelector('#workflow-detail')?.classList?.contains('hidden')");
        return new CreateWorkflowViaFormOutput(name);
    }
}

public class PlaywrightCreateCatalogViaFormStep : PlaywrightStep<CreateCatalogViaFormRequest>
{
    public override async Task<object?> ExecuteAsync(IPage page, Dictionary<string, object?> resolvedFields, WorkflowContext context)
    {
        var name = (string)resolvedFields["Name"]!;
        await page.GetByRole(AriaRole.Button, new() { Name = "+ New Catalog" }).ClickAsync();
        await page.WaitForSelectorAsync("h3:text('New Catalog')");
        await page.FillAsync("#new-catalog-name", name);
        await page.GetByRole(AriaRole.Button, new() { Name = "Create" }).ClickAsync();
        await page.WaitForFunctionAsync($"document.querySelector('#catalog-list')?.innerText?.includes('{name}')");
        return new CreateCatalogViaFormOutput(name);
    }
}

public class PlaywrightNextWorkflowsPageStep : PlaywrightStep<NextWorkflowsPageRequest>
{
    public override async Task<object?> ExecuteAsync(IPage page, Dictionary<string, object?> resolvedFields, WorkflowContext context)
    {
        await page.WaitForFunctionAsync("document.querySelector('#pager-workflows')?.textContent?.trim().length > 0");
        var currentText = await page.InnerTextAsync("#pager-workflows");
        var current = PagedUiHelper.ParsePagerInfo(currentText).CurrentPage;
        await page.ClickAsync("#pager-workflows button:last-child");
        await page.WaitForFunctionAsync(
            $"document.querySelector('#pager-workflows').textContent.includes('Page {current + 1} of')");
        return PagedUiHelper.ParsePagerInfo(await page.InnerTextAsync("#pager-workflows"));
    }
}

public class PlaywrightNextCatalogsPageStep : PlaywrightStep<NextCatalogsPageRequest>
{
    public override async Task<object?> ExecuteAsync(IPage page, Dictionary<string, object?> resolvedFields, WorkflowContext context)
    {
        await page.WaitForFunctionAsync("document.querySelector('#pager-catalogs')?.textContent?.trim().length > 0");
        var currentText = await page.InnerTextAsync("#pager-catalogs");
        var current = PagedUiHelper.ParsePagerInfo(currentText).CurrentPage;
        await page.ClickAsync("#pager-catalogs button:last-child");
        await page.WaitForFunctionAsync(
            $"document.querySelector('#pager-catalogs').textContent.includes('Page {current + 1} of')");
        return PagedUiHelper.ParsePagerInfo(await page.InnerTextAsync("#pager-catalogs"));
    }
}

public class PlaywrightOpenRunDetailStep : PlaywrightStep<OpenRunDetailRequest>
{
    public override async Task<object?> ExecuteAsync(IPage page, Dictionary<string, object?> resolvedFields, WorkflowContext context)
    {
        var workflowName = (string)resolvedFields["WorkflowName"]!;
        var row = page.Locator("#runs-list tr").Filter(new LocatorFilterOptions { HasText = workflowName });
        await row.GetByText("View").ClickAsync();
        await page.Locator("#run-detail").WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });
        return new UiActionResponse();
    }
}
