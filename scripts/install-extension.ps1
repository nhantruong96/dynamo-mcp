# Builds the DynamoMCP view extension and installs it as a user-level Dynamo package
# (no admin rights needed). Dynamo loads *_ViewExtensionDefinition.xml files found in a
# package's "extra" folder.
#
#   .\scripts\install-extension.ps1                            # Dynamo for Revit 4.1 (Revit 2027)
#   .\scripts\install-extension.ps1 -DynamoVersion 3.4         # another Dynamo minor version
#   .\scripts\install-extension.ps1 -HostName "Dynamo Core"    # Dynamo Sandbox
#   .\scripts\install-extension.ps1 -SkipBuild
param(
    [string]$DynamoVersion = "4.1",
    [string]$HostName = "Dynamo Revit",
    [string]$Configuration = "Release",
    [string]$DynamoDir = "",
    [switch]$SkipBuild
)

$ErrorActionPreference = "Stop"
$root = Split-Path $PSScriptRoot -Parent
$proj = Join-Path $root "extension\DynamoMcpExtension.csproj"

if (-not $SkipBuild) {
    $buildArgs = @("build", $proj, "-c", $Configuration, "-nologo")
    if ($DynamoDir -ne "") { $buildArgs += "-p:DynamoDir=$DynamoDir" }
    & dotnet @buildArgs
    if ($LASTEXITCODE -ne 0) { throw "dotnet build failed" }
}

$dll = Get-ChildItem -Path (Join-Path $root "extension\bin\$Configuration") -Recurse -Filter "DynamoMcpExtension.dll" | Select-Object -First 1
if ($null -eq $dll) { throw "DynamoMcpExtension.dll not found under extension\bin\$Configuration - build first" }

$dest = Join-Path $env:APPDATA "Dynamo\$HostName\$DynamoVersion\packages\DynamoMCP"
New-Item -ItemType Directory -Force -Path (Join-Path $dest "bin"), (Join-Path $dest "extra") | Out-Null
Copy-Item $dll.FullName (Join-Path $dest "bin") -Force
Copy-Item (Join-Path $root "extension\package\pkg.json") $dest -Force
Copy-Item (Join-Path $root "extension\package\extra\DynamoMcp_ViewExtensionDefinition.xml") (Join-Path $dest "extra") -Force

Write-Host "Installed DynamoMCP package to:" -ForegroundColor Green
Write-Host "  $dest"
Write-Host "Restart Dynamo. The 'Extensions' menu should show 'Dynamo MCP: ON (127.0.0.1:8555)'."
Write-Host "Log file: $env:LOCALAPPDATA\DynamoMCP\extension.log"
