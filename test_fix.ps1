# Fix mojibake caused by: UTF-8 BOM -> Get-Content (read as GBK/CP936) -> Set-Content -Encoding UTF8
# Reverse: read UTF-8 -> encode as GBK -> those bytes are the original UTF-8

$gbk = [System.Text.Encoding]::GetEncoding(936)
$utf8 = New-Object System.Text.UTF8Encoding($true)  # with BOM

$testFile = 'D:\ProxyTool\Assets\Scripts\Core\Commands\CheckCommand.cs'
$content = [System.IO.File]::ReadAllText($testFile, [System.Text.Encoding]::UTF8)

# Reverse the double-encoding
$gbkBytes = $gbk.GetBytes($content)
$recovered = [System.Text.Encoding]::UTF8.GetString($gbkBytes)

# Print first 15 lines to verify
$lines = $recovered -split "`n"
for ($i = 0; $i -lt [Math]::Min(15, $lines.Length); $i++) {
    Write-Host $lines[$i]
}
