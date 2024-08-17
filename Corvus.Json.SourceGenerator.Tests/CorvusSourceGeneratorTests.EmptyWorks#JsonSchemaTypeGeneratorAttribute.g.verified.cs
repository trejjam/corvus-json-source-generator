//HintName: JsonSchemaTypeGeneratorAttribute.g.cs
using System;

namespace Corvus.Json;

[AttributeUsage(AttributeTargets.Struct, Inherited = false, AllowMultiple = false)]
internal sealed class JsonSchemaTypeGeneratorAttribute : Attribute
{
    public JsonSchemaTypeGeneratorAttribute(string location, bool rebaseToRootPath = false)
    {
        this.Location = location;
        this.RebaseToRootPath = rebaseToRootPath;
    }

    /// <summary>
    /// Gets the location for the JSON schema.
    /// </summary>
    public string Location { get; }

    /// <summary>
    /// Gets a value indicating whether to rebase to the root path.
    /// </summary>
    public bool RebaseToRootPath { get; }
}
