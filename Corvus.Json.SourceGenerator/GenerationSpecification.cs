namespace Corvus.Json.SourceGenerator;

internal readonly struct GenerationSpecification(string typeName, string ns, string location, bool rebaseToRootPath)
{
    public string TypeName { get; } = typeName;

    public string Namespace { get; } = ns;

    public string Location { get; } = location;

    public bool RebaseToRootPath { get; } = rebaseToRootPath;
}