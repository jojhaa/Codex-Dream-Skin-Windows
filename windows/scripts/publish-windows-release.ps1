[CmdletBinding()]
param(
  [string]$Version = '0.3.6',
  [switch]$KeepStaging
)

$ErrorActionPreference = 'Stop'

$WindowsRoot = Split-Path -Parent $PSScriptRoot
$RepositoryRoot = Split-Path -Parent $WindowsRoot
$Project = Join-Path $WindowsRoot 'app\CodexDreamSkin\CodexDreamSkin.csproj'
$ReleaseRoot = Join-Path $RepositoryRoot 'release'
$PortableName = "CodexDreamSkin-Windows-x64-v$Version-Portable"
$LiteName = "CodexDreamSkin-Windows-x64-v$Version-Lite"
$PortableStage = Join-Path $ReleaseRoot $PortableName
$LiteStage = Join-Path $ReleaseRoot $LiteName
$PortableZip = Join-Path $ReleaseRoot "$PortableName.zip"
$LiteZip = Join-Path $ReleaseRoot "$LiteName.zip"

function Remove-ReleasePath {
  param([Parameter(Mandatory = $true)][string]$Path)

  $releaseFullPath = [System.IO.Path]::GetFullPath($ReleaseRoot).TrimEnd('\') + '\'
  $targetFullPath = [System.IO.Path]::GetFullPath($Path)
  if (-not $targetFullPath.StartsWith($releaseFullPath, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "Refusing to remove a path outside the release directory: $targetFullPath"
  }
  if (Test-Path -LiteralPath $targetFullPath) {
    Remove-Item -LiteralPath $targetFullPath -Recurse -Force
  }
}

function Invoke-Publish {
  param(
    [Parameter(Mandatory = $true)][string]$Output,
    [Parameter(Mandatory = $true)][string]$ModeProperty,
    [Parameter(Mandatory = $true)][bool]$SelfContained
  )

  $selfContainedValue = $SelfContained.ToString().ToLowerInvariant()
  $arguments = @(
    'publish',
    $Project,
    '-c', 'Release',
    '-r', 'win-x64',
    '-p:Platform=x64',
    "-p:$ModeProperty=true",
    "-p:SelfContained=$selfContainedValue",
    "-p:WindowsAppSDKSelfContained=$selfContainedValue",
    '-p:PublishReadyToRun=false',
    '-p:PublishTrimmed=false',
    '-p:DebugSymbols=false',
    '-p:DebugType=None',
    '-o', $Output,
    '-nologo',
    '-v', 'minimal'
  )
  & dotnet @arguments
  if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed for $ModeProperty with exit code $LASTEXITCODE"
  }
  if (-not (Test-Path -LiteralPath (Join-Path $Output 'CodexDreamSkin.exe') -PathType Leaf)) {
    throw "Published executable is missing from $Output"
  }
}

New-Item -ItemType Directory -Path $ReleaseRoot -Force | Out-Null
foreach ($path in @($PortableStage, $LiteStage, $PortableZip, $LiteZip)) {
  Remove-ReleasePath -Path $path
}

Invoke-Publish -Output $PortableStage -ModeProperty 'PortableExe' -SelfContained $true
Copy-Item -LiteralPath (Join-Path $WindowsRoot 'package\README-PORTABLE.txt') `
  -Destination (Join-Path $PortableStage 'README.txt')

Invoke-Publish -Output $LiteStage -ModeProperty 'LiteExe' -SelfContained $false
Copy-Item -LiteralPath (Join-Path $WindowsRoot 'package\README-LITE.txt') `
  -Destination (Join-Path $LiteStage 'README.txt')

Compress-Archive -Path (Join-Path $PortableStage '*') `
  -DestinationPath $PortableZip -CompressionLevel Optimal
Compress-Archive -Path (Join-Path $LiteStage '*') `
  -DestinationPath $LiteZip -CompressionLevel Optimal

$artifacts = foreach ($zip in @($PortableZip, $LiteZip)) {
  $file = Get-Item -LiteralPath $zip
  [pscustomobject]@{
    Name = $file.Name
    Bytes = $file.Length
    Sha256 = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash
  }
}

$artifacts | Format-Table -AutoSize

if (-not $KeepStaging) {
  Remove-ReleasePath -Path $PortableStage
  Remove-ReleasePath -Path $LiteStage
}
