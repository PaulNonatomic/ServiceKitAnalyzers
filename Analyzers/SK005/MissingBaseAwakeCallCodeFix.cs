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
	[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(MissingBaseAwakeCallCodeFix))]
	[Shared]
	public sealed class MissingBaseAwakeCallCodeFix : CodeFixProvider
	{
		public override ImmutableArray<string> FixableDiagnosticIds =>
			ImmutableArray.Create(MissingBaseAwakeCallAnalyzer.DiagnosticId);

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

			var method = node.FirstAncestorOrSelf<MethodDeclarationSyntax>();
			if (method?.Body == null)
			{
				return;
			}

			// Determine if Awake is async (async void Awake)
			var isAsync = method.Modifiers.Any(SyntaxKind.AsyncKeyword);

			// Build the base.Awake() call statement
			var baseAwakeCall = SyntaxFactory.ExpressionStatement(
				SyntaxFactory.InvocationExpression(
					SyntaxFactory.MemberAccessExpression(
						SyntaxKind.SimpleMemberAccessExpression,
						SyntaxFactory.BaseExpression(),
						SyntaxFactory.IdentifierName("Awake")
					)
				)
			);

			// If async, wrap in await: await base.Awake(); — but Awake returns void, so no await needed.
			// base.Awake() is async void, so we just call it directly regardless.
			var callStatement = baseAwakeCall
				.WithLeadingTrivia(GetBodyIndentation(method.Body))
				.NormalizeWhitespace()
				.WithTrailingTrivia(SyntaxFactory.CarriageReturnLineFeed);

			// Add base.Awake() as the first statement in the method body
			var newBody = method.Body.WithStatements(method.Body.Statements.Insert(0, callStatement));
			var newMethod = method.WithBody(newBody);
			var newRoot = root.ReplaceNode(method, newMethod);

			context.RegisterCodeFix(
				CodeAction.Create(
					title: "Add base.Awake() call",
					createChangedDocument: ct => Task.FromResult(document.WithSyntaxRoot(newRoot)),
					equivalenceKey: "SK005_AddBaseAwake"),
				diagnostic);
		}

		private static SyntaxTriviaList GetBodyIndentation(BlockSyntax body)
		{
			// Try to match the indentation of existing statements
			var firstStatement = body.Statements.FirstOrDefault();
			if (firstStatement != null)
			{
				return firstStatement.GetLeadingTrivia();
			}

			// Fall back to the body's own indentation plus extra whitespace
			return SyntaxFactory.TriviaList(SyntaxFactory.Whitespace("\t\t\t"));
		}
	}
}
