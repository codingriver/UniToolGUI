param(
  [string]$unityPkgPath = "D:\UniToolGUI\UnityPackage\Gate",
  [string]$gateCoreCsproj = "D:\UniToolGUI\AIGate\src\Gate.Core\Gate.Core.csproj",
  [string]$gateCliCsproj = "D:\UniToolGUI\AIGate\src\Gate.CLI\Gate.CLI.csproj"
)

function Get-GateDllPath([string]$csprojPath, [string]$targetFramework) {
  $projDir = Split-Path $csprojPath
  $binPath = Join-Path $projDir (Join-Path "bin" (Join-Path "Release" $targetFramework))
  if (Test-Path $binPath) {
    $dlls = Get-ChildItem -Path $binPath -Filter "*.dll" -File
    if ($dlls.Count -gt 0) { return $dlls[0].FullName }
  }
  return $null
}

# Ensure output folder exists
if (-Not (Test-Path $unityPkgPath)) {
  New-Item -ItemType Directory -Force -Path $unityPkgPath | Out-Null
}

Write-Host "Building Gate.Core for DLL..."
dotnet build `
  "$gateCoreCsproj" -c Release | Out-Null
$gateCoreDll = Get-GateDllPath -csprojPath $gateCoreCsproj -targetFramework "netstandard2.0"
if ($gateCoreDll) {
  Copy-Item $gateCoreDll -Destination (Join-Path $unityPkgPath "Gate.Core.dll") -Force
  Write-Host "Copied Gate.Core.dll to Unity package: $([IO.Path]::Combine($unityPkgPath,'Gate.Core.dll'))"
} else {
  Write-Warning "Gate.Core.dll not found after build."
}

Write-Host "Building Gate.CLI for DLL (and EXE)..."
dotnet build `
  "$gateCliCsproj" -c Release | Out-Null

# Gate.CLI outputs DLL(s) in net8.0; copy the first DLL found as the Unity consumable
$gateCliBin = Split-Path (Join-Path (Split-Path $gateCliCsproj) "bin\Release\net8.0") -Parent
$gateCliDll = $null
if (Test-Path $gateCliBin) {
  $dlls = Get-ChildItem -Path $gateCliBin -Filter "*.dll" -File
  if ($dlls.Count -gt 0) { $gateCliDll = $dlls[0].FullName }
}
if ($gateCliDll) {
  Copy-Item $gateCliDll -Destination (Join-Path $unityPkgPath "Gate.CLI.dll") -Force
  Write-Host "Copied Gate.CLI.dll to Unity package: $([IO.Path]::Combine($unityPkgPath,'Gate.CLI.dll'))"
} else {
  Write-Warning "Gate.CLI.dll not found after build."
}

Write-Host "Gate packaging complete. Unity package path: $unityPkgPath"
