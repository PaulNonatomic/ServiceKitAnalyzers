<div align=center>   

<p align="center">
  <img src="Readme~\logo.png">
</p>

</div>

# ServiceKit.Analyzers

Roslyn analyzers and code fixers to enforce ServiceKit best practices in Unity projects. These analyzers help developers write better, more maintainable code when using ServiceKit's dependency injection framework.

## Overview

ServiceKit.Analyzers provides compile-time validation of dependency injection patterns, ensuring:
- Proper interface-based injection for better testability and AOT compatibility
- Correct field visibility and modifiers for injected services
- Proper cancellation token usage in async injection chains
- Consistent coding patterns across your ServiceKit implementation

## Analyzers and Code Fixers

### SK001: Injected Member Must Be Interface

**Severity:** Warning  
**Category:** ServiceKit.Usage

#### Description
Enforces that members decorated with `[InjectService]` attribute should be interface types rather than concrete classes. This promotes loose coupling, better testability, and AOT-friendly code.

#### Example
```csharp
// ❌ Bad - Using concrete class
[InjectService] private MyService _service;

// ✅ Good - Using interface
[InjectService] private IMyService _service;
```

#### Code Fixer
The code fixer automatically suggests all interfaces implemented by the concrete type, allowing you to quickly change the member type to an appropriate interface.

---

### SK002: Injected Field Visibility

**Severity:** Warning  
**Category:** ServiceKit.Usage

#### Description
Ensures that fields with `[InjectService]` attribute follow ServiceKit's requirements:
- Must be `private` (to avoid Unity inspector exposure)
- Must be instance fields (not `static`)
- Must be mutable (not `readonly`)
- Must not have `[SerializeField]` attribute

#### Example
```csharp
// ❌ Bad - Public, static, readonly, or serialized
[InjectService] public IMyService Service;
[InjectService] static IMyService _service;
[InjectService] readonly IMyService _service;
[SerializeField][InjectService] private IMyService _service;

// ✅ Good - Private, instance, mutable field
[InjectService] private IMyService _service;
```

#### Code Fixer
Automatically corrects field modifiers to make them private, instance, mutable, and removes any `[SerializeField]` attributes.

---

### SK004: Injection Must Include Cancellation

**Severity:** Warning  
**Category:** ServiceKit.Async

#### Description
Ensures that async injection chains specify a destroy cancellation token to properly handle Unity object lifecycle. This prevents memory leaks and ensures clean shutdown when GameObjects are destroyed.

#### Example
```csharp
// ❌ Bad - Missing cancellation token
await _locator.InjectServicesAsync(gameObject)
    .ExecuteAsync();

// ✅ Good - With cancellation token
await _locator.InjectServicesAsync(gameObject)
    .WithCancellation(destroyCancellationToken)
    .ExecuteAsync();

// ✅ Also good - Using convenience method
await _locator.InjectServicesAsync(gameObject)
    .ExecuteWithCancellationAsync(destroyCancellationToken);
```

#### Code Fixer
Offers two fixes:
1. Add `.WithCancellation(destroyCancellationToken)` to the chain
2. Replace `.ExecuteAsync()` with `.ExecuteWithCancellationAsync(destroyCancellationToken)`

---

### SK010: Prefer ExecuteWithCancellationAsync

**Severity:** Info  
**Category:** ServiceKit.Async

#### Description
Suggests using the convenience method `ExecuteWithCancellationAsync` instead of the longer `.WithCancellation().ExecuteAsync()` pattern for cleaner, more consistent code.

#### Example
```csharp
// ⚠️ Works but verbose
await _locator.InjectServicesAsync(gameObject)
    .WithCancellation(destroyCancellationToken)
    .ExecuteAsync();

// ✅ Preferred - Cleaner syntax
await _locator.InjectServicesAsync(gameObject)
    .ExecuteWithCancellationAsync(destroyCancellationToken);
```

#### Code Fixer
Automatically refactors the code to use the convenience method, removing the `.WithCancellation()` call and replacing `.ExecuteAsync()` with `.ExecuteWithCancellationAsync()`.

## Building ServiceKit.Analyzers

### Development Build
For local development and testing:
```
dotnet build
```
Output: `bin/Debug/netstandard2.0/ServiceKit.Analyzers.dll`

### Release Build
For production release:
```
dotnet build -c Release
```
Output: `bin/Release/netstandard2.0/ServiceKit.Analyzers.dll`

### Copy to Output Directory
To copy the analyzer DLL to a specific output directory:
```
dotnet build -c Release -o <output-directory>
```

The analyzer DLL can then be referenced by projects that need ServiceKit analysis.

## Integration with ServiceKit

These analyzers are designed to work seamlessly with ServiceKit's dependency injection system in Unity. They help enforce best practices such as:

- **Interface-based injection:** Promotes testability and allows for easy mocking/substitution
- **Proper field configuration:** Ensures injected fields work correctly with ServiceKit's injection mechanism
- **Lifecycle management:** Ensures proper cancellation token usage to prevent memory leaks
- **Code consistency:** Promotes use of standardized patterns across your codebase

## Technical Details

- **Target Framework:** .NET Standard 2.0 for broad compatibility
- **Language Version:** Latest C# with nullable reference types enabled
- **Dependencies:** Microsoft.CodeAnalysis.CSharp 4.10.0
- **Package Type:** Development dependency (no runtime impact)
