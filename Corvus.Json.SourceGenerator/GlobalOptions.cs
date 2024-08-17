using System.Collections.Immutable;
using Corvus.Json.CodeGeneration;

namespace Corvus.Json.SourceGenerator;

internal readonly struct GlobalOptions(
    IVocabulary fallbackVocabulary,
    bool optionalAsNullable,
    bool useOptionalNameHeuristics,
    bool alwaysAssertFormat,
    ImmutableArray<string> disabledNamingHeuristics)
{
    public IVocabulary FallbackVocabulary { get; } = fallbackVocabulary;

    public bool OptionalAsNullable { get; } = optionalAsNullable;

    public bool UseOptionalNameHeuristics { get; } = useOptionalNameHeuristics;

    public ImmutableArray<string> DisabledNamingHeuristics { get; } = disabledNamingHeuristics;

    public bool AlwaysAssertFormat { get; } = alwaysAssertFormat;
}