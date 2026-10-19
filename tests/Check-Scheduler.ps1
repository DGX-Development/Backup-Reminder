$ErrorActionPreference='Stop'
$projectRoot=Split-Path -Parent $PSScriptRoot
$evidence=Join-Path $projectRoot ('artifacts\tests\scheduler-'+[guid]::NewGuid().ToString('N'))
$exe=Join-Path $projectRoot 'artifacts\Backup-Reminder-win-x64\Backup-Reminder.exe'
$process=Start-Process -FilePath $exe -WorkingDirectory $projectRoot -ArgumentList ('--scheduler-test "'+$evidence+'"') -WindowStyle Hidden -PassThru
if(-not $process.WaitForExit(65000)){throw 'Scheduler verification timed out; inspect its isolated task before retrying.'}
Get-Content -LiteralPath (Join-Path $evidence 'scheduler-verification.txt')
if($process.ExitCode -ne 0){throw 'Scheduler verification failed'}
'Evidence: '+$evidence
