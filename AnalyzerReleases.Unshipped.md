; Unshipped analyzer release
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
SK006 | ServiceKit.Usage | Warning | ServiceAttributeOnAbstractClassAnalyzer — [Service] on an abstract class has no effect (not inherited); place it on concrete subclasses.
