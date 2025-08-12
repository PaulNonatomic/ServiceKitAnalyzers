using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ServiceKit.Analyzers
{
	[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(InjectedFieldVisibilityCodeFix))]
	[Shared]
	public sealed class InjectedFieldVisibilityCodeFix : CodeFixProvider
	{
		public override ImmutableArray<string> FixableDiagnosticIds =>
			ImmutableArray.Create(InjectedFieldVisibilityAnalyzer.DiagnosticId);

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
			var fieldDecl = node.FirstAncestorOrSelf<FieldDeclarationSyntax>();
			if (fieldDecl == null)
			{
				return;
			}

			context.RegisterCodeFix(
				CodeAction.Create(
					"Make field private, instance, mutable, and remove [SerializeField]",
					ct => ApplyAsync(context.Document, root, fieldDecl, ct),
					"SK002_Fix"),
				diagnostic);
		}

		private static Task<Document> ApplyAsync(Document document, SyntaxNode root, FieldDeclarationSyntax fieldDecl, CancellationToken ct)
		{
			// 1) private
			var newModifiers = SyntaxFactory.TokenList(SyntaxFactory.Token(SyntaxKind.PrivateKeyword));

			// 2) remove static/readonly if present
			// (we rebuild the modifiers list instead of filtering to enforce exactly 'private')
			var cleanedField = fieldDecl.WithModifiers(newModifiers);

			// 3) remove [SerializeField] attributes if present
			if (cleanedField.AttributeLists.Count > 0)
			{
				var newLists = new SyntaxList<AttributeListSyntax>();
				foreach (var list in cleanedField.AttributeLists)
				{
					var keptAttrs = list.Attributes.Where(a =>
					{
						var name = a.Name.ToString();
						if (name.EndsWith("SerializeField") || name == "SerializeField")
						{
							return false;
						}
						return true;
					}).ToList();

					if (keptAttrs.Count > 0)
					{
						newLists = newLists.Add(list.WithAttributes(SyntaxFactory.SeparatedList(keptAttrs)));
					}
				}
				cleanedField = cleanedField.WithAttributeLists(newLists);
			}

			var newRoot = root.ReplaceNode(fieldDecl, cleanedField);
			return Task.FromResult(document.WithSyntaxRoot(newRoot));
		}
	}
}
