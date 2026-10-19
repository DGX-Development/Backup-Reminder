param(
    [ValidateSet('win-x64')][string]$Runtime = 'win-x64',
    [string]$OutputDirectory
)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$dotnet = (Get-Command dotnet -ErrorAction Stop).Source
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_GENERATE_ASPNET_CERTIFICATE = 'false'
$project = Join-Path $projectRoot 'src\BackupReminder\BackupReminder.csproj'
$artifacts = if ($OutputDirectory) { [IO.Path]::GetFullPath($OutputDirectory) } else { Join-Path $projectRoot 'artifacts' }
# Use a fresh publish directory; never collect files recursively from the checkout.
$publish = Join-Path $artifacts ('build-' + [guid]::NewGuid().ToString('N'))
& $dotnet publish $project -c Release -r $Runtime --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -o $publish --nologo
if ($LASTEXITCODE -ne 0) {throw 'Publishing failed'}
$executable = Join-Path $publish 'Backup-Reminder.exe'
if (-not (Test-Path -LiteralPath $executable)) {throw 'Published executable missing'}
$packageRoot = Join-Path $artifacts ('Backup-Reminder-' + $Runtime)
New-Item -ItemType Directory -Path $packageRoot -Force | Out-Null
Copy-Item -LiteralPath $executable -Destination $packageRoot
Copy-Item -LiteralPath (Join-Path $projectRoot 'docs\Portable-Readme.txt') -Destination (Join-Path $packageRoot 'README.txt')
Copy-Item -LiteralPath (Join-Path $projectRoot 'LICENSE') -Destination (Join-Path $packageRoot 'LICENSE.txt')
# Notices come from the exact runtime packs used by the resolved publish assets.
$assets = Get-Content -Raw -LiteralPath (Join-Path $projectRoot 'src\BackupReminder\obj\project.assets.json') | ConvertFrom-Json
$packageFolders = @($assets.packageFolders.PSObject.Properties.Name)
$runtimeNotices = @(
    @{Package='Microsoft.NETCore.App.Runtime.win-x64';Source='LICENSE.TXT';Destination='DOTNET-LICENSE.txt'},
    @{Package='Microsoft.NETCore.App.Runtime.win-x64';Source='THIRD-PARTY-NOTICES.TXT';Destination='DOTNET-THIRD-PARTY-NOTICES.txt'},
    @{Package='Microsoft.WindowsDesktop.App.Runtime.win-x64';Source='LICENSE';Destination='WINDOWS-DESKTOP-LICENSE.txt'}
)
foreach ($notice in $runtimeNotices) {
    $download = @($assets.project.frameworks.PSObject.Properties.Value.downloadDependencies | Where-Object name -eq $notice.Package)[0]
    if (-not $download) {throw ('Resolved runtime package missing: ' + $notice.Package)}
    $version = $download.version.Trim('[',']').Split(',')[0].Trim()
    $source = $null
    foreach ($folder in $packageFolders) {
        $candidate = Join-Path $folder ($notice.Package.ToLowerInvariant() + '\' + $version + '\' + $notice.Source)
        if (Test-Path -LiteralPath $candidate) {$source=$candidate;break}
    }
    if (-not $source) {throw ('Required runtime notice missing: ' + $notice.Source)}
    Copy-Item -LiteralPath $source -Destination (Join-Path $packageRoot $notice.Destination)
}
# Package only the executable, instructions and required notices.
$allowed = @('Backup-Reminder.exe','README.txt','LICENSE.txt','DOTNET-LICENSE.txt','DOTNET-THIRD-PARTY-NOTICES.txt','WINDOWS-DESKTOP-LICENSE.txt')
$actual = @(Get-ChildItem -LiteralPath $packageRoot | Select-Object -ExpandProperty Name)
if (@(Compare-Object ($allowed | Sort-Object) ($actual | Sort-Object)).Count) {throw 'Unexpected package contents'}
[xml]$projectMetadata=Get-Content -Raw -LiteralPath $project
$version=[string]$projectMetadata.Project.PropertyGroup.InformationalVersion
$zip = Join-Path $artifacts ('Backup-Reminder-' + $version + '-' + $Runtime + '.zip')
Compress-Archive -LiteralPath @($allowed | ForEach-Object {Join-Path $packageRoot $_}) -DestinationPath $zip -Force
$checksum = (Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash.ToLowerInvariant()
[IO.File]::WriteAllText(($zip+'.sha256'),($checksum+'  '+[IO.Path]::GetFileName($zip)+"`n"))
Write-Output ('Executable: ' + (Join-Path $packageRoot 'Backup-Reminder.exe'))
Write-Output ('ZIP: ' + $zip)
