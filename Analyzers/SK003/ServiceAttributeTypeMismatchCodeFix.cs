using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;

namespace ServiceKit.Analyzers
{
	[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(ServiceAttributeTypeMismatchCodeFix))]
	[Shared]
	public sealed class ServiceAttributeTypeMismatchCodeFix : CodeFixProvider
	{
		public override ImmutableArray<string> FixableDiagnosticIds =>
			ImmutableArray.Create(ServiceAttributeTypeMismatchAnalyzer.DiagnosticId);

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

			var classDecl = node.FirstAncestorOrSelf<ClassDeclarationSyntax>();
			if (classDecl == null)
			{
				return;
			}

			var model = await document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);
			if (model == null)
			{
				return;
			}

			var classSymbol = model.GetDeclaredSymbol(classDecl, context.CancellationToken) as INamedTypeSymbol;
			if (classSymbol == null)
			{
				return;
			}

			// Find unimplemented types from the [Service] attribute
			foreach (var attributeData in classSymbol.GetAttributes())
			{
				if (attributeData.AttributeClass?.Name != "ServiceAttribute")
				{
					continue;
				}

				foreach (var arg in attributeData.ConstructorArguments)
				{
					var typeArgs = arg.Kind == TypedConstantKind.Array
						? arg.Values.Where(v => v.Value is INamedTypeSymbol).Select(v => (INamedTypeSymbol)v.Value!)
						: arg.Value is INamedTypeSymbol t ? new[] { t } : Enumerable.Empty<INamedTypeSymbol>();

					foreach (var declaredType in typeArgs)
					{
						if (declaredType.TypeKind == TypeKind.Interface &&
							!classSymbol.AllInterfaces.Contains(declaredType, SymbolEqualityComparer.Default))
						{
							var interfaceName = declaredType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
							var fullName = declaredType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

							context.RegisterCodeFix(
								CodeAction.Create(
									title: $"Implement interface '{interfaceName}'",
									createChangedDocument: ct =>
									{
										var baseList = classDecl.BaseList ?? SyntaxFactory.BaseList();
										var newBaseType = SyntaxFactory.SimpleBaseType(SyntaxFactory.ParseTypeName(fullName));
										var newBaseList = baseList.AddTypes(newBaseType);
										var newClassDecl = classDecl.WithBaseList(newBaseList);
										var newRoot = root.ReplaceNode(classDecl, newClassDecl);
										return Task.FromResult(document.WithSyntaxRoot(newRoot));
									},
									equivalenceKey: $"SK003_Implement_{interfaceName}"),
								diagnostic);
						}
					}
				}
			}
		}
	}
}
