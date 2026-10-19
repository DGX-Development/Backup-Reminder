$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$tokens = $null
$errors = $null
$ast = [System.Management.Automation.Language.Parser]::ParseFile((Join-Path $projectRoot 'Backup-Reminder.ps1'),[ref]$tokens,[ref]$errors)
if ($errors.Count) {throw 'Application syntax failed'}
$menuTokens=$null;$menuErrors=$null
[System.Management.Automation.Language.Parser]::ParseFile((Join-Path $projectRoot 'ui\Menus.ps1'),[ref]$menuTokens,[ref]$menuErrors) | Out-Null
if($menuErrors.Count) {throw 'Menu source syntax failed'}
$reader = $ast.Find({param($node) $node -is [System.Management.Automation.Language.FunctionDefinitionAst] -and $node.Name -eq 'Read-Settings'},$true)
if (-not $reader) {throw 'Settings reader missing'}
. ([scriptblock]::Create($reader.Extent.Text))
$example = Join-Path $projectRoot 'settings.example.json'
$settings = Read-Settings $example
if (@($settings.items).Count -ne 5) {throw 'Example did not load'}
if (@($settings.items | Where-Object {-not $_.Group}).Count -ne 2 -or @($settings.items | Where-Object Group -eq 'backup-jobs').Count -ne 3) {throw 'Starter must have two standalone items and three grouped jobs'}
$temporaryFile = [System.IO.Path]::GetTempFileName()
try {
    foreach ($testCase in @('duplicate','empty','orphan-group','version')) {
        $invalid = Get-Content -Raw -LiteralPath $example | ConvertFrom-Json
        switch ($testCase) {
            'duplicate' {$invalid.items[1].Id = $invalid.items[0].Id}
            'empty' {$invalid.items = @()}
            'orphan-group' {$invalid.items[2].Group = 'missing'}
            'version' {$invalid.version = 999}
        }
        $invalid | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $temporaryFile -Encoding UTF8
        $rejected = $false
        try {Read-Settings $temporaryFile | Out-Null} catch {$rejected = $true}
        if (-not $rejected) {throw "Invalid settings were accepted: $testCase"}
    }
} finally {Remove-Item -LiteralPath $temporaryFile -Force}
'PASS: application syntax, generic configuration, duplicate IDs, empty checklist, orphan group, unsupported version.'
$resolver = $ast.Find({param($node) $node -is [System.Management.Automation.Language.FunctionDefinitionAst] -and $node.Name -eq 'Resolve-BackupTarget'},$true)
. ([scriptblock]::Create($resolver.Extent.Text))
if ((Resolve-BackupTarget 'knownfolder:Documents') -ne [Environment]::GetFolderPath('MyDocuments')) {throw 'Documents resolution failed'}
if ((Resolve-BackupTarget 'knownfolder:Pictures') -ne [Environment]::GetFolderPath('MyPictures')) {throw 'Pictures resolution failed'}
$rejected=$false
try {Resolve-BackupTarget 'knownfolder:Unknown' | Out-Null} catch {$rejected=$true}
if(-not $rejected) {throw 'Unknown folder token was accepted'}
$instanceFunction=$ast.Find({param($node) $node -is [System.Management.Automation.Language.FunctionDefinitionAst] -and $node.Name -eq 'Get-ProfileInstanceName'},$true)
. ([scriptblock]::Create($instanceFunction.Extent.Text))
$personal=Join-Path ([IO.Path]::GetTempPath()) 'BackupReminder-ProfileIdentity'
$development=Join-Path $personal 'development'
if ((Get-ProfileInstanceName $personal) -eq (Get-ProfileInstanceName $development)) {throw 'Profiles share an instance lock'}
if ((Get-ProfileInstanceName $personal) -ne (Get-ProfileInstanceName ($personal.ToUpperInvariant()+'\'))) {throw 'Equivalent profile paths do not share an instance lock'}
'PASS: known Windows folder targets and separate per-profile instance identities.'
