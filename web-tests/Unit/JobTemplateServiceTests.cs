using KueueConsole.Web.Services;

namespace KueueConsole.Web.Tests.Unit;

public class JobTemplateServiceTests
{
    private readonly JobTemplateService _svc = new();

    [Fact]
    public void GetAll_ReturnsThreeTemplates()
    {
        var templates = _svc.GetAll();
        Assert.Equal(3, templates.Count);
    }

    [Fact]
    public void GetAll_EachTemplateHasRequiredFields()
    {
        foreach (var t in _svc.GetAll())
        {
            Assert.False(string.IsNullOrWhiteSpace(t.Id));
            Assert.False(string.IsNullOrWhiteSpace(t.Name));
            Assert.False(string.IsNullOrWhiteSpace(t.Image));
            Assert.False(string.IsNullOrWhiteSpace(t.Command));
            Assert.False(string.IsNullOrWhiteSpace(t.CpuRequest));
            Assert.False(string.IsNullOrWhiteSpace(t.MemoryRequest));
        }
    }

    [Fact]
    public void GetById_KnownId_ReturnsTemplate()
    {
        var t = _svc.GetById("job1");
        Assert.NotNull(t);
        Assert.Equal("job1", t!.Id);
        Assert.Equal("busybox", t.Image);
    }

    [Fact]
    public void GetById_CaseInsensitive()
    {
        var t = _svc.GetById("JOB2");
        Assert.NotNull(t);
        Assert.Equal("job2", t!.Id);
    }

    [Fact]
    public void GetById_UnknownId_ReturnsNull()
    {
        var t = _svc.GetById("does-not-exist");
        Assert.Null(t);
    }
}
