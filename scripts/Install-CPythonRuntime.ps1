param(
    [string]$Version = "3.11.9",
    [string]$Destination = "$PSScriptRoot\..\src\PythonMod\Runtime"
)

$ErrorActionPreference = "Stop"

$destinationPath = Resolve-Path -LiteralPath (New-Item -ItemType Directory -Force -Path $Destination)
$zipName = "python-$Version-embed-amd64.zip"
$url = "https://www.python.org/ftp/python/$Version/$zipName"
$zipPath = Join-Path ([System.IO.Path]::GetTempPath()) $zipName

Write-Host "Downloading $url"
Invoke-WebRequest -Uri $url -OutFile $zipPath

Write-Host "Extracting to $destinationPath"
Expand-Archive -LiteralPath $zipPath -DestinationPath $destinationPath -Force

$pthPath = Join-Path $destinationPath "python311._pth"
if (Test-Path -LiteralPath $pthPath) {
    $content = Get-Content -LiteralPath $pthPath
    $content = $content | ForEach-Object {
        if ($_ -eq "#import site") { "import site" } else { $_ }
    }
    if ($content -notcontains "Lib") {
        $content += "Lib"
    }
    if ($content -notcontains "Lib\site-packages") {
        $content += "Lib\site-packages"
    }
    Set-Content -LiteralPath $pthPath -Value $content -Encoding ASCII
}

New-Item -ItemType Directory -Force -Path (Join-Path $destinationPath "Lib\site-packages") | Out-Null
Write-Host "CPython $Version runtime installed."
