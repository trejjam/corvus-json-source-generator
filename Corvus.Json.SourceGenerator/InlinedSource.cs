using Microsoft.CodeAnalysis.Text;

namespace Corvus.Json.SourceGenerator;

public sealed record InlinedSource(
    string InlineSource,
    SourceText? Content
);
