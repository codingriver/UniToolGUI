# Fix mojibake in all .cs files
# Damage: UTF-8 BOM -> Get-Content (decoded as GBK/CP936) -> Set-Content -Encoding UTF8
# Reverse: read current UTF-8 -> encode to GBK bytes -> decode as UTF-8

$gbk = [System.Text.Encoding]::GetEncoding(936)
$utf8bom = New-Object System.Text.UTF8Encoding($true)
$utf8 = New-Object System.Text.UTF8Encoding($false)

$files = Get-ChildItem -Recurse -Filter '*.cs' 'D:\ProxyTool\Assets\Scripts\Core'
$fixed = 0
$failed = 0

foreach ($file in $files) {
    $bytes = [System.IO.File]::ReadAllBytes($file.FullName)
    
    # Skip BOM if present
    $startIdx = 0
    if ($bytes.Length -ge 3 -and $bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF) {
        $startIdx = 3
    }
    
    $contentBytes = $bytes[$startIdx..($bytes.Length - 1)]
    $content = $utf8.GetString($contentBytes)
    
    # Check if has mojibake (Chinese chars in ranges that indicate double-encoding)
    if ($content -match '[\u9300-\u93FF]|[\u6DC7]|[\u5B2A]') {
        # Reverse the double-encoding
        $gbkBytes = $gbk.GetBytes($content)
        $recovered = $utf8.GetString($gbkBytes)
        
        # Write back with UTF-8 BOM
        [System.IO.File]::WriteAllText($file.FullName, $recovered, $utf8bom)
        $fixed++
        Write-Host "FIXED: $($file.Name)"
    } else {
        Write-Host "SKIP: $($file.Name) (no mojibake detected)"
    }
}

Write-Host ""
Write-Host "Fixed: $fixed files"
