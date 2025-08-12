using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace ServiceKit.Analyzers
{
	[DiagnosticAnalyzer(LanguageNames.CSharp)]
	public sealed class PreferExecuteWithCancellationAnalyzer : DiagnosticAnalyzer
	{
		public const string DiagnosticId = "SK010";

		private static readonly DiagnosticDescriptor _rule = new(
			id: DiagnosticId,
			title: "Prefer ExecuteWithCancellationAsync(...)",
			messageFormat: "Prefer '.ExecuteWithCancellationAsync(token)' over '.WithCancellation(token).ExecuteAsync()'",
			category: "ServiceKit.Async",
			defaultSeverity: DiagnosticSeverity.Info,
			isEnabledByDefault: true,
			description: "Use the convenience wrapper for consistent injection cancellation.");

		public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(_rule);

		public override void Initialize(AnalysisContext context)
		{
			context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
			context.EnableConcurrentExecution();
			context.RegisterSyntaxNodeAction(AnalyzeInvocation, SyntaxKind.InvocationExpression);
		}

		private static void AnalyzeInvocation(SyntaxNodeAnalysisContext ctx)
		{
			if (ctx.Node is not InvocationExpressionSyntax executeInvoke) return;
			if (executeInvoke.Expression is not MemberAccessExpressionSyntax executeAccess) return;

			var terminal = executeAccess.Name.Identifier.ValueText;

			// If they already use the wrapper, do nothing.
			if (terminal == "ExecuteWithCancellationAsync") return;
			if (terminal != "ExecuteAsync") return;

			// Walk chain to ensure it starts from InjectServicesAsync and contains WithCancellation
			var hasWithCancellation = false;
			var startedWithInject = false;

			ExpressionSyntax? cursor = executeAccess.Expression;
			while (cursor is InvocationExpressionSyntax inv && inv.Expression is MemberAccessExpressionSyntax ma)
			{
				var name = ma.Name.Identifier.ValueText;
				if (name == "WithCancellation") hasWithCancellation = true;
				if (name == "InjectServicesAsync") startedWithInject = true;
				cursor = ma.Expression;
			}

			if (!startedWithInject)
			{
				if (cursor is InvocationExpressionSyntax rootInv &&
					rootInv.Expression is MemberAccessExpressionSyntax rootMa &&
					rootMa.Name.Identifier.ValueText == "InjectServicesAsync")
				{
					startedWithInject = true;
				}
			}

			if (startedWithInject && hasWithCancellation)
			{
				ctx.ReportDiagnostic(Diagnostic.Create(_rule, executeAccess.Name.Identifier.GetLocation()));
			}
		}
	}
}
