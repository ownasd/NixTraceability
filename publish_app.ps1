# This script publishes the NixTraceability application as a self-contained, single-file executable for Windows x64.

$projectName = "NixTraceability"
$projectPath = ".\NixTraceability\$projectName.csproj"
$outputDir = ".\Publish"

echo "Cleaning up old publish folder..."
if (Test-Path $outputDir) { Remove-Item -Recurse -Force $outputDir }

echo "Publishing $projectName..."

# Flags explained:
# -c Release: Build in Release mode (optimized)
# -r win-x64: Target 64-bit Windows
# --self-contained true: Bundle the .NET runtime with the app
# -p:PublishSingleFile=true: Bundle all DLLs into one EXE
# -p:IncludeNativeLibrariesForSelfExtract=true: Ensure SQLite native DLLs are extracted and used correctly
# -o $outputDir: Output to the 'Publish' folder

dotnet publish $projectPath `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -o $outputDir

if ($LASTEXITCODE -eq 0) {
    echo "----------------------------------------------------"
    echo "SUCCESS: Publication complete!"
    echo "The executable is located in: $outputDir"
    echo "----------------------------------------------------"
} else {
    echo "ERROR: Publication failed. Please check the output above."
}
