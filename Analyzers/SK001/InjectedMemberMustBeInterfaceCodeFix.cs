using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ServiceKit.Analyzers
{
	[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(InjectedMemberMustBeInterfaceCodeFix))]
	[Shared]
	public sealed class InjectedMemberMustBeInterfaceCodeFix : CodeFixProvider
	{
		public override ImmutableArray<string> FixableDiagnosticIds =>
			ImmutableArray.Create(InjectedMemberMustBeInterfaceAnalyzer.DiagnosticId);

		public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

		public override async Task RegisterCodeFixesAsync(CodeFixContext context)
		{
			var document = context.Document;
			var root = await document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
			if (root == null)
			{
				return;
			}

			var diagnostic = context.Diagnostics.First();
			var span = diagnostic.Location.SourceSpan;
			var node = root.FindNode(span, getInnermostNodeForTie: true);

			// Support both fields and properties
			var fieldDecl = node.FirstAncestorOrSelf<FieldDeclarationSyntax>();
			var propDecl = node.FirstAncestorOrSelf<PropertyDeclarationSyntax>();

			if (fieldDecl != null)
			{
				await OfferFixesForFieldAsync(context, root, fieldDecl).ConfigureAwait(false);
				return;
			}

			if (propDecl != null)
			{
				await OfferFixesForPropertyAsync(context, root, propDecl).ConfigureAwait(false);
			}
		}

		private static async Task OfferFixesForFieldAsync(CodeFixContext context, SyntaxNode root, FieldDeclarationSyntax fieldDecl)
		{
			var model = await context.Document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);
			if (model == null)
			{
				return;
			}

			var typeSyntax = fieldDecl.Declaration.Type;
			var typeSymbol = model.GetTypeInfo(typeSyntax, context.CancellationToken).Type as INamedTypeSymbol;
			if (typeSymbol == null)
			{
				return;
			}

			var interfaces = GetCandidateInterfaces(typeSymbol);
			RegisterInterfaceFixes(context, root, fieldDecl, typeSyntax, interfaces);
		}

		private static async Task OfferFixesForPropertyAsync(CodeFixContext context, SyntaxNode root, PropertyDeclarationSyntax propDecl)
		{
			var model = await context.Document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);
			if (model == null)
			{
				return;
			}

			var typeSyntax = propDecl.Type;
			var typeSymbol = model.GetTypeInfo(typeSyntax, context.CancellationToken).Type as INamedTypeSymbol;
			if (typeSymbol == null)
			{
				return;
			}

			var interfaces = GetCandidateInterfaces(typeSymbol);
			RegisterInterfaceFixes(context, root, propDecl, typeSyntax, interfaces);
		}

		private static IReadOnlyList<INamedTypeSymbol> GetCandidateInterfaces(INamedTypeSymbol typeSymbol)
		{
			// Only suggest *implemented* interfaces. You could filter system interfaces if desired.
			return typeSymbol.TypeKind == TypeKind.Interface
				? new List<INamedTypeSymbol>()
				: typeSymbol.AllInterfaces.Distinct<INamedTypeSymbol>(SymbolEqualityComparer.Default).ToList();
		}

		private static void RegisterInterfaceFixes(
			CodeFixContext context,
			SyntaxNode root,
			SyntaxNode ownerDecl,
			TypeSyntax typeSyntax,
			IReadOnlyList<INamedTypeSymbol> interfaces)
		{
			if (interfaces.Count == 0)
			{
				return;
			}

			foreach (var iface in interfaces)
			{
				var display = iface.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat); // global::Namespace.IInterface
				var newType = SyntaxFactory.ParseTypeName(display);

				var newOwner = ownerDecl switch
				{
					FieldDeclarationSyntax f => f.WithDeclaration(f.Declaration.WithType(newType)),
					PropertyDeclarationSyntax p => p.WithType(newType),
					_ => ownerDecl
				};

				var newRoot = root.ReplaceNode(ownerDecl, newOwner);
				var newDoc = context.Document.WithSyntaxRoot(newRoot);

				context.RegisterCodeFix(
					CodeAction.Create(
						title: $"Change type to interface '{iface.Name}'",
						createChangedDocument: ct => Task.FromResult(newDoc),
						equivalenceKey: $"SK001_ChangeType_{iface.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)}"),
					context.Diagnostics.First());
			}
		}
	}
}
