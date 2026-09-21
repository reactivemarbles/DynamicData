[CmdletBinding()]
param(
    [string]$WorkflowPath = (Join-Path $PSScriptRoot '..\workflows\release.yml')
)

$ErrorActionPreference = 'Stop'
$workflow = Get-Content -LiteralPath $WorkflowPath -Raw
$command = [regex]::Match($workflow, '(?m)^[ \t]+run:[ \t]+(git-release-notes[^\r\n]+)\r?$')
if (-not $command.Success) {
    throw 'The release workflow must contain an inspectable release-notes invocation.'
}

$expectedCommit = '1111111111111111111111111111111111111111'
$expectedVersion = '9.5.0'
$originalCommit = $env:GITHUB_SHA
$originalVersion = $env:RELEASE_VERSION
$env:GITHUB_SHA = $expectedCommit
$env:RELEASE_VERSION = $expectedVersion

function git-release-notes {
    $headIndex = [Array]::IndexOf($args, '--head-ref')
    if ($headIndex -lt 0 -or $headIndex + 1 -ge $args.Count -or $args[$headIndex + 1] -ne $expectedCommit) {
        throw 'Release notes must compare against the commit being published, not the repository default branch.'
    }

    $versionIndex = [Array]::IndexOf($args, '--release-version')
    if ($versionIndex -lt 0 -or $versionIndex + 1 -ge $args.Count -or $args[$versionIndex + 1] -ne $expectedVersion) {
        throw 'Release notes must use the version being published.'
    }

    $outputIndex = [Array]::IndexOf($args, '--output-file')
    if ($outputIndex -lt 0 -or $outputIndex + 1 -ge $args.Count -or $args[$outputIndex + 1] -ne 'release-notes.md') {
        throw 'Release notes must be written to the file consumed by the release step.'
    }
}

try {
    & ([scriptblock]::Create($command.Groups[1].Value))
}
finally {
    $env:GITHUB_SHA = $originalCommit
    $env:RELEASE_VERSION = $originalVersion
    Remove-Item -LiteralPath Function:\git-release-notes
}

Write-Output 'Release-notes invocation uses the published commit, version, and output file.'
