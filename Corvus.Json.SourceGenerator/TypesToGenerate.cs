using System.Collections.Immutable;
using Corvus.Json.CodeGeneration;

namespace Corvus.Json.SourceGenerator;

internal readonly struct TypesToGenerate(ImmutableArray<GenerationSpecification> left, GenerationContext right)
{
    public ImmutableArray<GenerationSpecification> GenerationSpecifications { get; } = left;

    public CompoundDocumentResolver DocumentResolver { get; } = right.DocumentResolver;

    public IVocabulary FallbackVocabulary { get; } = right.FallbackVocabulary;

    public bool OptionalAsNullable { get; } = right.OptionalAsNullable;

    public bool UseOptionalNameHeuristics { get; } = right.UseOptionalNameHeuristics;

    public ImmutableArray<string> DisabledNamingHeuristics { get; } = right.DisabledNamingHeuristics;

    public bool AlwaysAssertFormat { get; } = right.AlwaysAssertFormat;
}