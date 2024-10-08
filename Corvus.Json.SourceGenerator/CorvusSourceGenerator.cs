﻿// <copyright file="IncrementalSourceGenerator.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using Corvus.Json.CodeGeneration;
using Corvus.Json.CodeGeneration.CorvusVocabulary;
using Corvus.Json.CodeGeneration.CSharp;
using Corvus.Json.CodeGeneration.Draft202012;
using H.Generators.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

namespace Corvus.Json.SourceGenerator;

/// <summary>
/// Base for a source generator.
/// </summary>
[Generator]
public class CorvusSourceGenerator : IIncrementalGenerator
{
    private static readonly ImmutableArray<string> DefaultDisabledNamingHeuristics = ["DocumentationNameHeuristic"];
    private static readonly PrepopulatedDocumentResolver MetaSchemaResolver = CreateMetaSchemaResolver();
    private static readonly VocabularyRegistry VocabularyRegistry = RegisterVocabularies(MetaSchemaResolver);

    private const string InlineSource = "CorvusSource";

    private static readonly DiagnosticDescriptor Crv1001ErrorGeneratingCSharpCode =
        new(id: "CRV1001",
            title: "JSON Schema Type Generator Error",
            messageFormat: $"Error generating C# code: {{0}}",
            category: "JsonSchemaCodeGenerator",
            DiagnosticSeverity.Error,
            isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor Crv1000ErrorAddingTypeDeclarations =
        new(id: "CRV1000",
            title: "JSON Schema Type Generator Error",
            messageFormat: $"Error adding type declarations for path '{{0}}': {{1}}",
            category: "JsonSchemaCodeGenerator",
            DiagnosticSeverity.Error,
            isEnabledByDefault: true);

    /// <inheritdoc/>
    public void Initialize(IncrementalGeneratorInitializationContext initializationContext)
    {
        EmitGeneratorAttribute(initializationContext);

        IncrementalValueProvider<GlobalOptions> globalOptions =
            initializationContext.AnalyzerConfigOptionsProvider.Select(GetGlobalOptions);

        IncrementalValuesProvider<InlinedSource> jsonSourceFiles = initializationContext.AdditionalTextsProvider
            .Where(p => p.Path.EndsWith(".json"))
            .Combine(initializationContext.AnalyzerConfigOptionsProvider)
            .Select((x, cancellationToken) =>
            {
                var (additionalText, configProvider) = x;

                if (configProvider.GetOptions(additionalText).TryGetValue(InlineSource, out var inlineSource))
                {
                    return new InlinedSource(
                        inlineSource,
                        additionalText.GetText(cancellationToken)
                    );
                }

                return null;
            })
            .Where(x => x?.Content != null);

        IncrementalValueProvider<CompoundDocumentResolver> documentResolver =
            jsonSourceFiles.Collect().Select(BuildDocumentResolver);

        IncrementalValueProvider<GenerationContext> generationContext = documentResolver.Combine(globalOptions)
            .Select((r, c) => new GenerationContext(r.Left, r.Right));

        IncrementalValuesProvider<GenerationSpecification> generationSpecifications =
            initializationContext.SyntaxProvider.ForAttributeWithMetadataName(
                "Corvus.Json.JsonSchemaTypeGeneratorAttribute",
                IsValidAttributeTarget,
                BuildGenerationSpecifications
            );

        IncrementalValueProvider<TypesToGenerate> typesToGenerate = generationSpecifications.Collect()
            .Combine(generationContext).Select((c, t) => new TypesToGenerate(c.Left, c.Right));

        typesToGenerate
            .SelectAndReportExceptions(GenerateCode, initializationContext)
            .SelectAndReportDiagnostics(initializationContext)
            .AddSource(initializationContext);
    }

    private bool IsValidAttributeTarget(SyntaxNode node, CancellationToken token)
    {
        return
            node is StructDeclarationSyntax structDeclarationSyntax &&
            structDeclarationSyntax
                .Modifiers
                .Any(m => m.IsKind(SyntaxKind.PartialKeyword)) &&
            structDeclarationSyntax.Parent is FileScopedNamespaceDeclarationSyntax or NamespaceDeclarationSyntax;
    }

    private static ResultWithDiagnostics<EquatableArray<FileWithName>> GenerateCode(
        TypesToGenerate generationSource, CancellationToken cancellationToken
    )
    {
        if (generationSource.GenerationSpecifications.Length == 0)
        {
            // Nothing to generate
            return new ResultWithDiagnostics<EquatableArray<FileWithName>>(
                ImmutableArray<FileWithName>.Empty.AsEquatableArray()
            );
        }

        List<TypeDeclaration> typesToGenerate = [];
        List<CSharpLanguageProvider.NamedType> namedTypes = [];
        JsonSchemaTypeBuilder typeBuilder = new(generationSource.DocumentResolver, VocabularyRegistry);

        var diagnostics = new List<Diagnostic>();
        foreach (GenerationSpecification spec in generationSource.GenerationSpecifications)
        {
            cancellationToken.ThrowIfCancellationRequested();

            string schemaFile = spec.Location;
            JsonReference reference = new(schemaFile);

            var rootType = typeBuilder.AddTypeDeclarations(
                reference, generationSource.FallbackVocabulary,
                spec.RebaseToRootPath, cancellationToken
            );

            typesToGenerate.Add(rootType);

            namedTypes.Add(
                new CSharpLanguageProvider.NamedType(
                    rootType.ReducedTypeDeclaration().ReducedType.LocatedSchema.Location,
                    spec.TypeName,
                    spec.Namespace));
        }

        CSharpLanguageProvider.Options options = new(
            "GeneratedTypes",
            namedTypes.ToArray(),
            disabledNamingHeuristics: generationSource.DisabledNamingHeuristics.ToArray(),
            optionalAsNullable: generationSource.OptionalAsNullable,
            useOptionalNameHeuristics: generationSource.UseOptionalNameHeuristics,
            alwaysAssertFormat: generationSource.AlwaysAssertFormat,
            fileExtension: ".g.cs");

        var languageProvider = CSharpLanguageProvider.DefaultWithOptions(options);

        var generatedCode = typeBuilder.GenerateCodeUsing(
            languageProvider,
            cancellationToken,
            typesToGenerate
        ).Select(x => new FileWithName(x.FileName, x.FileContent)).ToImmutableArray();

        return new ResultWithDiagnostics<EquatableArray<FileWithName>>(
            generatedCode.AsEquatableArray(),
            diagnostics.ToImmutableArray().AsEquatableArray()
        );
    }

    private static GenerationSpecification BuildGenerationSpecifications(
        GeneratorAttributeSyntaxContext context, CancellationToken token
    )
    {
        AttributeData attribute = context.Attributes[0];
        string location = attribute.ConstructorArguments[0].Value as string ??
                          throw new InvalidOperationException("Location is required");

        bool rebaseToRootPath = attribute.ConstructorArguments[1].Value as bool? ?? false;

        return new(
            context.TargetSymbol.Name, context.TargetSymbol.ContainingNamespace.ToDisplayString(), location,
            rebaseToRootPath
        );
    }

    private static GlobalOptions GetGlobalOptions(AnalyzerConfigOptionsProvider source, CancellationToken token)
    {
        IVocabulary fallbackVocabulary = VocabularyAnalyser.DefaultVocabulary;
        if (source.GlobalOptions.TryGetValue("build_property.CorvusJsonSchemaFallbackVocabulary",
                out string? fallbackVocabularyName))
        {
            fallbackVocabulary = fallbackVocabularyName switch
            {
                "Draft202012" => VocabularyAnalyser.DefaultVocabulary,
                "Draft201909" => CodeGeneration.Draft201909.VocabularyAnalyser.DefaultVocabulary,
                "Draft7" => CodeGeneration.Draft7.VocabularyAnalyser.DefaultVocabulary,
                "Draft6" => CodeGeneration.Draft6.VocabularyAnalyser.DefaultVocabulary,
                "Draft4" => CodeGeneration.Draft4.VocabularyAnalyser.DefaultVocabulary,
                "OpenApi30" => CodeGeneration.OpenApi30.VocabularyAnalyser.DefaultVocabulary,
                _ => VocabularyAnalyser.DefaultVocabulary,
            };
        }

        bool optionalAsNullable = true;

        if (source.GlobalOptions.TryGetValue("build_property.CorvusJsonSchemaOptionalAsNullable",
                out string? optionalAsNullableName))
        {
            optionalAsNullable = optionalAsNullableName == "NullOrUndefined";
        }

        bool useOptionalNameHeuristics = true;

        if (source.GlobalOptions.TryGetValue("build_property.CorvusJsonSchemaUseOptionalNameHeuristics",
                out string? useOptionalNameHeuristicsName))
        {
            useOptionalNameHeuristics =
                useOptionalNameHeuristicsName == "true" || useOptionalNameHeuristicsName == "True";
        }

        bool alwaysAssertFormat = true;

        if (source.GlobalOptions.TryGetValue("build_property.CorvusJsonSchemaAlwaysAssertFormat",
                out string? alwaysAssertFormatName))
        {
            alwaysAssertFormat = alwaysAssertFormatName == "true" || alwaysAssertFormatName == "True";
        }

        ImmutableArray<string>? disabledNamingHeuristics = null;

        if (source.GlobalOptions.TryGetValue("build_property.CorvusJsonSchemaDisabledNamingHeuristics",
                out string? disabledNamingHeuristicsSemicolonSeparated))
        {
            string[] disabledNames =
                disabledNamingHeuristicsSemicolonSeparated.Split([';'], StringSplitOptions.RemoveEmptyEntries);

            disabledNamingHeuristics = disabledNames.Select(d => d.Trim()).ToImmutableArray();
        }

        return new(fallbackVocabulary, optionalAsNullable, useOptionalNameHeuristics, alwaysAssertFormat,
            disabledNamingHeuristics ?? DefaultDisabledNamingHeuristics);
    }

    private static CompoundDocumentResolver BuildDocumentResolver(
        ImmutableArray<InlinedSource> sources, CancellationToken token
    )
    {
        PrepopulatedDocumentResolver newResolver = new();
        foreach (var source in sources)
        {
            if (token.IsCancellationRequested)
            {
                return new CompoundDocumentResolver();
            }

            var json = source.Content.ToString();

            var doc = JsonDocument.Parse(json);

            newResolver.AddDocument(source.InlineSource, doc);
        }

        return new CompoundDocumentResolver(newResolver, MetaSchemaResolver);
    }

    private static PrepopulatedDocumentResolver CreateMetaSchemaResolver()
    {
        PrepopulatedDocumentResolver metaSchemaResolver = new();
        metaSchemaResolver.AddMetaschema();
        return metaSchemaResolver;
    }

    private static VocabularyRegistry RegisterVocabularies(IDocumentResolver documentResolver)
    {
        VocabularyRegistry vocabularyRegistry = new();

        // Add support for the vocabularies we are interested in.
        VocabularyAnalyser.RegisterAnalyser(documentResolver, vocabularyRegistry);
        CodeGeneration.Draft201909.VocabularyAnalyser.RegisterAnalyser(documentResolver, vocabularyRegistry);
        CodeGeneration.Draft7.VocabularyAnalyser.RegisterAnalyser(vocabularyRegistry);
        CodeGeneration.Draft6.VocabularyAnalyser.RegisterAnalyser(vocabularyRegistry);
        CodeGeneration.Draft4.VocabularyAnalyser.RegisterAnalyser(vocabularyRegistry);
        CodeGeneration.OpenApi30.VocabularyAnalyser.RegisterAnalyser(vocabularyRegistry);

        // And register the custom vocabulary for Corvus extensions.
        vocabularyRegistry.RegisterVocabularies(
            SchemaVocabulary.DefaultInstance);

        return vocabularyRegistry;
    }

    private static void EmitGeneratorAttribute(IncrementalGeneratorInitializationContext initializationContext)
    {
        initializationContext.RegisterPostInitializationOutput(static postInitializationContext =>
        {
            postInitializationContext.AddSource(
                "JsonSchemaTypeGeneratorAttribute.g.cs",
                SourceText.From(
                    """
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
                    """, Encoding.UTF8));
        });
    }
}
