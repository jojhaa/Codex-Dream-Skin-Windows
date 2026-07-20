[CmdletBinding()]
param(
  [int]$Port = 9335,
  [switch]$NoShortcuts,
  [switch]$RepairLauncher
)

$ErrorActionPreference = 'Stop'
$PortExplicit = $PSBoundParameters.ContainsKey('Port')
$SkillRoot = Split-Path -Parent $PSScriptRoot
. (Join-Path $PSScriptRoot 'common-windows.ps1')
. (Join-Path $PSScriptRoot 'theme-windows.ps1')

$operationLock = Enter-DreamSkinOperationLock
try {
  Assert-DreamSkinPort -Port $Port
  $null = Get-DreamSkinNodeRuntime
  $registeredInstalls = @(Get-DreamSkinRegisteredCodexInstalls)
  if ($registeredInstalls.Count -eq 0) {
    throw 'The official OpenAI.Codex Store package is not installed or its identity cannot be validated.'
  }
  if (-not $RepairLauncher) {
    foreach ($registeredCodex in $registeredInstalls) {
      if ((Get-DreamSkinCodexProcesses -Codex $registeredCodex).Count -gt 0) {
        throw 'Close Codex before installing Dream Skin so config.toml cannot change during the transaction.'
      }
    }
  }

  $StateRoot = Join-Path $env:LOCALAPPDATA 'CodexDreamSkin'
  $themePaths = Get-DreamSkinThemePaths -StateRoot $StateRoot
  Ensure-DreamSkinManagedDirectory -Path $themePaths.Root -Root $themePaths.Root
  $StatePath = Join-Path $StateRoot 'state.json'
  $existingState = Read-DreamSkinState -Path $StatePath
  $savedPathCandidate = Get-DreamSkinCodexStatePathCandidate -State $existingState
  $savedCodex = Resolve-DreamSkinCodexInstallFromState -State $existingState -RegisteredInstalls $registeredInstalls
  if ($null -ne $savedPathCandidate -and $null -eq $savedCodex -and
    (Get-DreamSkinCodexProcesses -Codex $savedPathCandidate).Count -gt 0) {
    throw 'The saved Codex path is still running but no longer matches a registered Store package. Close it manually before installing.'
  }
  if (Test-DreamSkinTrayActive) {
    throw 'Exit the Codex Dream Skin tray before reinstalling so every shortcut can move to the new runtime safely.'
  }
  $engine = Install-DreamSkinRuntimeEngine -SkillRoot $SkillRoot -StateRoot $StateRoot
  $null = Initialize-DreamSkinThemeStore -SkillRoot $engine.Root -StateRoot $StateRoot
  if (-not $RepairLauncher) {
    $ConfigPath = Join-Path $HOME '.codex\config.toml'
    $BackupPath = Join-Path $StateRoot 'config.before-dream-skin.toml'
    Install-DreamSkinBaseTheme -ConfigPath $ConfigPath -BackupPath $BackupPath
  }

  if (-not $NoShortcuts) {
    $shell = New-Object -ComObject WScript.Shell
    $desktop = [Environment]::GetFolderPath('Desktop')
    $startMenu = Join-Path $env:APPDATA 'Microsoft\Windows\Start Menu\Programs'
    $powershell = (Get-Command powershell.exe -ErrorAction Stop).Source
    $startScript = $engine.Start
    $restoreScript = $engine.Restore
    $portArgument = if ($PortExplicit) { " -Port $Port" } else { '' }

    foreach ($folder in @($desktop, $startMenu)) {
      @(
        'Codex Dream Skin.lnk',
        'Codex Dream Skin - Tray.lnk',
        'Codex Kanna Blue Skin - Tray.lnk'
      ) | ForEach-Object {
        Remove-Item -LiteralPath (Join-Path $folder $_) -Force -ErrorAction SilentlyContinue
      }
    }
    Remove-Item -LiteralPath (Join-Path $desktop 'Codex Dream Skin - Restore.lnk') -Force -ErrorAction SilentlyContinue

    foreach ($folder in @($desktop, $startMenu)) {
      $shortcut = $shell.CreateShortcut((Join-Path $folder 'Codex Kanna Blue Skin.lnk'))
      $shortcut.TargetPath = $powershell
      $shortcut.Arguments = "-NoProfile -ExecutionPolicy RemoteSigned -File `"$startScript`"$portArgument -PromptRestart"
      $shortcut.WorkingDirectory = $engine.Root
      $shortcut.Description = 'Launch the official Codex app with Codex Dream Skin'
      $shortcut.IconLocation = "$($registeredInstalls[0].Executable),0"
      $shortcut.Save()
    }

    $restore = $shell.CreateShortcut((Join-Path $desktop 'Codex Kanna Blue Skin - Restore.lnk'))
    $restore.TargetPath = $powershell
    $restore.Arguments = "-NoProfile -ExecutionPolicy RemoteSigned -File `"$restoreScript`"$portArgument -RestoreBaseTheme -PromptRestart"
    $restore.WorkingDirectory = $engine.Root
    $restore.Description = 'Restore the official Codex appearance and close the CDP session'
    $restore.IconLocation = "$($registeredInstalls[0].Executable),0"
    $restore.Save()

  }

  if ($RepairLauncher) {
    Write-Host "Codex Kanna Blue Skin launcher repaired at $($engine.Root)."
  } elseif ($NoShortcuts) {
    Write-Host "Codex Kanna Blue Skin base theme installed at $($engine.Root). Run $($engine.Start) to launch it."
  } else {
    Write-Host 'Codex Kanna Blue Skin installed. Use its launch shortcut after every full app exit.'
  }
} finally {
  Exit-DreamSkinOperationLock -Mutex $operationLock
}
