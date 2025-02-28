using HotChocolate.Types.Analyzers.Models;
using Microsoft.CodeAnalysis;

namespace HotChocolate.Types.Analyzers.Inspectors;

public interface IAttributeWithMetadataInspector
{
    string FullyQualifiedMetadataName { get; }
    bool Predicate(SyntaxNode syntaxNode, CancellationToken cancellationToken);
    SyntaxInfo? Transform(GeneratorAttributeSyntaxContext context, CancellationToken cancellationToken);
}
