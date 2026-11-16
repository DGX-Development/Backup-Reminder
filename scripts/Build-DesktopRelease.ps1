param([string]$DotNet='dotnet',[Parameter(Mandatory)][string]$Compiler,[string]$OutputDirectory=(Join-Path $PSScriptRoot '../artifacts/releases'))
$ErrorActionPreference='Stop'
$root=Split-Path -Parent $PSScriptRoot
$OutputDirectory=[IO.Path]::GetFullPath($OutputDirectory)
$source=Join-Path $root 'src/BackupReminder.Desktop'
$current=Join-Path $OutputDirectory 'Build'
& $DotNet publish (Join-Path $source 'BackupReminder.Desktop.csproj') -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=None -o $current -p:NuGetAudit=false --ignore-failed-sources
if($LASTEXITCODE -ne 0){throw 'Publish failed'}
$payload=Join-Path $OutputDirectory 'Portable'
New-Item -ItemType Directory -Path $payload -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $current 'Backup-Reminder.exe') -Destination (Join-Path $payload 'Backup-Reminder.exe')
Copy-Item -LiteralPath (Join-Path $root 'LICENSE') -Destination (Join-Path $payload 'LICENSE.txt')
[IO.File]::WriteAllText((Join-Path $payload 'portable.mode'),'')
$guide=Get-Content -LiteralPath (Join-Path $source 'ReviewHelp.json') -Raw | ConvertFrom-Json -AsHashtable
$lines=@('Backup Reminder 1.0.2','','Extract the entire portable ZIP before opening Backup-Reminder.exe.','')
foreach($topic in $guide.GetEnumerator()){$lines+=@($topic.Key,$topic.Value,'')}
[IO.File]::WriteAllText((Join-Path $payload 'README.txt'),($lines -join "`r`n"))
$assets=Get-Content -Raw (Join-Path $source 'obj/project.assets.json') | ConvertFrom-Json
$notices=@(@{Package='Microsoft.NETCore.App.Runtime.win-x64';Source='LICENSE.TXT';Destination='DOTNET-LICENSE.txt'},@{Package='Microsoft.NETCore.App.Runtime.win-x64';Source='THIRD-PARTY-NOTICES.TXT';Destination='DOTNET-THIRD-PARTY-NOTICES.txt'},@{Package='Microsoft.WindowsDesktop.App.Runtime.win-x64';Source='LICENSE';Destination='WINDOWS-DESKTOP-LICENSE.txt'})
foreach($notice in $notices){$download=@($assets.project.frameworks.PSObject.Properties.Value.downloadDependencies | Where-Object name -eq $notice.Package)[0];$version=$download.version.Trim('[',']').Split(',')[0].Trim();$source=$null;foreach($folder in $assets.packageFolders.PSObject.Properties.Name){$candidate=Join-Path $folder ($notice.Package.ToLowerInvariant()+'\'+$version+'\'+$notice.Source);if(Test-Path -LiteralPath $candidate){$source=$candidate;break}};if(!$source){throw 'Missing runtime notice'};Copy-Item -LiteralPath $source -Destination (Join-Path $payload $notice.Destination)}
$allowed=@('Backup-Reminder.exe','README.txt','LICENSE.txt','DOTNET-LICENSE.txt','DOTNET-THIRD-PARTY-NOTICES.txt','WINDOWS-DESKTOP-LICENSE.txt','portable.mode')
if(@(Compare-Object ($allowed|Sort-Object) @(Get-ChildItem -LiteralPath $payload -Name | Sort-Object)).Count){throw 'Unexpected package contents'}
Compress-Archive -LiteralPath @($allowed|ForEach-Object {Join-Path $payload $_}) -DestinationPath (Join-Path $OutputDirectory 'Backup-Reminder-1.0.2-Portable.zip') -Force
& $Compiler ('/DPackageDir='+$payload) ('/DOutputFolder='+$OutputDirectory) ('/DArtworkDir='+(Join-Path $root 'Packaging')) (Join-Path $root 'Packaging/BackupReminder.iss')
if($LASTEXITCODE -ne 0){throw 'Installer compile failed'}
$names=@('Backup-Reminder-1.0.2-Portable.zip','Backup-Reminder-1.0.2-Setup.exe')
$lines=foreach($name in $names){((Get-FileHash -LiteralPath (Join-Path $OutputDirectory $name)).Hash.ToLowerInvariant())+'  '+$name}
[IO.File]::WriteAllLines((Join-Path $OutputDirectory 'SHA256SUMS.txt'),[string[]]$lines)
