using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace ServiceKit.Analyzers
{
	[DiagnosticAnalyzer(LanguageNames.CSharp)]
	public sealed class MissingBaseAwakeCallAnalyzer : DiagnosticAnalyzer
	{
		public const string DiagnosticId = "SK005";

		private static readonly DiagnosticDescriptor _rule = new(
			id: DiagnosticId,
			title: "ServiceKitBehaviour.Awake override must call base.Awake()",
			messageFormat: "'{0}' overrides Awake() but does not call base.Awake(). ServiceKit will not register or inject this service.",
			category: "ServiceKit.Usage",
			defaultSeverity: DiagnosticSeverity.Error,
			isEnabledByDefault: true,
			description: "ServiceKitBehaviour uses Awake() to register the service, inject dependencies, and mark it as ready. Overriding Awake() without calling base.Awake() will prevent the service lifecycle from executing."
		);

		public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(_rule);

		public override void Initialize(AnalysisContext context)
		{
			context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
			context.EnableConcurrentExecution();

			context.RegisterSyntaxNodeAction(AnalyzeMethodDeclaration, SyntaxKind.MethodDeclaration);
		}

		private static void AnalyzeMethodDeclaration(SyntaxNodeAnalysisContext ctx)
		{
			var method = (MethodDeclarationSyntax)ctx.Node;

			// Only care about methods named "Awake"
			if (method.Identifier.Text != "Awake")
			{
				return;
			}

			// Must be an override (protected override void Awake)
			if (!method.Modifiers.Any(SyntaxKind.OverrideKeyword))
			{
				return;
			}

			// Must have a body or expression body
			if (method.Body == null && method.ExpressionBody == null)
			{
				return;
			}

			// Check if the containing class inherits from ServiceBehaviour
			var classDecl = method.FirstAncestorOrSelf<ClassDeclarationSyntax>();
			if (classDecl == null)
			{
				return;
			}

			var model = ctx.SemanticModel;
			var classSymbol = model.GetDeclaredSymbol(classDecl, ctx.CancellationToken) as INamedTypeSymbol;
			if (classSymbol == null)
			{
				return;
			}

			if (!InheritsFromServiceBehaviour(classSymbol))
			{
				return;
			}

			// Check if the method body contains a call to base.Awake()
			if (ContainsBaseAwakeCall(method))
			{
				return;
			}

			ctx.ReportDiagnostic(Diagnostic.Create(_rule, method.Identifier.GetLocation(), classSymbol.Name));
		}

		private static bool InheritsFromServiceBehaviour(INamedTypeSymbol classSymbol)
		{
			var current = classSymbol.BaseType;
			while (current != null)
			{
				if (current.Name == "ServiceBehaviour")
				{
					return true;
				}

				// Also check the old name for backwards compatibility
				if (current.Name == "ServiceKitBehaviour")
				{
					return true;
				}

				current = current.BaseType;
			}
			return false;
		}

		private static bool ContainsBaseAwakeCall(MethodDeclarationSyntax method)
		{
			// Check body block
			if (method.Body != null)
			{
				return method.Body.DescendantNodes()
					.OfType<InvocationExpressionSyntax>()
					.Any(IsBaseAwakeInvocation);
			}

			// Check expression body (e.g., protected override void Awake() => base.Awake();)
			if (method.ExpressionBody != null)
			{
				return method.ExpressionBody.DescendantNodes()
					.OfType<InvocationExpressionSyntax>()
					.Any(IsBaseAwakeInvocation);
			}

			return false;
		}

		private static bool IsBaseAwakeInvocation(InvocationExpressionSyntax invocation)
		{
			// Looking for: base.Awake()
			if (invocation.Expression is MemberAccessExpressionSyntax memberAccess)
			{
				return memberAccess.Expression is BaseExpressionSyntax &&
					   memberAccess.Name.Identifier.Text == "Awake";
			}

			return false;
		}
	}
}
