# Releasing Pica

Pica uses Velopack for Windows installation and automatic updates. Only
`Pica.Desktop` participates in this process. Applications that embed
`Pica.Viewer` do not start the updater.

## Prerequisites

- .NET 9 SDK
- Windows SDK signing tools
- an Authenticode certificate in `Cert:\CurrentUser\My`
- `GITHUB_TOKEN` with permission to create releases when publishing to GitHub

Restore the repository tools before the first release:

```powershell
dotnet tool restore
```

Set `PicaVersion` in `src/Pica.Build.props` to the version being released and
commit that change before publishing the application.

## Publish from Rider

The shared `WindowsLocal` configuration publishes the multi-file Release build
to `src/Pica.Desktop/bin/Release/net9.0/publish`.

The shared `WindowsGitHub` configuration builds Pica, creates and signs the
Velopack release, builds the installer with directory selection, and uploads
the release to GitHub.

Set these user environment variables before starting Rider:

```powershell
[Environment]::SetEnvironmentVariable(
    'PICA_CERTIFICATE_THUMBPRINT',
    '0123456789ABCDEF0123456789ABCDEF01234567',
    'User')
[Environment]::SetEnvironmentVariable(
    'GITHUB_TOKEN',
    '...',
    'User')
```

Restart Rider after changing the environment variables. Then run the shared
`WindowsGitHub` configuration.

## Build from the Folder publish profile

The `Folder.pubxml` profile is also available for other IDEs and the command
line. Ensure that the selected solution configuration is Release because IDE
command-line properties take precedence over values stored in a `.pubxml`.
Then pass the published directory to the release script:

```powershell
.\eng\Publish-PicaRelease.ps1 `
  -Version 1.0.0 `
  -CertificateThumbprint <THUMBPRINT> `
  -PreparedApplicationDirectory "F:\Program Files\Krivodeling\Pica"
```

The script verifies and signs the application package, creates the Velopack
release in `Releases`, and replaces the standard Windows setup executable with
the Pica installer that lets the user choose an installation directory.

Add `-PublishToGitHub` to upload the finished release:

```powershell
$env:GITHUB_TOKEN = "<TOKEN>"
.\eng\Publish-PicaRelease.ps1 `
  -Version 1.0.0 `
  -CertificateThumbprint <THUMBPRINT> `
  -PreparedApplicationDirectory "F:\Program Files\Krivodeling\Pica" `
  -PublishToGitHub
```

Never commit the certificate, its private key, or a GitHub token.
