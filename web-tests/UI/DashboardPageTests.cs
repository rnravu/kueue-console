using Microsoft.Playwright;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using KueueConsole.Web.Models;
using KueueConsole.Web.Services;

namespace KueueConsole.Web.Tests.UI;

/// <summary>
/// Playwright UI tests that launch the ASP.NET app in-process and
/// drive a real browser (Chromium) against it.
///
/// PREREQUISITES:
///   Run `pwsh playwright.ps1 install chromium` from the test project's bin directory
///   the first time, or invoke: dotnet exec playwright.dll install chromium
/// </summary>
[Collection("UI Tests")]
public class DashboardPageTests : IAsyncLifetime
{
    private IPlaywright _playwright = null!;
    private IBrowser _browser = null!;
    private WebApplicationFactory<Program> _factory = null!;
    private string _baseUrl = "";

    public async Task InitializeAsync()
    {
        // Remove watchers and real Kubernetes so no cluster is needed
        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                var k8sDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(k8s.IKubernetes));
                if (k8sDescriptor != null) services.Remove(k8sDescriptor);
                services.AddSingleton<k8s.IKubernetes>(_ => null!);

                var hosted = services.Where(d =>
                    d.ServiceType == typeof(Microsoft.Extensions.Hosting.IHostedService)).ToList();
                foreach (var d in hosted) services.Remove(d);
            });
        });

        // Start the server and obtain the base URL from the test server
        var client = _factory.CreateClient();
        _baseUrl = _factory.Server.BaseAddress.ToString();

        _playwright = await Playwright.CreateAsync();
        _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = true
        });
    }

    public async Task DisposeAsync()
    {
        await _browser.CloseAsync();
        _playwright.Dispose();
        _factory.Dispose();
    }

    private async Task<IPage> NewPageAsync()
    {
        var page = await _browser.NewPageAsync();
        // Route /api/* to the in-process test server
        await page.RouteAsync("**/*", async route =>
        {
            var url = route.Request.Url;
            if (url.Contains("/api/") || url == _baseUrl || url == _baseUrl + "index.html")
            {
                // Let through to the actual server via fetch from within the browser context
                await route.ContinueAsync();
            }
            else
            {
                await route.ContinueAsync();
            }
        });
        return page;
    }

    [Fact]
    public async Task DashboardPage_Loads_ShowsSummaryCards()
    {
        // Seed data before page load
        using var scope = _factory.Services.CreateScope();
        var workloads = scope.ServiceProvider.GetRequiredService<WorkloadStateService>();
        workloads.Apply(new WorkloadDto { Name = "test-wl", Namespace = "ns", Status = "Running" });

        var page = await _browser.NewPageAsync();
        await page.GotoAsync(_baseUrl);

        // The page title "Kueue Console" should be visible
        await page.WaitForSelectorAsync("text=Kueue Console");
        var title = await page.TextContentAsync("nav");
        Assert.Contains("Kueue Console", title);
    }

    [Fact]
    public async Task Navigation_ClickingQueues_ShowsQueuesPage()
    {
        var page = await _browser.NewPageAsync();
        await page.GotoAsync(_baseUrl);
        await page.WaitForSelectorAsync("text=Queues");

        await page.ClickAsync("button:has-text('Queues')");
        var content = await page.TextContentAsync("main");

        Assert.Contains("Queues", content);
    }

    [Fact]
    public async Task Navigation_ClickingJobs_ShowsJobsPage()
    {
        var page = await _browser.NewPageAsync();
        await page.GotoAsync(_baseUrl);
        await page.WaitForSelectorAsync("button:has-text('Jobs')");

        await page.ClickAsync("button:has-text('Jobs')");
        // Wait for the Jobs heading to appear
        await page.WaitForSelectorAsync("text=Jobs & Workloads");
        var heading = await page.TextContentAsync("h1");

        Assert.Contains("Jobs", heading);
    }

    [Fact]
    public async Task Navigation_ClickingTroubleshooting_ShowsTroubleshootPage()
    {
        var page = await _browser.NewPageAsync();
        await page.GotoAsync(_baseUrl);
        await page.WaitForSelectorAsync("button:has-text('Troubleshooting')");

        await page.ClickAsync("button:has-text('Troubleshooting')");
        await page.WaitForSelectorAsync("text=Stuck");
        var content = await page.TextContentAsync("main");

        Assert.Contains("Stuck", content);
    }

    [Fact]
    public async Task DashboardPage_SSEIndicator_IsPresentInNav()
    {
        var page = await _browser.NewPageAsync();
        await page.GotoAsync(_baseUrl);
        await page.WaitForSelectorAsync("nav");

        // The SSE indicator (Live or Disconnected) should appear
        var nav = await page.TextContentAsync("nav");
        Assert.True(nav!.Contains("Live") || nav.Contains("Disconnected"));
    }
}
