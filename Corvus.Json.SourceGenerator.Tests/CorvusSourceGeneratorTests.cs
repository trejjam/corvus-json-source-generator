using System.Threading.Tasks;
using Xunit;

namespace Corvus.Json.SourceGenerator.Tests;

public class CorvusSourceGeneratorTests
{
    [Fact]
    public Task EmptyWorks() => TestHelper.Verify<CorvusSourceGenerator>(string.Empty);

    [Fact]
    public Task SimpleWorks() => TestHelper.Verify<CorvusSourceGenerator>(
        """
         
        
        """
    );
}
