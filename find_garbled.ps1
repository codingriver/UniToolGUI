$files = Get-ChildItem -Recurse -Filter '*.cs' 'D:\ProxyTool\Assets\Scripts\Core'
foreach ($file in $files) {
    $lines = [System.IO.File]::ReadAllLines($file.FullName, [System.Text.Encoding]::UTF8)
    $lineNum = 0
    foreach ($line in $lines) {
        $lineNum++
        # Look for lines with Chinese text that end with truncated characters
        # Pattern: Chinese text followed by garbled end (replacement char U+FFFD or lone ?)
        $hasChinese = $line -match '[\u4E00-\u9FFF]'
        $hasGarbled = $line -match '\ufffd' -or ($line -match '[\u4E00-\u9FFF].*\?\)' -and $line -notmatch 'null \?') -or ($line -match '[\u4E00-\u9FFF].*\?\"' -and $line -notmatch '\?\?') -or ($line -match '[\u4E00-\u9FFF].*\?;')
        if ($hasChinese -and $hasGarbled) {
            Write-Host ("{0}:{1}: {2}" -f $file.Name, $lineNum, $line.Trim())
        }
    }
}
