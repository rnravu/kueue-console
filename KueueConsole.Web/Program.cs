using KueueConsole.Web.Services;
using KueueConsole.Web.Services.Watchers;
using k8s;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

// ── Kubernetes client ────────────────────────────────────────────────────────
builder.Services.AddSingleton<IKubernetes>(sp =>
{
    var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger("KubernetesClient");
    try
    {
        var config = KubernetesClientConfiguration.InClusterConfig();
        logger.LogInformation("Using in-cluster Kubernetes configuration");
        return new Kubernetes(config);
    }
    catch
    {
        logger.LogInformation("In-cluster config not available, falling back to kubeconfig file");
        var config = KubernetesClientConfiguration.BuildConfigFromConfigFile();
        return new Kubernetes(config);
    }
});

// ── Event bus ────────────────────────────────────────────────────────────────
builder.Services.AddSingleton<ClusterEventService>();

// ── In-memory state stores ───────────────────────────────────────────────────
builder.Services.AddSingleton<WorkloadStateService>();
builder.Services.AddSingleton<ClusterQueueService>();
builder.Services.AddSingleton<LocalQueueStateService>();

// ── Aggregators / helpers ────────────────────────────────────────────────────
builder.Services.AddSingleton<DashboardAggregatorService>();
builder.Services.AddSingleton<NamespaceService>();builder.Services.AddSingleton<JobDiagnosticsService>();
// ── Background watch services ────────────────────────────────────────────────
builder.Services.AddHostedService<WorkloadWatchService>();
builder.Services.AddHostedService<ClusterQueueWatchService>();
builder.Services.AddHostedService<LocalQueueWatchService>();

// ── Legacy services kept for reference ──────────────────────────────────────
// Authentication/demo user store removed by request — no auth is registered.

// ── Provisioning services ────────────────────────────────────────────────────
builder.Services.AddScoped<LocalQueueProvisionService>();
builder.Services.AddScoped<JobProvisionService>();
builder.Services.AddSingleton<JobTemplateService>();
builder.Services.AddScoped<SampleDataService>();

// ── YAML / delete services ───────────────────────────────────────────────────
builder.Services.AddScoped<YamlService>();
builder.Services.AddScoped<JobDeleteService>();
builder.Services.AddScoped<QueueDeleteService>();

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();
app.UseRouting();
app.MapControllers();

app.Run();
// Required for WebApplicationFactory<Program> in test projects
public partial class Program { }
