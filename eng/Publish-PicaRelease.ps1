[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^\d+\.\d+\.\d+$')]
    [string]$Version,

    [Parameter(Mandatory = $true)]
    [ValidatePattern('^[A-Fa-f0-9]{40}$')]
    [string]$CertificateThumbprint,

    [string]$PreparedApplicationDirectory,

    [string]$ReleaseNotesPath,

    [switch]$PublishToGitHub
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

Add-Type -AssemblyName System.IO.Compression.FileSystem

$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$buildPropertiesPath = Join-Path $repositoryRoot 'Directory.Build.props'
[xml]$buildProperties = Get-Content -LiteralPath $buildPropertiesPath
$repositoryUrl = [string]$buildProperties.Project.PropertyGroup.PicaRepositoryUrl

if ([string]::IsNullOrWhiteSpace($repositoryUrl)) {
    throw "PicaRepositoryUrl is missing from '$buildPropertiesPath'."
}

$projectPath = Join-Path $repositoryRoot 'src\Pica.Desktop\Pica.Desktop.csproj'
$publishDirectory = if ([string]::IsNullOrWhiteSpace($PreparedApplicationDirectory)) {
    Join-Path $repositoryRoot 'publish\Pica.Desktop\WindowsLocal'
}
else {
    [System.IO.Path]::GetFullPath($PreparedApplicationDirectory)
}
$releaseDirectory = Join-Path $repositoryRoot 'Releases'
$iconPath = Join-Path $repositoryRoot 'src\Pica.Viewer\Assets\AppIcon.ico'
$installerProjectPath = Join-Path $repositoryRoot 'eng\installer\Pica.Installer.csproj'
$installerBuildDirectory = Join-Path $repositoryRoot 'publish\Pica.Installer'
$installerPath = Join-Path $releaseDirectory 'Pica-win-Setup.exe'
$normalizedThumbprint = $CertificateThumbprint.ToUpperInvariant()
$certificatePath = "Cert:\CurrentUser\My\$normalizedThumbprint"
$certificate = Get-Item -LiteralPath $certificatePath -ErrorAction Stop

if (-not $certificate.HasPrivateKey) {
    throw "Certificate '$normalizedThumbprint' does not have an accessible private key."
}

if ($certificate.NotAfter -le (Get-Date)) {
    throw "Certificate '$normalizedThumbprint' has expired."
}

if (-not (Test-Path -LiteralPath $releaseDirectory -PathType Container)) {
    New-Item -ItemType Directory -Path $releaseDirectory | Out-Null
}

function Invoke-DotNet {
    param(
        [Parameter(Mandatory = $true)]
        [string[]]$Arguments
    )

    $previousDotNetCliUiLanguage = $env:DOTNET_CLI_UI_LANGUAGE

    try {
        $env:DOTNET_CLI_UI_LANGUAGE = 'en-US'
        & dotnet @Arguments
        $exitCode = $LASTEXITCODE
    }
    finally {
        $env:DOTNET_CLI_UI_LANGUAGE = $previousDotNetCliUiLanguage
    }

    if ($exitCode -ne 0) {
        throw "dotnet command failed with exit code $exitCode."
    }
}

function Assert-NoNewerPicaRelease {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Directory,

        [Parameter(Mandatory = $true)]
        [version]$RequestedVersion
    )

    $fullPackages = @(
        Get-ChildItem `
            -LiteralPath $Directory `
            -Filter 'Pica-*-full.nupkg' `
            -File
    )

    foreach ($fullPackage in $fullPackages) {
        $versionMatch = [regex]::Match(
            $fullPackage.Name,
            '^Pica-(?<version>\d+\.\d+\.\d+)-full\.nupkg$',
            [System.Text.RegularExpressions.RegexOptions]::IgnoreCase)

        if (-not $versionMatch.Success) {
            continue
        }

        $existingVersion = [version]::Parse($versionMatch.Groups['version'].Value)

        if ($existingVersion -gt $RequestedVersion) {
            throw "Release directory '$Directory' contains newer version '$existingVersion'. Increase the requested version above it."
        }
    }
}

function Get-SignToolPath {
    $programFilesX86 = [Environment]::GetFolderPath(
        [Environment+SpecialFolder]::ProgramFilesX86)
    $windowsSdkBinDirectory = Join-Path `
        $programFilesX86 `
        'Windows Kits\10\bin'

    if (-not (Test-Path -LiteralPath $windowsSdkBinDirectory -PathType Container)) {
        throw 'Windows SDK was not found. Install the Windows SDK signing tools.'
    }

    $signToolPaths = @(
        Get-ChildItem `
            -LiteralPath $windowsSdkBinDirectory `
            -Directory |
            Sort-Object -Property Name -Descending |
            ForEach-Object {
                Join-Path $_.FullName 'x64\signtool.exe'
            } |
            Where-Object {
                Test-Path -LiteralPath $_ -PathType Leaf
            }
    )

    if ($signToolPaths.Count -eq 0) {
        throw 'The x64 signtool.exe was not found in the Windows SDK.'
    }

    return $signToolPaths[0]
}

function Test-PicaInstallerBootstrapper {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        return $false
    }

    $versionInfo = [System.Diagnostics.FileVersionInfo]::GetVersionInfo($Path)
    $hasExpectedProductName = [string]::Equals(
        $versionInfo.ProductName,
        'Pica Installer',
        [System.StringComparison]::Ordinal)
    $hasExpectedDescription = [string]::Equals(
        $versionInfo.FileDescription,
        'Pica Installer Bootstrapper',
        [System.StringComparison]::Ordinal)

    return $hasExpectedProductName -and $hasExpectedDescription
}

function Assert-PicaSignature {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    $signature = Get-AuthenticodeSignature -LiteralPath $Path

    if ($signature.SignatureType -eq [System.Management.Automation.SignatureType]::None) {
        throw "File '$Path' is not Authenticode-signed."
    }

    if ($signature.Status -eq [System.Management.Automation.SignatureStatus]::HashMismatch) {
        throw "File '$Path' has an invalid Authenticode content hash."
    }

    if ($null -eq $signature.SignerCertificate) {
        throw "File '$Path' does not contain a signer certificate."
    }

    if (-not [string]::Equals(
            $signature.SignerCertificate.Thumbprint,
            $normalizedThumbprint,
            [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "File '$Path' was signed by an unexpected certificate."
    }
}

function New-PicaInstallerBootstrapper {
    param(
        [Parameter(Mandatory = $true)]
        [string]$VelopackSetupPath,

        [Parameter(Mandatory = $true)]
        [string]$DestinationPath
    )

    Assert-PicaSignature -Path $VelopackSetupPath

    $outputDirectory = Join-Path $installerBuildDirectory 'output'
    $builtInstallerPath = Join-Path `
        $outputDirectory `
        'Pica-win-Setup.exe'

    if (-not (Test-Path -LiteralPath $outputDirectory -PathType Container)) {
        New-Item -ItemType Directory -Path $outputDirectory | Out-Null
    }

    Invoke-DotNet -Arguments @(
        'build',
        $installerProjectPath,
        '--configuration',
        'Release',
        '--nologo',
        '--verbosity',
        'quiet',
        "-p:InstallerOutputDirectory=$outputDirectory",
        "-p:InstallerVersion=$Version",
        "-p:VelopackSetupPath=$VelopackSetupPath"
    )

    if (-not (Test-Path -LiteralPath $builtInstallerPath -PathType Leaf)) {
        throw "Pica installer build did not create '$builtInstallerPath'."
    }

    $signToolPath = Get-SignToolPath
    & $signToolPath `
        sign `
        /fd sha256 `
        /sha1 $normalizedThumbprint `
        /tr http://timestamp.digicert.com `
        /td sha256 `
        $builtInstallerPath

    if ($LASTEXITCODE -ne 0) {
        throw "Pica installer signing failed with exit code $LASTEXITCODE."
    }

    Assert-PicaSignature -Path $builtInstallerPath

    if (-not (Test-PicaInstallerBootstrapper -Path $builtInstallerPath)) {
        throw "Built file '$builtInstallerPath' is not a Pica installer bootstrapper."
    }

    Copy-Item `
        -LiteralPath $builtInstallerPath `
        -Destination $DestinationPath `
        -Force
}

function Assert-PicaPackageSignatures {
    param(
        [Parameter(Mandatory = $true)]
        [string]$PackagePath,

        [Parameter(Mandatory = $true)]
        [string]$MainExecutableEntryPath
    )

    if (-not (Test-Path -LiteralPath $PackagePath -PathType Leaf)) {
        throw "Velopack full package '$PackagePath' does not exist."
    }

    $archive = [System.IO.Compression.ZipFile]::OpenRead($PackagePath)

    try {
        $mainExecutableEntries = @(
            $archive.Entries |
                Where-Object {
                    [string]::Equals(
                        $_.FullName,
                        $MainExecutableEntryPath,
                        [System.StringComparison]::OrdinalIgnoreCase)
                }
        )

        if ($mainExecutableEntries.Count -ne 1) {
            throw "Velopack full package '$PackagePath' does not contain exactly one '$MainExecutableEntryPath' entry."
        }

        $executableEntries = @(
            $archive.Entries |
                Where-Object {
                    $_.FullName.EndsWith(
                        '.exe',
                        [System.StringComparison]::OrdinalIgnoreCase)
                }
        )

        if ($executableEntries.Count -eq 0) {
            throw "Velopack full package '$PackagePath' does not contain executable files."
        }

        foreach ($executableEntry in $executableEntries) {
            $temporaryExecutablePath = Join-Path `
                ([System.IO.Path]::GetTempPath()) `
                "Pica-signature-$([Guid]::NewGuid().ToString('N')).exe"

            try {
                [System.IO.Compression.ZipFileExtensions]::ExtractToFile(
                    $executableEntry,
                    $temporaryExecutablePath,
                    $false)
                Assert-PicaSignature -Path $temporaryExecutablePath
            }
            catch {
                throw "Velopack package entry '$($executableEntry.FullName)' failed signature validation: $($_.Exception.Message)"
            }
            finally {
                if (Test-Path -LiteralPath $temporaryExecutablePath -PathType Leaf) {
                    Remove-Item -LiteralPath $temporaryExecutablePath -Force
                }
            }
        }
    }
    finally {
        $archive.Dispose()
    }
}

Invoke-DotNet -Arguments @('tool', 'restore')

if ([string]::IsNullOrWhiteSpace($PreparedApplicationDirectory)) {
    Invoke-DotNet -Arguments @(
        'publish',
        $projectPath,
        '--configuration',
        'Release',
        '--runtime',
        'win-x64',
        '--self-contained',
        'false',
        "-p:Version=$Version",
        '-p:PublishSingleFile=false',
        '-p:IncludeNativeLibrariesForSelfExtract=false',
        "-p:PublishDir=$publishDirectory"
    )
}
elseif (-not (Test-Path -LiteralPath (Join-Path $publishDirectory 'Pica.exe') -PathType Leaf)) {
    throw "Prepared application directory '$publishDirectory' does not contain Pica.exe."
}

$publishedExecutablePath = Join-Path $publishDirectory 'Pica.exe'
$publishedVersionInfo = [System.Diagnostics.FileVersionInfo]::GetVersionInfo(
    $publishedExecutablePath)
$publishedVersion = [version]::Parse($publishedVersionInfo.FileVersion)
$requestedVersion = [version]::Parse($Version)

if (($publishedVersion.Major -ne $requestedVersion.Major) -or
    ($publishedVersion.Minor -ne $requestedVersion.Minor) -or
    ($publishedVersion.Build -ne $requestedVersion.Build)) {
    throw "Published Pica.exe version '$publishedVersion' does not match requested release version '$requestedVersion'."
}

$packArguments = @(
    'vpk',
    'pack',
    '--packId',
    'Pica',
    '--packVersion',
    $Version,
    '--packDir',
    $publishDirectory,
    '--mainExe',
    'Pica.exe',
    '--packTitle',
    'Pica',
    '--packAuthors',
    'Krivodeling',
    '--icon',
    $iconPath,
    '--runtime',
    'win-x64',
    '--framework',
    'net9-x64-desktop',
    '--outputDir',
    $releaseDirectory,
    '--noPortable',
    'true',
    '--signParallel',
    '1',
    '--signParams',
    "/fd sha256 /sha1 $normalizedThumbprint /tr http://timestamp.digicert.com /td sha256"
)

if (-not [string]::IsNullOrWhiteSpace($ReleaseNotesPath)) {
    $resolvedReleaseNotesPath = [System.IO.Path]::GetFullPath($ReleaseNotesPath)

    if (-not (Test-Path -LiteralPath $resolvedReleaseNotesPath -PathType Leaf)) {
        throw "Release notes file '$resolvedReleaseNotesPath' does not exist."
    }

    $packArguments += @('--releaseNotes', $resolvedReleaseNotesPath)
}

$fullPackagePath = Join-Path $releaseDirectory "Pica-$Version-full.nupkg"
Assert-NoNewerPicaRelease `
    -Directory $releaseDirectory `
    -RequestedVersion $requestedVersion

if (Test-Path -LiteralPath $fullPackagePath -PathType Leaf) {
    Write-Output "Existing Pica $Version release will be verified and reused."
}
else {
    Invoke-DotNet -Arguments $packArguments
}

Assert-PicaPackageSignatures `
    -PackagePath $fullPackagePath `
    -MainExecutableEntryPath 'lib/app/Pica.exe'

if (-not (Test-Path -LiteralPath $installerPath -PathType Leaf)) {
    throw "Velopack did not create '$installerPath'."
}

if (Test-PicaInstallerBootstrapper -Path $installerPath) {
    Write-Output "Existing Pica installer bootstrapper will be verified and reused."
}
else {
    New-PicaInstallerBootstrapper `
        -VelopackSetupPath $installerPath `
        -DestinationPath $installerPath
}

Assert-PicaSignature -Path $installerPath

if (-not (Test-PicaInstallerBootstrapper -Path $installerPath)) {
    throw "Public installer '$installerPath' is not a Pica installer bootstrapper."
}

if ($PublishToGitHub) {
    if ([string]::IsNullOrWhiteSpace($env:GITHUB_TOKEN)) {
        throw 'GITHUB_TOKEN is required to publish a release.'
    }

    $previousVelopackToken = $env:VPK_TOKEN

    try {
        $env:VPK_TOKEN = $env:GITHUB_TOKEN
        Invoke-DotNet -Arguments @(
            'vpk',
            'upload',
            'github',
            '--outputDir',
            $releaseDirectory,
            '--repoUrl',
            $repositoryUrl,
            '--publish',
            'true',
            '--releaseName',
            "Pica $Version",
            '--tag',
            "v$Version"
        )
    }
    finally {
        $env:VPK_TOKEN = $previousVelopackToken
    }
}

Write-Output "Pica $Version release is ready in '$releaseDirectory'."
