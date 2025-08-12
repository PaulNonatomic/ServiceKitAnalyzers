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
	[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(PreferExecuteWithCancellationCodeFix))]
	[Shared]
	public sealed class PreferExecuteWithCancellationCodeFix : CodeFixProvider
	{
		public override ImmutableArray<string> FixableDiagnosticIds =>
			ImmutableArray.Create(PreferExecuteWithCancellationAnalyzer.DiagnosticId);

		public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

		public override async Task RegisterCodeFixesAsync(CodeFixContext context)
		{
			var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
			if (root == null) return;

			var diagnostic = context.Diagnostics.First();
			var node = root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true);

			if (node is not IdentifierNameSyntax executeIdent) return;
			if (executeIdent.Parent is not MemberAccessExpressionSyntax executeAccess) return;

			context.RegisterCodeFix(
				CodeAction.Create(
					"Use '.ExecuteWithCancellationAsync(...)' and remove '.WithCancellation(...)'",
					ct =>
					{
						var oldInvocation = executeAccess.Parent as InvocationExpressionSyntax;
						if (oldInvocation == null) return Task.FromResult(context.Document);

						// 1) Find the WithCancellation(...) invocation and capture its argument list.
						if (!TryFindWithCancellationInvocation(executeAccess.Expression, out var withCancelInv, out var withCancelMember))
						{
							// Nothing to do (defensive).
							return Task.FromResult(context.Document);
						}

						var tokenArgs = withCancelInv.ArgumentList; // reuse same arguments

						// 2) Remove the WithCancellation(...) call from the left-hand chain.
						var leftSansCancel = RemoveFirstWithCancellation(executeAccess.Expression);

						// 3) Replace ExecuteAsync with ExecuteWithCancellationAsync and pass the captured args.
						var newAccess = executeAccess.WithExpression(leftSansCancel)
							.WithName(SyntaxFactory.IdentifierName("ExecuteWithCancellationAsync"));

						var newInvocation = oldInvocation.WithExpression(newAccess)
							.WithArgumentList(tokenArgs);

						var newRoot = root.ReplaceNode(oldInvocation, newInvocation);
						return Task.FromResult(context.Document.WithSyntaxRoot(newRoot));
					},
					"SK010_UseWrapper"),
				diagnostic);
		}

		private static bool TryFindWithCancellationInvocation(
			ExpressionSyntax start,
			out InvocationExpressionSyntax withCancelInvocation,
			out MemberAccessExpressionSyntax withCancelMember)
		{
			withCancelInvocation = default!;
			withCancelMember = default!;

			ExpressionSyntax? cursor = start;

			while (cursor is InvocationExpressionSyntax inv &&
				   inv.Expression is MemberAccessExpressionSyntax ma)
			{
				if (ma.Name.Identifier.ValueText == "WithCancellation")
				{
					withCancelInvocation = inv;
					withCancelMember = ma;
					return true;
				}

				cursor = ma.Expression;
			}

			return false;
		}

		private static ExpressionSyntax RemoveFirstWithCancellation(ExpressionSyntax expr)
		{
			// Recursively rebuild the chain, skipping the first WithCancellation level we encounter.
			if (expr is InvocationExpressionSyntax inv && inv.Expression is MemberAccessExpressionSyntax ma)
			{
				if (ma.Name.Identifier.ValueText == "WithCancellation")
				{
					// Drop this level by returning its receiver (ma.Expression)
					return ma.Expression;
				}

				var rebuiltReceiver = RemoveFirstWithCancellation(ma.Expression);
				var newMember = ma.WithExpression(rebuiltReceiver);
				return inv.WithExpression(newMember);
			}

			return expr;
		}
	}
}
