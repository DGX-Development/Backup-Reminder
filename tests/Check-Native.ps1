param([string]$ZipPath)
$ErrorActionPreference='Stop'
$projectRoot=Split-Path -Parent $PSScriptRoot
if (-not $ZipPath) {
[xml]$projectMetadata=Get-Content -Raw -LiteralPath (Join-Path $projectRoot 'src\BackupReminder\BackupReminder.csproj')
$ZipPath=Join-Path $projectRoot ('artifacts\Backup-Reminder-'+[string]$projectMetadata.Project.PropertyGroup.InformationalVersion+'-win-x64.zip')
}
$testRoot=Join-Path $projectRoot ('artifacts\tests\native-package-'+[guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $testRoot -Force | Out-Null
Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive=[IO.Compression.ZipFile]::OpenRead($ZipPath)
try {
    $expected=@('Backup-Reminder.exe','README.txt','LICENSE.txt','DOTNET-LICENSE.txt','DOTNET-THIRD-PARTY-NOTICES.txt','WINDOWS-DESKTOP-LICENSE.txt')
    $names=@($archive.Entries.FullName)
    if (@(Compare-Object ($expected|Sort-Object) ($names|Sort-Object)).Count) {throw 'ZIP contains unexpected files'}
} finally {$archive.Dispose()}
Expand-Archive -LiteralPath $ZipPath -DestinationPath (Join-Path $testRoot 'unpacked')
$executable=Join-Path $testRoot 'unpacked\Backup-Reminder.exe'
if((Get-FileHash -LiteralPath (Join-Path $projectRoot 'LICENSE')).Hash -ne (Get-FileHash -LiteralPath (Join-Path $testRoot 'unpacked\LICENSE.txt')).Hash){throw 'Packaged app license differs from source'}
function Run-Native([string]$arguments) {
    $process=Start-Process -FilePath $executable -ArgumentList $arguments -PassThru -WindowStyle Hidden
    if (-not $process.WaitForExit(30000)) {throw 'Native check timed out'}
    if ($process.ExitCode -ne 0) {throw ('Native check failed: '+$arguments)}
}
$priorDotnetRoot=$env:DOTNET_ROOT
try {
    $env:DOTNET_ROOT=Join-Path $testRoot 'no-installed-runtime'
    Run-Native ('--self-test "'+(Join-Path $testRoot 'verification')+'"')
    $profile=Join-Path $testRoot 'live-profile'
    $first=Start-Process -FilePath $executable -ArgumentList ('--data-directory "'+$profile+'" --smoke-test') -PassThru -WindowStyle Hidden
    try {
        $ready=$false
        for($i=0;$i -lt 40;$i++) {
            Start-Sleep -Milliseconds 50;$first.Refresh()
            if($first.HasExited){throw 'First window exited before duplicate check'}
            if(Test-Path -LiteralPath (Join-Path $profile 'settings.json')){$ready=$true;break}
        }
        if(-not $ready){throw 'First profile did not initialize'}
        Run-Native ('--data-directory "'+$profile+'"')
        $first.Refresh()
        if($first.HasExited -or -not $first.Responding){throw 'First instance did not remain responsive'}
        if(-not $first.WaitForExit(10000) -or $first.ExitCode -ne 0){throw 'Smoke-test window did not close cleanly'}
    } finally {if(-not $first.HasExited){[void]$first.CloseMainWindow();[void]$first.WaitForExit(5000)}}
    if(-not(Test-Path -LiteralPath (Join-Path $profile 'settings.json')) -or -not(Test-Path -LiteralPath (Join-Path $profile 'checklist.json'))){throw 'First launch did not persist profile data'}
    $settingsHash=(Get-FileHash -LiteralPath (Join-Path $profile 'settings.json')).Hash
    Run-Native ('--data-directory "'+$profile+'" --smoke-test')
    if((Get-FileHash -LiteralPath (Join-Path $profile 'settings.json')).Hash -ne $settingsHash){throw 'Relaunch replaced the existing configuration'}
    Get-Content -LiteralPath (Join-Path $testRoot 'verification\verification.txt')
    'PASS: ZIP allowlist, extracted self-contained startup, duplicate activation, clean shutdown and configuration preservation.'
    'Evidence: '+$testRoot
} finally {$env:DOTNET_ROOT=$priorDotnetRoot}
