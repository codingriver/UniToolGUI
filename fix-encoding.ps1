# fix-encoding.ps1 - Convert all source files to UTF-8 (no BOM)
$extensions = @('*.cs', '*.uxml', '*.uss', '*.json', '*.md')
$searchPaths = @(
    'Assets\Scripts\AIGate',
    'Assets\UI\AIGate',
    'AIGate\src\Gate.Core\UI',
    'AIGate\src\Gate.CLI',
    'AIGate\docs'
)
$utf8NoBom = New-Object System.Text.UTF8Encoding $false
$count = 0
foreach ($searchPath in $searchPaths) {
    if (-not (Test-Path $searchPath)) { continue }
    foreach ($ext in $extensions) {
        Get-ChildItem -Path $searchPath -Filter $ext -Recurse | ForEach-Object {
            $file = $_.FullName
            try {
                $content = [System.IO.File]::ReadAllText($file)
                [System.IO.File]::WriteAllText($file, $content, $utf8NoBom)
                Write-Host ("[OK] " + $_.Name) -ForegroundColor Green
                $count++
            } catch {
                Write-Host ("[FAIL] " + $_.Name + ": " + $_) -ForegroundColor Red
            }
        }
    }
}
Write-Host ("") 
Write-Host ("Done: " + $count + " files converted to UTF-8 no-BOM") -ForegroundColor Cyan
