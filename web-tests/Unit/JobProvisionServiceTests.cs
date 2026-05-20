using k8s.Models;
using KueueConsole.Web.Services;

namespace KueueConsole.Web.Tests.Unit;

public class JobProvisionServiceTests
{
    [Fact]
    public void BuildJob_SetsKueueQueueLabel()
    {
        var job = JobProvisionService.BuildJob(
            "my-job", "demo", "user-queue",
            "busybox", new[] { "sleep", "60" },
            "100m", "128Mi", extraLabels: null);

        Assert.Equal("user-queue", job.Metadata.Labels["kueue.x-k8s.io/queue-name"]);
    }

    [Fact]
    public void BuildJob_IsSuspendedOnCreation()
    {
        var job = JobProvisionService.BuildJob(
            "my-job", "demo", "user-queue",
            "busybox", new[] { "sleep", "60" },
            "100m", "128Mi", extraLabels: null);

        Assert.True(job.Spec.Suspend);
    }

    [Fact]
    public void BuildJob_ContainerHasCorrectImage()
    {
        var job = JobProvisionService.BuildJob(
            "img-test", "demo", "q",
            "nginx:latest", new[] { "nginx" },
            "50m", "64Mi", extraLabels: null);

        var container = job.Spec.Template.Spec.Containers[0];
        Assert.Equal("nginx:latest", container.Image);
    }

    [Fact]
    public void BuildJob_CommandIsPassedAsArgv()
    {
        var argv = new[] { "sh", "-c", "echo hello" };
        var job = JobProvisionService.BuildJob(
            "cmd-test", "demo", "q",
            "busybox", argv,
            "50m", "64Mi", extraLabels: null);

        var container = job.Spec.Template.Spec.Containers[0];
        Assert.Equal(argv, container.Command);
    }

    [Fact]
    public void BuildJob_ResourceRequestsAreSet()
    {
        var job = JobProvisionService.BuildJob(
            "res-test", "demo", "q",
            "busybox", new[] { "sleep", "10" },
            "200m", "512Mi", extraLabels: null);

        var requests = job.Spec.Template.Spec.Containers[0].Resources.Requests;
        Assert.Equal("200m", requests["cpu"].ToString());
        Assert.Equal("512Mi", requests["memory"].ToString());
    }

    [Fact]
    public void BuildJob_ExtraLabelsAreMerged()
    {
        var extras = new Dictionary<string, string> { ["app"] = "test" };
        var job = JobProvisionService.BuildJob(
            "label-test", "demo", "q",
            "busybox", new[] { "sleep", "10" },
            "50m", "64Mi", extraLabels: extras);

        Assert.Equal("test", job.Metadata.Labels["app"]);
        // Kueue label still present
        Assert.True(job.Metadata.Labels.ContainsKey("kueue.x-k8s.io/queue-name"));
    }

    [Fact]
    public void BuildJob_RestartPolicyIsNever()
    {
        var job = JobProvisionService.BuildJob(
            "rp-test", "demo", "q",
            "busybox", new[] { "true" },
            "50m", "64Mi", extraLabels: null);

        Assert.Equal("Never", job.Spec.Template.Spec.RestartPolicy);
    }
}
