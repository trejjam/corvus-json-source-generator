using System.Runtime.CompilerServices;
using VerifyTests;

namespace Corvus.Json.SourceGenerator.Tests;

public static class ModuleInitializer
{
    [ModuleInitializer]
    public static void Init() => VerifySourceGenerators.Initialize();
}
