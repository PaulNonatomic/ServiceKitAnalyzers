using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace ServiceKit.Analyzers
{
	[DiagnosticAnalyzer(LanguageNames.CSharp)]
	public sealed class InjectionMustIncludeCancellationAnalyzer : DiagnosticAnalyzer
	{
		public const string DiagnosticId = "SK004";

		private static readonly DiagnosticDescriptor _rule = new(
			id: DiagnosticId,
			title: "Injection chain should specify a destroy cancellation token",
			messageFormat: "Call '.WithCancellation(destroyCancellationToken)' or use '.ExecuteWithCancellationAsync(destroyCancellationToken)'",
			category: "ServiceKit.Async",
			defaultSeverity: DiagnosticSeverity.Warning,
			isEnabledByDefault: true);

		public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(_rule);

		public override void Initialize(AnalysisContext context)
		{
			context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
			context.EnableConcurrentExecution();
			context.RegisterSyntaxNodeAction(AnalyzeInvocation, SyntaxKind.InvocationExpression);
		}

		private static void AnalyzeInvocation(SyntaxNodeAnalysisContext ctx)
		{
			if (ctx.Node is not InvocationExpressionSyntax invoke) return;
			if (invoke.Expression is not MemberAccessExpressionSyntax access) return;

			var terminal = access.Name.Identifier.ValueText;

			// If they already call the safe wrapper, we're done.
			if (terminal == "ExecuteWithCancellationAsync") return;

			if (terminal != "ExecuteAsync") return;

			var (startedWithInject, hasWithCancellation) = WalkChain(access.Expression);
			if (startedWithInject && !hasWithCancellation)
			{
				ctx.ReportDiagnostic(Diagnostic.Create(_rule, access.Name.Identifier.GetLocation()));
			}
		}

		private static (bool startedWithInject, bool hasWithCancellation) WalkChain(ExpressionSyntax expr)
		{
			var started = false;
			var hasCancel = false;

			ExpressionSyntax? cursor = expr;

			while (cursor is InvocationExpressionSyntax inv && inv.Expression is MemberAccessExpressionSyntax ma)
			{
				var name = ma.Name.Identifier.ValueText;
				if (name == "InjectServicesAsync") started = true;
				if (name == "WithCancellation") hasCancel = true;
				cursor = ma.Expression;
			}

			// Handle minimal chain: _loc.InjectServicesAsync(...).ExecuteAsync()
			if (!started && cursor is InvocationExpressionSyntax rootInv &&
				rootInv.Expression is MemberAccessExpressionSyntax rootMa &&
				rootMa.Name.Identifier.ValueText == "InjectServicesAsync")
			{
				started = true;
			}

			return (started, hasCancel);
		}
	}
}
