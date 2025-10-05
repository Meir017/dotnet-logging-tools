<#
.SYNOPSIS
    Removes broken program entries from the Windows registry.

.DESCRIPTION
    Scans common registry locations for program entries and removes those that reference
    non-existent files or folders. Supports a DryRun mode to preview changes without
    making modifications.

.PARAMETER DryRun
    When specified, the script will only report what would be removed without actually
    making any changes to the registry.

.EXAMPLE
    .\Remove-BrokenRegistryEntries.ps1 -DryRun
    Preview what would be removed without making changes.

.EXAMPLE
    .\Remove-BrokenRegistryEntries.ps1
    Actually remove broken registry entries.
#>

[CmdletBinding()]
param(
    [Parameter()]
    [switch]$DryRun
)

# Requires elevation
# Requires -RunAsAdministrator

Write-Host "=== Broken Registry Entry Cleanup ===" -ForegroundColor Cyan
Write-Host ""

if ($DryRun) {
    Write-Host "*** DRY RUN MODE - No changes will be made ***" -ForegroundColor Yellow
    Write-Host ""
}

# Registry paths to check for program entries
$registryPaths = @(
    # Uninstall entries (32-bit and 64-bit)
    "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
    "HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall",
    "HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",

    # App Paths
    "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths",
    "HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\App Paths",

    # Run entries
    "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Run",
    "HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Run",
    "HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\Run"
)

$stats = @{
    TotalChecked = 0
    BrokenFound = 0
    Removed = 0
    Errors = 0
}

function Test-PathExists {
    param([string]$Path)

    if ([string]::IsNullOrWhiteSpace($Path)) {
        return $false
    }

    # Remove quotes
    $Path = $Path.Trim('"')

    # Extract executable path from command line (remove arguments)
    if ($Path -match '^([^/]*\.exe|[^/]*\.msi|[^/]*\.dll)') {
        $Path = $matches[1].Trim()
    }

    # Expand environment variables
    try {
        $Path = [System.Environment]::ExpandEnvironmentVariables($Path)
    }
    catch {
        return $false
    }

    # Check if path exists
    return (Test-Path -LiteralPath $Path -ErrorAction SilentlyContinue)
}

function Get-RelevantPaths {
    param($RegistryKey)

    $paths = @()

    # Common properties that contain file paths
    $pathProperties = @(
        'DisplayIcon',
        'InstallLocation',
        'UninstallString',
        'QuietUninstallString',
        'ModifyPath',
        'RepairPath',
        '(Default)'  # For App Paths
    )

    foreach ($prop in $pathProperties) {
        try {
            $value = $RegistryKey.GetValue($prop)
            if ($null -ne $value -and $value -is [string] -and -not [string]::IsNullOrWhiteSpace($value)) {
                $paths += @{
                    Property = $prop
                    Path = $value
                }
            }
        }
        catch {
            # Property doesn't exist, continue
        }
    }

    return $paths
}

Write-Host "Scanning registry paths..." -ForegroundColor Cyan
Write-Host ""

foreach ($regPath in $registryPaths) {
    if (-not (Test-Path $regPath)) {
        Write-Host "Skipping (not found): $regPath" -ForegroundColor Gray
        continue
    }

    Write-Host "Checking: $regPath" -ForegroundColor White

    try {
        $subKeys = Get-ChildItem -Path $regPath -ErrorAction SilentlyContinue

        foreach ($subKey in $subKeys) {
            $stats.TotalChecked++

            $keyPath = $subKey.PSPath
            $keyName = $subKey.PSChildName

            # Get registry key object to read properties
            $regKey = Get-ItemProperty -Path $keyPath -ErrorAction SilentlyContinue

            if ($null -eq $regKey) {
                continue
            }

            # Get all relevant paths from this key
            $relevantPaths = Get-RelevantPaths -RegistryKey $subKey

            if ($relevantPaths.Count -eq 0) {
                continue
            }

            # Check if any path exists
            $anyPathExists = $false
            $brokenPaths = @()

            foreach ($pathInfo in $relevantPaths) {
                $pathExists = Test-PathExists -Path $pathInfo.Path

                if ($pathExists) {
                    $anyPathExists = $true
                }
                else {
                    $brokenPaths += $pathInfo
                }
            }

            # If no paths exist, this is a broken entry
            if (-not $anyPathExists -and $brokenPaths.Count -gt 0) {
                $stats.BrokenFound++

                $displayName = $regKey.DisplayName
                if ([string]::IsNullOrWhiteSpace($displayName)) {
                    $displayName = $keyName
                }

                Write-Host "  [BROKEN] $displayName" -ForegroundColor Red
                Write-Host "    Registry: $keyPath" -ForegroundColor Gray

                foreach ($broken in $brokenPaths) {
                    Write-Host "    Missing [$($broken.Property)]: $($broken.Path)" -ForegroundColor DarkRed
                }

                if (-not $DryRun) {
                    try {
                        Remove-Item -Path $keyPath -Recurse -Force -ErrorAction Stop
                        Write-Host "    ✓ Removed" -ForegroundColor Green
                        $stats.Removed++
                    }
                    catch {
                        Write-Host "    ✗ Failed to remove: $($_.Exception.Message)" -ForegroundColor Red
                        $stats.Errors++
                    }
                }
                else {
                    Write-Host "    [DRY RUN] Would remove this entry" -ForegroundColor Yellow
                }

                Write-Host ""
            }
        }
    }
    catch {
        Write-Host "  Error scanning path: $($_.Exception.Message)" -ForegroundColor Red
        $stats.Errors++
    }
}

Write-Host ""
Write-Host "=== Summary ===" -ForegroundColor Cyan
Write-Host "Total entries checked: $($stats.TotalChecked)" -ForegroundColor White
Write-Host "Broken entries found:  $($stats.BrokenFound)" -ForegroundColor $(if ($stats.BrokenFound -gt 0) { "Yellow" } else { "Green" })

if ($DryRun) {
    Write-Host "Entries that would be removed: $($stats.BrokenFound)" -ForegroundColor Yellow
}
else {
    Write-Host "Entries removed:       $($stats.Removed)" -ForegroundColor Green
    if ($stats.Errors -gt 0) {
        Write-Host "Errors encountered:    $($stats.Errors)" -ForegroundColor Red
    }
}

Write-Host ""

if ($DryRun) {
    Write-Host "To actually remove these entries, run the script without -DryRun flag" -ForegroundColor Yellow
}
else {
    Write-Host "Cleanup complete!" -ForegroundColor Green
}
