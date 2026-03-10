$files = Get-ChildItem -Recurse -Filter '*.cs' 'D:\ProxyTool\Assets\Scripts\Core'
$totalGarbled = 0
foreach ($file in $files) {
    $content = [System.IO.File]::ReadAllText($file.FullName, [System.Text.Encoding]::UTF8)
    $lines = $content -split "`n"
    $lineNum = 0
    $fileGarbled = 0
    foreach ($line in $lines) {
        $lineNum++
        # Check for replacement char or truncated Chinese (char followed by ?)
        if ($line -match '\ufffd' -or ($line -match '[\x{4E00}-\x{9FFF}]\?' -and $line -notmatch 'null\s*\?' -and $line -notmatch '\?\?' -and $line -notmatch '\?\s*:' -and $line -notmatch '\?\.' -and $line -notmatch '\?>')) {
            Write-Host ("{0}:{1}: {2}" -f $file.Name, $lineNum, $line.Trim())
            $fileGarbled++
            $totalGarbled++
        }
    }
}
Write-Host ""
Write-Host "Total garbled lines remaining: $totalGarbled"
