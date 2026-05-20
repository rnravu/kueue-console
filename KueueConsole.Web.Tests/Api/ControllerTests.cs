using k8s;
using k8s.Autorest;
using k8s.Models;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using KueueConsole.Web.Models;
using KueueConsole.Web.Services;

namespace KueueConsole.Web.Tests.Api;

/// <summary>
/// Custom WebApplicationFactory that replaces the real Kubernetes client
/// and all background watchers with no-ops so tests are hermetic.
/// </summary>
public class TestWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Remove the real IKubernetes registration
            var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(IKubernetes));
            if (descriptor != null) services.Remove(descriptor);

            // Register a null stub — the state stores will already be seeded by tests directly
            services.AddSingleton<IKubernetes>(_ => null!);

            // Remove hosted services (watchers) so they don't try to connect to a cluster
            var hostedToRemove = services
                .Where(d => d.ServiceType == typeof(Microsoft.Extensions.Hosting.IHostedService))
                .ToList();
            foreach (var d in hostedToRemove) services.Remove(d);
        });
    }
}

public class DashboardControllerTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly TestWebApplicationFactory _factory;

    public DashboardControllerTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetDashboard_Returns200WithExpectedShape()
    {
        var response = await _client.GetAsync("/api/dashboard");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<DashboardSummaryDto>();
        Assert.NotNull(body);
        Assert.True(body!.TotalClusterQueues >= 0);
        Assert.True(body.ActiveWorkloads >= 0);
    }

    [Fact]
    public async Task GetDashboard_WithSeededWorkloads_ReflectsCorrectCounts()
    {
        // Seed state directly via DI
        using var scope = _factory.Services.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<WorkloadStateService>();
        store.Apply(new WorkloadDto { Name = "w1", Namespace = "ns", Status = "Running" });
        store.Apply(new WorkloadDto { Name = "w2", Namespace = "ns", Status = "Pending" });

        var response = await _client.GetAsync("/api/dashboard");
        var body = await response.Content.ReadFromJsonAsync<DashboardSummaryDto>();

        Assert.NotNull(body);
        Assert.True(body!.ActiveWorkloads >= 1);
        Assert.True(body.PendingWorkloads >= 1);
    }
}

public class QueuesControllerTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly TestWebApplicationFactory _factory;

    public QueuesControllerTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetClusterQueues_Returns200WithList()
    {
        var response = await _client.GetAsync("/api/queues/cluster");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<List<ClusterQueueDto>>();
        Assert.NotNull(body);
    }

    [Fact]
    public async Task GetLocalQueues_WithNamespaceFilter_ReturnsFilteredList()
    {
        using var scope = _factory.Services.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<LocalQueueStateService>();
        store.Apply(new LocalQueueDto { Name = "lq1", Namespace = "team-a" });
        store.Apply(new LocalQueueDto { Name = "lq2", Namespace = "team-b" });

        var response = await _client.GetAsync("/api/queues/local?namespace=team-a");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<List<LocalQueueDto>>();
        Assert.NotNull(body);
        Assert.All(body!, q => Assert.Equal("team-a", q.Namespace));
    }

    [Fact]
    public async Task GetLocalQueues_NoFilter_ReturnsAll()
    {
        using var scope = _factory.Services.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<LocalQueueStateService>();
        store.Apply(new LocalQueueDto { Name = "lqA", Namespace = "ns-1" });
        store.Apply(new LocalQueueDto { Name = "lqB", Namespace = "ns-2" });

        var response = await _client.GetAsync("/api/queues/local");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<List<LocalQueueDto>>();
        Assert.NotNull(body);
        Assert.True(body!.Count >= 2);
    }
}

public class JobsControllerTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly TestWebApplicationFactory _factory;

    public JobsControllerTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetJobs_Returns200()
    {
        var response = await _client.GetAsync("/api/jobs");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetJobs_StatusFilter_ReturnsMatchingItems()
    {
        using var scope = _factory.Services.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<WorkloadStateService>();
        store.Apply(new WorkloadDto { Name = "j1", Namespace = "ns", Status = "Running" });
        store.Apply(new WorkloadDto { Name = "j2", Namespace = "ns", Status = "Failed" });
        store.Apply(new WorkloadDto { Name = "j3", Namespace = "ns", Status = "Pending" });

        var response = await _client.GetAsync("/api/jobs?status=Failed");
        var body = await response.Content.ReadFromJsonAsync<List<WorkloadDto>>();

        Assert.NotNull(body);
        Assert.All(body!, w => Assert.Equal("Failed", w.Status));
    }

    [Fact]
    public async Task GetJobs_NamespaceAndStatusFilter_CombineCorrectly()
    {
        using var scope = _factory.Services.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<WorkloadStateService>();
        store.Apply(new WorkloadDto { Name = "x1", Namespace = "alpha", Status = "Running" });
        store.Apply(new WorkloadDto { Name = "x2", Namespace = "alpha", Status = "Pending" });
        store.Apply(new WorkloadDto { Name = "x3", Namespace = "beta",  Status = "Running" });

        var response = await _client.GetAsync("/api/jobs?namespace=alpha&status=Running");
        var body = await response.Content.ReadFromJsonAsync<List<WorkloadDto>>();

        Assert.NotNull(body);
        Assert.All(body!, w => Assert.Equal("alpha", w.Namespace));
        Assert.All(body!, w => Assert.Equal("Running", w.Status));
    }
}

public class StreamControllerTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client;

    public StreamControllerTests(TestWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Stream_OnConnect_ImmediatelySendsSnapshotEvents()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        using var response = await _client.GetAsync(
            "/api/stream",
            HttpCompletionOption.ResponseHeadersRead,
            cts.Token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("text/event-stream", response.Content.Headers.ContentType?.MediaType ?? "");

        await using var stream = await response.Content.ReadAsStreamAsync(cts.Token);
        using var reader = new StreamReader(stream);

        var messages = new List<string>();
        while (messages.Count < 4)
        {
            var line = await reader.ReadLineAsync(cts.Token);
            if (line is null) break;
            if (line.StartsWith("data: "))
                messages.Add(line[6..]);
        }

        cts.Cancel(); // disconnect from the stream

        Assert.Equal(4, messages.Count);

        foreach (var json in messages)
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            Assert.True(root.TryGetProperty("action", out var actionProp),
                $"SSE envelope missing 'action' field: {json}");
            Assert.Equal("snapshot", actionProp.GetString());
        }
    }
}

public class JobTemplatesControllerTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client;

    public JobTemplatesControllerTests(TestWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetJobTemplates_Returns200WithThreeItems()
    {
        var response = await _client.GetAsync("/api/job-templates");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<List<KueueConsole.Web.Models.JobTemplateDto>>();
        Assert.NotNull(body);
        Assert.Equal(3, body!.Count);
    }

    [Fact]
    public async Task GetJobTemplates_EachItemHasRequiredFields()
    {
        var response = await _client.GetAsync("/api/job-templates");
        var body = await response.Content.ReadFromJsonAsync<List<KueueConsole.Web.Models.JobTemplateDto>>();
        Assert.NotNull(body);

        foreach (var t in body!)
        {
            Assert.False(string.IsNullOrWhiteSpace(t.Id));
            Assert.False(string.IsNullOrWhiteSpace(t.Name));
            Assert.False(string.IsNullOrWhiteSpace(t.Image));
            Assert.False(string.IsNullOrWhiteSpace(t.Command));
        }
    }
}

public class CreateQueueApiTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client;

    public CreateQueueApiTests(TestWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task PostQueue_EmptyBody_Returns400()
    {
        // Sending empty JSON object → [Required] fields fail model validation
        var response = await _client.PostAsJsonAsync("/api/queues", new { });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PostQueue_MissingName_Returns400()
    {
        var response = await _client.PostAsJsonAsync("/api/queues",
            new { Namespace = "demo", ClusterQueue = "cq" });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}

public class CreateJobApiTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client;

    public CreateJobApiTests(TestWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task PostJob_EmptyBody_Returns400()
    {
        var response = await _client.PostAsJsonAsync("/api/jobs", new { });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PostJob_MissingNamespace_Returns400()
    {
        var response = await _client.PostAsJsonAsync("/api/jobs",
            new { Name = "my-job", QueueName = "user-queue" });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}

public class JobDiagnosticsControllerTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly TestWebApplicationFactory _factory;

    public JobDiagnosticsControllerTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetDiagnostics_KnownWorkload_Returns200WithExpectedFields()
    {
        using var scope = _factory.Services.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<WorkloadStateService>();
        store.Apply(new WorkloadDto
        {
            Name = "wl-diag-test", Namespace = "ns-diag", Queue = "q1",
            Status = "Running", Admitted = true, Finished = false,
        });

        var response = await _client.GetAsync("/api/jobs/ns-diag/wl-diag-test");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = doc.RootElement;

        Assert.Equal("wl-diag-test", root.GetProperty("name").GetString());
        Assert.Equal("ns-diag",      root.GetProperty("namespace").GetString());
        Assert.Equal("Running",      root.GetProperty("status").GetString());
        Assert.True(root.TryGetProperty("diagnosticSummary", out _));
        Assert.True(root.TryGetProperty("conditions", out _));
    }

    [Fact]
    public async Task GetDiagnostics_UnknownWorkload_Returns404()
    {
        var response = await _client.GetAsync("/api/jobs/no-such-ns/no-such-workload");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetDiagnostics_PendingWorkloadWithMessage_DiagnosticSummaryNonEmpty()
    {
        using var scope = _factory.Services.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<WorkloadStateService>();
        store.Apply(new WorkloadDto
        {
            Name = "wl-pending-msg", Namespace = "ns-p",
            Queue = "user-queue", Status = "Pending",
            Message = "LocalQueue user-queue doesn't exist in namespace ns-p",
            Conditions =
            [
                new KueueConsole.Web.Models.WorkloadCondition
                {
                    Type = "Admitted", ConditionStatus = "False",
                    Reason = "Inadmissible",
                    Message = "LocalQueue user-queue doesn't exist in namespace ns-p",
                },
            ],
        });

        var response = await _client.GetAsync("/api/jobs/ns-p/wl-pending-msg");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var summary = doc.RootElement.GetProperty("diagnosticSummary").GetString();
        Assert.False(string.IsNullOrWhiteSpace(summary));
        Assert.Contains("Pending because", summary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetDiagnosticsAlias_SameAsMainEndpoint()
    {
        using var scope = _factory.Services.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<WorkloadStateService>();
        store.Apply(new WorkloadDto
        {
            Name = "wl-alias", Namespace = "ns-alias", Status = "Completed",
        });

        var r1 = await _client.GetAsync("/api/jobs/ns-alias/wl-alias");
        var r2 = await _client.GetAsync("/api/jobs/ns-alias/wl-alias/diagnostics");

        Assert.Equal(HttpStatusCode.OK, r1.StatusCode);
        Assert.Equal(HttpStatusCode.OK, r2.StatusCode);
    }
}
