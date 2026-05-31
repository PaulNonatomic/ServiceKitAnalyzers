using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ServiceKit.Analyzers
{
	[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(ServiceAttributeOnAbstractClassCodeFix))]
	[Shared]
	public sealed class ServiceAttributeOnAbstractClassCodeFix : CodeFixProvider
	{
		public override ImmutableArray<string> FixableDiagnosticIds =>
			ImmutableArray.Create(ServiceAttributeOnAbstractClassAnalyzer.DiagnosticId);

		public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

		public override async Task RegisterCodeFixesAsync(CodeFixContext context)
		{
			var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
			if (root == null)
			{
				return;
			}

			var diagnostic = context.Diagnostics.First();
			var node = root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true);

			var attribute = node.FirstAncestorOrSelf<AttributeSyntax>();
			if (attribute == null)
			{
				return;
			}

			context.RegisterCodeFix(
				CodeAction.Create(
					title: "Remove ineffective [Service] attribute",
					createChangedDocument: ct => RemoveAttributeAsync(context.Document, attribute, ct),
					equivalenceKey: "RemoveIneffectiveServiceAttribute"),
				diagnostic);
		}

		private static async Task<Document> RemoveAttributeAsync(Document document, AttributeSyntax attribute, CancellationToken ct)
		{
			var root = await document.GetSyntaxRootAsync(ct).ConfigureAwait(false);
			if (root == null || attribute.Parent is not AttributeListSyntax attributeList)
			{
				return document;
			}

			SyntaxNode? newRoot;
			if (attributeList.Attributes.Count == 1)
			{
				// [Service] is the only attribute in its list — remove the whole list (and its line).
				newRoot = root.RemoveNode(attributeList, SyntaxRemoveOptions.KeepNoTrivia);
			}
			else
			{
				// Other attributes share the list — drop only [Service].
				var newList = attributeList.WithAttributes(attributeList.Attributes.Remove(attribute));
				newRoot = root.ReplaceNode(attributeList, newList);
			}

			return newRoot == null ? document : document.WithSyntaxRoot(newRoot);
		}
	}
}
