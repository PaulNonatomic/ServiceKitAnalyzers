using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace ServiceKit.Analyzers
{
	[DiagnosticAnalyzer(LanguageNames.CSharp)]
	public sealed class ServiceAttributeOnAbstractClassAnalyzer : DiagnosticAnalyzer
	{
		public const string DiagnosticId = "SK006";

		private static readonly DiagnosticDescriptor _rule = new(
			id: DiagnosticId,
			title: "Service attribute on abstract class has no effect",
			messageFormat: "Abstract class '{0}' has [Service] but the attribute is not inherited; place [Service] on each concrete subclass that should register as the service",
			category: "ServiceKit.Usage",
			defaultSeverity: DiagnosticSeverity.Warning,
			isEnabledByDefault: true,
			description: "ServiceKit's [Service] attribute is declared with Inherited = false and an abstract class is never instantiated, so [Service] on an abstract class never registers anything. ServiceKitBehaviour reads the attribute via GetType().GetCustomAttribute<ServiceAttribute>() on the concrete instance, which will not see an attribute placed on a base class. Move [Service(typeof(...))] onto each concrete subclass that ServiceKit instantiates."
		);

		public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(_rule);

		public override void Initialize(AnalysisContext context)
		{
			context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
			context.EnableConcurrentExecution();

			context.RegisterSymbolAction(AnalyzeNamedType, SymbolKind.NamedType);
		}

		private static void AnalyzeNamedType(SymbolAnalysisContext ctx)
		{
			var type = (INamedTypeSymbol)ctx.Symbol;
			if (type.TypeKind != TypeKind.Class || !type.IsAbstract)
			{
				return;
			}

			foreach (var attributeData in type.GetAttributes())
			{
				if (!IsServiceAttribute(attributeData.AttributeClass))
				{
					continue;
				}

				var location = attributeData.ApplicationSyntaxReference?.GetSyntax(ctx.CancellationToken).GetLocation()
				               ?? type.Locations.FirstOrDefault();
				if (location != null)
				{
					ctx.ReportDiagnostic(Diagnostic.Create(_rule, location, type.Name));
				}
			}
		}

		private static bool IsServiceAttribute(INamedTypeSymbol? attr)
		{
			if (attr == null)
			{
				return false;
			}

			if (attr.Name == "ServiceAttribute")
			{
				return true;
			}

			var full = attr.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
			return full.EndsWith(".ServiceAttribute");
		}
	}
}
