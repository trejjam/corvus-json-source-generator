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
        new DictionaryAnalyzerConfigOptionsProvider(
            additionalTextOptions: new Dictionary<string, Dictionary<string, string>>
            {
                [H.Resources.test_json.FileName] = new()
                {
                    ["CorvusSource"] = "https://corvus-oss.org/json-schema/2020-12/schema.json",
                }
            }
        ),
        [
            H.Resources.test_json
        ],
        """
        namespace SourceGenTest2.Model;

        using Corvus.Json;

        [JsonSchemaTypeGenerator("https://corvus-oss.org/json-schema/2020-12/schema.json")]
        public readonly partial struct FlimFlam;
        """
    );
}
