param(
    [string]$OutputDirectory = ''
)

$ErrorActionPreference = 'Stop'

$projectRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path $projectRoot 'artifacts\hera-cursor'
} elseif (-not [IO.Path]::IsPathRooted($OutputDirectory)) {
    $OutputDirectory = Join-Path $projectRoot $OutputDirectory
}

New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null

$cli = Get-Command 'hera-agent-unity' -ErrorAction SilentlyContinue
if ($cli) {
    $cliPath = $cli.Source
} else {
    $cliPath = Join-Path $env:LOCALAPPDATA 'JCSoft\HeraAgentUnity\bin\hera-agent-unity.exe'
}

if (-not (Test-Path -LiteralPath $cliPath)) {
    throw "hera-agent-unity was not found. Expected: $cliPath"
}

function Invoke-Hera {
    param(
        [string[]]$Arguments,
        [string]$OutputFile,
        [switch]$AllowFailure
    )

    $previousErrorActionPreference = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    try {
        $result = @(& $cliPath @Arguments 2>&1)
    } finally {
        $ErrorActionPreference = $previousErrorActionPreference
    }
    $exitCode = $LASTEXITCODE
    $lines = foreach ($item in $result) {
        if ($item -is [System.Management.Automation.ErrorRecord]) {
            $item.Exception.Message
        } else {
            $item.ToString()
        }
    }
    $text = ($lines | Out-String).Trim()
    Set-Content -LiteralPath (Join-Path $OutputDirectory $OutputFile) -Value $text -Encoding UTF8

    if ($exitCode -ne 0 -and -not $AllowFailure) {
        throw "Hera command failed ($exitCode): hera-agent-unity $($Arguments -join ' ')`n$text"
    }

    return $text
}

$statusText = Invoke-Hera @('status') 'status.txt' -AllowFailure
$sceneText = Invoke-Hera @('scene', 'info', '--compact-json') 'scene.json' -AllowFailure
$consoleText = Invoke-Hera @('console', '--type', 'error', '--lines', '50', '--compact-json') 'console-errors.json' -AllowFailure
$toolsText = Invoke-Hera @('list', '--compact') 'tools.json' -AllowFailure

$sceneImage = Join-Path $OutputDirectory 'scene_view.png'
$gameImage = Join-Path $OutputDirectory 'game_view.png'
Invoke-Hera @('screenshot', '--view', 'scene', '--width', '1280', '--height', '720', '--output_path', $sceneImage, '--compact-json') 'scene-screenshot.json' -AllowFailure | Out-Null
Invoke-Hera @('screenshot', '--view', 'game', '--width', '1280', '--height', '720', '--output_path', $gameImage, '--compact-json') 'game-screenshot.json' -AllowFailure | Out-Null

$scene = $null
try {
    if ($sceneText.TrimStart().StartsWith('{')) {
        $scene = $sceneText | ConvertFrom-Json
    }
} catch {
    $scene = $null
}

$console = $null
try {
    if ($consoleText.TrimStart().StartsWith('{')) {
        $console = $consoleText | ConvertFrom-Json
    }
} catch {
    $console = $null
}

$activeScene = if ($scene -and $scene.active) { "$($scene.active.name) ($($scene.active.path))" } else { '확인되지 않음' }
$rootCount = if ($scene -and $scene.loaded -and $scene.loaded.Count -gt 0) { $scene.loaded[0].rootCount } else { '확인되지 않음' }
$errorCount = if ($console -and $null -ne $console.matched) { $console.matched } else { '확인되지 않음' }
$generatedAt = Get-Date -Format 'yyyy-MM-dd HH:mm:ss K'

$context = @"
# Unity Live Context

Generated: $generatedAt
Project root: $projectRoot
CLI: $cliPath

## Connection

Read status.txt for the full Hera status. The current status output is:

--- STATUS START ---
$statusText
--- STATUS END ---

## Active scene

- Active scene: $activeScene
- Root count: $rootCount
- Scene JSON: scene.json

## Console

- Matched Console errors: $errorCount
- Details: console-errors.json

## Screenshots

- Scene View: scene_view.png
- Game View: game_view.png

These are point-in-time snapshots. Run tools/hera/capture-unity-context.ps1 again after Unity or code changes before relying on them.
"@

Set-Content -LiteralPath (Join-Path $OutputDirectory 'context.md') -Value $context -Encoding UTF8
Write-Output "Unity context written to: $OutputDirectory"
