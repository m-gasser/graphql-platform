using System.Collections.Immutable;
using HotChocolate.Types.Analyzers.Filters;
using HotChocolate.Types.Analyzers.Generators;
using HotChocolate.Types.Analyzers.Inspectors;
using HotChocolate.Types.Analyzers.Models;
using Microsoft.CodeAnalysis;

namespace HotChocolate.Types.Analyzers;

[Generator]
public class GraphQLServerGenerator : IIncrementalGenerator
{
    private static readonly ISyntaxInspector[] _inspectors =
    [
        new ClassBaseClassInspector(),
        new ModuleInspector(),
        new DataLoaderDefaultsInspector(),
        new DataLoaderModuleInspector(),
        new RequestMiddlewareInspector()
    ];

    private static readonly IAttributeWithMetadataInspector[] _attributeInspectors =
    [
        new TypeAttributeInspector(WellKnownAttributes.ExtendObjectTypeAttribute),
        new TypeAttributeInspector(WellKnownAttributes.ExtendObjectTypeAttributeGeneric),
        new TypeAttributeInspector(WellKnownAttributes.QueryTypeAttribute),
        new TypeAttributeInspector(WellKnownAttributes.MutationTypeAttribute),
        new TypeAttributeInspector(WellKnownAttributes.SubscriptionTypeAttribute),
        new DataLoaderInspector(),
        new OperationInspector(OperationType.Query),
        new OperationInspector(OperationType.Mutation),
        new OperationInspector(OperationType.Subscription),
        new ObjectTypeExtensionInfoInspector(WellKnownAttributes.ObjectTypeAttribute),
        new ObjectTypeExtensionInfoInspector(WellKnownAttributes.ObjectTypeAttributeGeneric),
        new ObjectTypeExtensionInfoInspector(WellKnownAttributes.QueryTypeAttribute),
        new ObjectTypeExtensionInfoInspector(WellKnownAttributes.MutationTypeAttribute),
        new ObjectTypeExtensionInfoInspector(WellKnownAttributes.SubscriptionTypeAttribute),
        new InterfaceTypeInfoInspector(WellKnownAttributes.InterfaceTypeAttribute),
        new InterfaceTypeInfoInspector(WellKnownAttributes.InterfaceTypeAttributeGeneric)
    ];

    private static readonly ISyntaxGenerator[] _generators =
    [
        new TypeModuleSyntaxGenerator(),
        new TypesSyntaxGenerator(),
        new MiddlewareGenerator(),
        new DataLoaderModuleGenerator(),
        new DataLoaderGenerator()
    ];

    private static readonly Func<SyntaxNode, bool> _predicate;

    static GraphQLServerGenerator()
    {
        var filterBuilder = new SyntaxFilterBuilder();

        foreach (var inspector in _inspectors)
        {
            filterBuilder.AddRange(inspector.Filters);
        }

        _predicate = filterBuilder.Build();
    }

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var syntaxInfos =
            context.SyntaxProvider
                .CreateSyntaxProvider(
                    predicate: static (s, _) => Predicate(s),
                    transform: static (ctx, _) => Transform(ctx))
                .WhereNotNull()
                .WithComparer(SyntaxInfoComparer.Default);

        foreach (var inspector in _attributeInspectors)
        {
            var valuesProvider = context.SyntaxProvider.ForAttributeWithMetadataName(
                    inspector.FullyQualifiedMetadataName,
                    inspector.Predicate,
                    inspector.Transform)
                .WhereNotNull()
                .WithComparer(SyntaxInfoComparer.Default);
            syntaxInfos = syntaxInfos.Concat(valuesProvider);
        }

        var assemblyNameProvider = context.CompilationProvider
            .Select(static (c, _) => c.AssemblyName!);

        var valueProvider = assemblyNameProvider.Combine(syntaxInfos.Collect());

        context.RegisterSourceOutput(
            valueProvider,
            static (context, source) => Execute(context, source.Left, source.Right));
    }

    private static bool Predicate(SyntaxNode node)
        => _predicate(node);

    private static SyntaxInfo? Transform(GeneratorSyntaxContext context)
    {
        for (var i = 0; i < _inspectors.Length; i++)
        {
            if (_inspectors[i].TryHandle(context, out var syntaxInfo))
            {
                return syntaxInfo;
            }
        }

        return null;
    }

    private static void Execute(
        SourceProductionContext context,
        string assemblyName,
        ImmutableArray<SyntaxInfo> syntaxInfos)
    {
        foreach (var syntaxInfo in syntaxInfos.AsSpan())
        {
            if (syntaxInfo.Diagnostics.Length > 0)
            {
                foreach (var diagnostic in syntaxInfo.Diagnostics.AsSpan())
                {
                    context.ReportDiagnostic(diagnostic);
                }
            }
        }

        foreach (var generator in _generators.AsSpan())
        {
            generator.Generate(context, assemblyName, syntaxInfos);
        }
    }
}

file static class Extensions
{
    public static IncrementalValuesProvider<SyntaxInfo> WhereNotNull(
        this IncrementalValuesProvider<SyntaxInfo?> source)
        => source.Where(static t => t is not null)!;

    // Currently there is no easy way to concatenate IncrementalValuesProviders.
    // See https://github.com/dotnet/roslyn/discussions/62761
    public static IncrementalValuesProvider<TSource> Concat<TSource>(
        this IncrementalValuesProvider<TSource> first,
        IncrementalValuesProvider<TSource> second)
        => first.Collect().Combine(second.Collect()).SelectMany((x, _) => x.Left.Concat(x.Right));
}
