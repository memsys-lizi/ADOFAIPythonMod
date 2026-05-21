param(
    [string]$Version = "3.8.10",
    [string]$Destination = "$PSScriptRoot\..\src\PythonMod\Runtime"
)

$ErrorActionPreference = "Stop"

$destinationPath = Resolve-Path -LiteralPath (New-Item -ItemType Directory -Force -Path $Destination)
$zipName = "python-$Version-embed-amd64.zip"
$url = "https://www.python.org/ftp/python/$Version/$zipName"
$zipPath = Join-Path ([System.IO.Path]::GetTempPath()) $zipName
$majorMinor = (($Version -split '\.')[0..1] -join '')

Write-Host "Downloading $url"
Invoke-WebRequest -Uri $url -OutFile $zipPath

Write-Host "Extracting to $destinationPath"
Get-ChildItem -LiteralPath $destinationPath -Force | Where-Object { $_.Name -ne ".gitkeep" } | Remove-Item -Recurse -Force
Expand-Archive -LiteralPath $zipPath -DestinationPath $destinationPath -Force

$pthPath = Join-Path $destinationPath "python$majorMinor._pth"
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
