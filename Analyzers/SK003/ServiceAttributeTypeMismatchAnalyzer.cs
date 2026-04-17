using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace ServiceKit.Analyzers
{
	[DiagnosticAnalyzer(LanguageNames.CSharp)]
	public sealed class ServiceAttributeTypeMismatchAnalyzer : DiagnosticAnalyzer
	{
		public const string DiagnosticId = "SK003";

		private static readonly DiagnosticDescriptor _rule = new(
			id: DiagnosticId,
			title: "Service attribute declares unimplemented type",
			messageFormat: "Class '{0}' has [Service(typeof({1}))] but does not implement '{1}'",
			category: "ServiceKit.Usage",
			defaultSeverity: DiagnosticSeverity.Error,
			isEnabledByDefault: true,
			description: "Types declared in the [Service] attribute must be implemented by the class. ServiceKit will throw at runtime if the class does not implement the declared type."
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
			if (type.TypeKind != TypeKind.Class)
			{
				return;
			}

			foreach (var attributeData in type.GetAttributes())
			{
				if (!IsServiceAttribute(attributeData.AttributeClass))
				{
					continue;
				}

				// Check each type argument passed to [Service(typeof(IFoo), typeof(IBar), ...)]
				foreach (var arg in attributeData.ConstructorArguments)
				{
					// The ServiceAttribute constructor takes params Type[] — could be a single value or an array
					if (arg.Kind == TypedConstantKind.Array)
					{
						foreach (var element in arg.Values)
						{
							CheckTypeArgument(ctx, type, element);
						}
					}
					else if (arg.Kind == TypedConstantKind.Type)
					{
						CheckTypeArgument(ctx, type, arg);
					}
				}
			}
		}

		private static void CheckTypeArgument(SymbolAnalysisContext ctx, INamedTypeSymbol classType, TypedConstant typeArg)
		{
			if (typeArg.Value is not INamedTypeSymbol declaredType)
			{
				return;
			}

			// Check if the class implements/extends the declared type
			if (declaredType.TypeKind == TypeKind.Interface)
			{
				if (!classType.AllInterfaces.Contains(declaredType, SymbolEqualityComparer.Default))
				{
					ReportDiagnostic(ctx, classType, declaredType);
				}
			}
			else if (declaredType.TypeKind == TypeKind.Class)
			{
				if (!IsOrInheritsFrom(classType, declaredType))
				{
					ReportDiagnostic(ctx, classType, declaredType);
				}
			}
		}

		private static void ReportDiagnostic(SymbolAnalysisContext ctx, INamedTypeSymbol classType, INamedTypeSymbol declaredType)
		{
			var location = classType.Locations.FirstOrDefault();
			if (location != null)
			{
				ctx.ReportDiagnostic(Diagnostic.Create(_rule, location, classType.Name, declaredType.Name));
			}
		}

		private static bool IsOrInheritsFrom(INamedTypeSymbol type, INamedTypeSymbol baseType)
		{
			var current = type;
			while (current != null)
			{
				if (SymbolEqualityComparer.Default.Equals(current, baseType))
				{
					return true;
				}
				current = current.BaseType;
			}
			return false;
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
