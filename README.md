
# Building ServiceKit.Analyzers

## Development Build
For local development and testing:
```
dotnet build
```
Output: `bin/Debug/netstandard2.0/ServiceKit.Analyzers.dll`

## Release Build
For production release:
```
dotnet build -c Release
```
Output: `bin/Release/netstandard2.0/ServiceKit.Analyzers.dll`

## Copy to Output Directory
To copy the analyzer DLL to a specific output directory:
```
dotnet build -c Release -o <output-directory>
```

The analyzer DLL can then be referenced by projects that need ServiceKit analysis.
