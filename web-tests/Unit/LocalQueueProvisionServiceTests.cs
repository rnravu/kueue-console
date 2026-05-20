using System.Text.Json;
using KueueConsole.Web.Services;

namespace KueueConsole.Web.Tests.Unit;

public class LocalQueueProvisionServiceTests
{
    private static JsonElement ManifestJson(string name, string ns, string cq,
        Dictionary<string, string>? labels = null)
    {
        var obj = LocalQueueProvisionService.BuildManifest(name, ns, cq, labels);
        var json = JsonSerializer.Serialize(obj, new JsonSerializerOptions { PropertyNamingPolicy = null });
        return JsonDocument.Parse(json).RootElement;
    }

    [Fact]
    public void BuildManifest_HasCorrectApiVersionAndKind()
    {
        var doc = ManifestJson("my-q", "demo", "cluster-queue");

        Assert.Equal("kueue.x-k8s.io/v1beta1", doc.GetProperty("apiVersion").GetString());
        Assert.Equal("LocalQueue", doc.GetProperty("kind").GetString());
    }

    [Fact]
    public void BuildManifest_MetadataContainsNameAndNamespace()
    {
        var doc = ManifestJson("user-queue", "demo", "cq-1");

        var meta = doc.GetProperty("metadata");
        Assert.Equal("user-queue", meta.GetProperty("name").GetString());
        Assert.Equal("demo", meta.GetProperty("namespace").GetString());
    }

    [Fact]
    public void BuildManifest_SpecContainsClusterQueue()
    {
        var doc = ManifestJson("my-q", "demo", "target-cq");

        Assert.Equal("target-cq", doc.GetProperty("spec").GetProperty("clusterQueue").GetString());
    }

    [Fact]
    public void BuildManifest_ExtraLabelsAreIncluded()
    {
        var labels = new Dictionary<string, string>
        {
            ["kueue-console.io/managed-by"] = "sample-data"
        };
        var doc = ManifestJson("lq", "demo", "cq", labels);

        var labelsEl = doc.GetProperty("metadata").GetProperty("labels");
        Assert.Equal("sample-data", labelsEl.GetProperty("kueue-console.io/managed-by").GetString());
    }

    [Fact]
    public void BuildManifest_NoLabels_LabelsObjectIsEmpty()
    {
        var doc = ManifestJson("lq", "demo", "cq");

        var labelsEl = doc.GetProperty("metadata").GetProperty("labels");
        // Should be an empty object, not missing
        Assert.Equal(JsonValueKind.Object, labelsEl.ValueKind);
    }
}
