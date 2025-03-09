using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace HotChocolate.Types.Analyzers.Models;

public interface IOutputTypeInfo
{
    /// <summary>
    /// Gets the name that the generator shall use to generate the output type.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets the namespace that the generator shall use to generate the output type.
    /// </summary>
    string Namespace { get; }

    /// <summary>
    /// Defines if the type is a public.
    /// </summary>
    bool IsPublic { get; }

    /// <summary>
    /// Gets the schema type symbol.
    /// </summary>
    INamedTypeSymbol? SchemaSchemaType { get; }

    /// <summary>
    /// Gets the full schema type name.
    /// </summary>
    string? SchemaTypeFullName { get; }

    /// <summary>
    /// Specifies if this type info has a schema type.
    /// </summary>
    [MemberNotNull(nameof(SchemaTypeFullName), nameof(SchemaSchemaType))]
    bool HasSchemaType { get; }

    /// <summary>
    /// Gets the runtime type symbol.
    /// </summary>
    INamedTypeSymbol? RuntimeType { get; }

    /// <summary>
    /// Gets the full runtime type name.
    /// </summary>
    string? RuntimeTypeFullName { get; }

    /// <summary>
    /// Specifies if this type info has a runtime type.
    /// </summary>
    [MemberNotNull(nameof(RuntimeTypeFullName), nameof(RuntimeType))]
    bool HasRuntimeType { get; }

    /// <summary>
    /// Gets the class declaration if one exists that this type info is based on.
    /// </summary>
    ClassDeclarationSyntax? ClassDeclaration { get; }

    ImmutableArray<Resolver> Resolvers { get; }

    ImmutableArray<Diagnostic> Diagnostics { get; }

    void AddDiagnostic(Diagnostic diagnostic);

    void AddDiagnosticRange(ImmutableArray<Diagnostic> diagnostics);

     void ReplaceResolver(
        Resolver current,
        Resolver replacement);
}
