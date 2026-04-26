using DevHub.Services;

namespace DevHub.U.Tests.Services.When_PackageJsonReader_reads;

public class Then_empty_when_no_file
{
    [Fact]
    public void Execute()
    {
        var sut = new PackageJsonReader();
        var result = sut.GetScripts(@"C:\ruta\que\no\existe");

        Assert.Empty(result);
    }
}