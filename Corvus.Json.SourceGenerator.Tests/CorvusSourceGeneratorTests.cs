using System.Collections.Generic;
using System.Threading.Tasks;
using H.Generators.Tests.Extensions;
using Xunit;

namespace Corvus.Json.SourceGenerator.Tests;

public class CorvusSourceGeneratorTests
{
    [Fact]
    public Task EmptyWorks() => TestHelper.Verify<CorvusSourceGenerator>(string.Empty);

    [Fact]
    public Task SimpleWorks() => TestHelper.Verify<CorvusSourceGenerator>(
        new DictionaryAnalyzerConfigOptionsProvider(),
        [
            H.Resources.test_json
        ],
        """
        namespace SourceGenTest2.Model;

        using Corvus.Json;

        [JsonSchemaTypeGenerator("../test.json")]
        public readonly partial struct FlimFlam;
        """
    );
}
