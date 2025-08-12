using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace ServiceKit.Analyzers
{
	[DiagnosticAnalyzer(LanguageNames.CSharp)]
	public sealed class InjectedFieldVisibilityAnalyzer : DiagnosticAnalyzer
	{
		public const string DiagnosticId = "SK002";

		private static readonly DiagnosticDescriptor _rule = new(
			id: DiagnosticId,
            title: "Injected field should be private, non-static, non-readonly, and not [SerializeField]",
            messageFormat: "Field '{0}' with [InjectService] should be private, instance, mutable, and not [SerializeField]",
            category: "ServiceKit.Usage",
            defaultSeverity: DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: "ServiceKit injects into instance fields; keep them private (avoid inspector), non-static, non-readonly, and do not mark with [SerializeField].");

		public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(_rule);

		public override void Initialize(AnalysisContext context)
		{
			context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
			context.EnableConcurrentExecution();
			context.RegisterSymbolAction(AnalyzeField, SymbolKind.Field);
		}

		private static void AnalyzeField(SymbolAnalysisContext ctx)
		{
			var field = (IFieldSymbol)ctx.Symbol;
			if (!HasInjectService(field))
			{
				return;
			}

			var hasSerializeField = HasSerializeField(field);
			var isBadAccess = field.DeclaredAccessibility != Accessibility.Private;
			var isStatic = field.IsStatic;
			var isReadonly = field.IsReadOnly;

			if (!hasSerializeField && !isBadAccess && !isStatic && !isReadonly)
			{
				return;
			}

			var loc = field.Locations.FirstOrDefault();
			if (loc == null)
			{
				return;
			}

			ctx.ReportDiagnostic(Diagnostic.Create(_rule, loc, field.Name));
		}

		private static bool HasInjectService(ISymbol symbol) =>
			symbol.GetAttributes().Any(a => a.AttributeClass?.Name == "InjectServiceAttribute"
				|| a.AttributeClass?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
					?.EndsWith(".InjectServiceAttribute") == true);

		private static bool HasSerializeField(ISymbol symbol) =>
			symbol.GetAttributes().Any(a => a.AttributeClass?.Name == "SerializeField"
				|| a.AttributeClass?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
					?.EndsWith(".SerializeField") == true);
	}
}
