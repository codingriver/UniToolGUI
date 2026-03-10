$files = Get-ChildItem -Recurse -Filter '*.cs' 'D:\ProxyTool\Assets\Scripts\Core'

foreach ($file in $files) {
    $bytes = [System.IO.File]::ReadAllBytes($file.FullName)
    
    # Check if starts with UTF-8 BOM
    $hasBom = ($bytes.Length -ge 3 -and $bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF)
    
    # Try to detect mojibake: read as Windows-1252, then check if re-encoding as UTF-8 produces valid Chinese
    $content1252 = [System.Text.Encoding]::GetEncoding('Windows-1252').GetString($bytes)
    $contentUtf8 = [System.Text.Encoding]::UTF8.GetString($bytes)
    
    # Check for common mojibake pattern
    $hasMojibake = $contentUtf8 -match '[\u9300-\u93FF]|[\u59A0-\u59FF]|[\u935B-\u935F]'
    
    Write-Host "$($file.Name): BOM=$hasBom, Mojibake=$hasMojibake, Size=$($bytes.Length)"
}
