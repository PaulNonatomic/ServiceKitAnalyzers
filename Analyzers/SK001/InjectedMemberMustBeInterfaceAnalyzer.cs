using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace ServiceKit.Analyzers
{
	[DiagnosticAnalyzer(LanguageNames.CSharp)]
	public sealed class InjectedMemberMustBeInterfaceAnalyzer : DiagnosticAnalyzer
	{
		public const string DiagnosticId = "SK001";

		private static readonly DiagnosticDescriptor _rule = new(
			id: DiagnosticId,
			title: "Injected member should be an interface",
			messageFormat: "Member '{0}' with [InjectService] should be an interface type",
			category: "ServiceKit.Usage",
			defaultSeverity: DiagnosticSeverity.Warning,
			isEnabledByDefault: true,
			description: "ServiceKit recommends injecting interfaces to keep services loosely coupled and AOT-friendly."
		);

		public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(_rule);

		public override void Initialize(AnalysisContext context)
		{
			context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
			context.EnableConcurrentExecution();

			context.RegisterSymbolAction(AnalyzeField, SymbolKind.Field);
			context.RegisterSymbolAction(AnalyzeProperty, SymbolKind.Property);
		}

		private static void AnalyzeField(SymbolAnalysisContext ctx)
		{
			var field = (IFieldSymbol)ctx.Symbol;
			if (!HasInjectService(field))
			{
				return;
			}

			// Arrays, generics, classes ⇒ warn. Only pure interfaces are allowed.
			if (field.Type?.TypeKind != TypeKind.Interface)
			{
				var location = field.Locations.FirstOrDefault();
				if (location != null)
				{
					ctx.ReportDiagnostic(Diagnostic.Create(_rule, location, field.Name));
				}
			}
		}

		private static void AnalyzeProperty(SymbolAnalysisContext ctx)
		{
			var prop = (IPropertySymbol)ctx.Symbol;
			if (!HasInjectService(prop))
			{
				return;
			}

			if (prop.Type?.TypeKind != TypeKind.Interface)
			{
				var location = prop.Locations.FirstOrDefault();
				if (location != null)
				{
					ctx.ReportDiagnostic(Diagnostic.Create(_rule, location, prop.Name));
				}
			}
		}

		private static bool HasInjectService(ISymbol symbol)
		{
			foreach (var a in symbol.GetAttributes())
			{
				var attr = a.AttributeClass;
				if (attr == null)
				{
					continue;
				}

				// Be resilient to namespace changes / aliases
				if (attr.Name == "InjectServiceAttribute")
				{
					return true;
				}

				var full = attr.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
				if (full.EndsWith(".InjectServiceAttribute"))
				{
					return true;
				}
			}
			return false;
		}
	}
}
