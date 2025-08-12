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
	[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(InjectionMustIncludeCancellationCodeFix))]
	[Shared]
	public sealed class InjectionMustIncludeCancellationCodeFix : CodeFixProvider
	{
		public override ImmutableArray<string> FixableDiagnosticIds =>
			ImmutableArray.Create(InjectionMustIncludeCancellationAnalyzer.DiagnosticId);

		public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

		public override async Task RegisterCodeFixesAsync(CodeFixContext context)
		{
			var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
			if (root == null) return;

			var diagnostic = context.Diagnostics.First();
			var node = root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true);

			if (node is not IdentifierNameSyntax executeIdent) return;
			if (executeIdent.Parent is not MemberAccessExpressionSyntax executeAccess) return;

			// Fix A: Insert .WithCancellation(destroyCancellationToken)
			context.RegisterCodeFix(
				CodeAction.Create(
					"Add '.WithCancellation(destroyCancellationToken)'",
					ct =>
					{
						var left = executeAccess.Expression;
						var withCancelInvocation =
							SyntaxFactory.InvocationExpression(
								SyntaxFactory.MemberAccessExpression(
									SyntaxKind.SimpleMemberAccessExpression,
									left,
									SyntaxFactory.IdentifierName("WithCancellation")),
								SyntaxFactory.ArgumentList(
									SyntaxFactory.SingletonSeparatedList(
										SyntaxFactory.Argument(SyntaxFactory.IdentifierName("destroyCancellationToken")))));

						var newAccess = executeAccess.WithExpression(withCancelInvocation);
						var oldInvocation = executeAccess.Parent as InvocationExpressionSyntax;
						var newInvocation = oldInvocation?.WithExpression(newAccess);
						var newRoot = root.ReplaceNode(oldInvocation!, newInvocation!);
						return Task.FromResult(context.Document.WithSyntaxRoot(newRoot));
					},
					"SK004_AddWithCancellation"),
				diagnostic);

			// Fix B: Switch to ExecuteWithCancellationAsync(destroyCancellationToken)
			context.RegisterCodeFix(
				CodeAction.Create(
					"Use '.ExecuteWithCancellationAsync(destroyCancellationToken)'",
					ct =>
					{
						var oldInvocation = executeAccess.Parent as InvocationExpressionSyntax;
						if (oldInvocation == null) return Task.FromResult(context.Document);

						var newAccess = executeAccess.WithName(SyntaxFactory.IdentifierName("ExecuteWithCancellationAsync"));
						var newInvocation = oldInvocation.WithExpression(newAccess)
							.WithArgumentList(
								SyntaxFactory.ArgumentList(
									SyntaxFactory.SingletonSeparatedList(
										SyntaxFactory.Argument(SyntaxFactory.IdentifierName("destroyCancellationToken")))));

						var newRoot = root.ReplaceNode(oldInvocation, newInvocation);
						return Task.FromResult(context.Document.WithSyntaxRoot(newRoot));
					},
					"SK004_UseExecuteWithCancellationAsync"),
				diagnostic);
		}
	}
}
