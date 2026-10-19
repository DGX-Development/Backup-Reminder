param([string]$PreviewPath, [ValidateSet('System','Light','Dark')][string]$Appearance = 'System', [string]$SettingsPath, [string]$DataDirectory, [switch]$Development)
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
[System.Windows.Forms.Application]::EnableVisualStyles()
$ErrorActionPreference = 'Stop'
$stateDirectory = Join-Path $env:LOCALAPPDATA 'Backup Reminder'
# Portable data gives desktop and packaged development launches the same profile.
if ($DataDirectory) {
    $stateDirectory = [System.IO.Path]::GetFullPath($DataDirectory)
} elseif ($Development) {
    $stateDirectory = Join-Path $PSScriptRoot '.local\development'
} elseif (Test-Path -LiteralPath (Join-Path $PSScriptRoot '.local\settings.json') -PathType Leaf) {
    $stateDirectory = Join-Path $PSScriptRoot '.local'
}
function Get-ProfileInstanceName([string]$directory) {
    $canonical = [System.IO.Path]::GetFullPath($directory).TrimEnd([char]'\').ToUpperInvariant()
    $hash = [System.Security.Cryptography.SHA256]::Create()
    try { $digest = $hash.ComputeHash([System.Text.Encoding]::UTF8.GetBytes($canonical)) } finally { $hash.Dispose() }
    return 'Local\BackupReminder-' + ([BitConverter]::ToString($digest).Replace('-',''))
}
$instanceMutex = $null
$activationEvent = $null
$activationTimer = $null
$ownsProfileMutex = $false
try {
if (-not $PreviewPath) {
    $instanceName = Get-ProfileInstanceName $stateDirectory
    $instanceMutex = New-Object System.Threading.Mutex($false,$instanceName)
    try { $ownsProfileMutex = $instanceMutex.WaitOne(0) } catch [System.Threading.AbandonedMutexException] { $ownsProfileMutex = $true }
    $activationEvent = New-Object System.Threading.EventWaitHandle($false,[System.Threading.EventResetMode]::AutoReset,($instanceName+'-activate'))
    if (-not $ownsProfileMutex) {
        [void]$activationEvent.Set()
        return
    }
}
$stateFile = Join-Path $stateDirectory 'checklist.json'
$settingsFile = Join-Path $stateDirectory 'settings.json'
if ($SettingsPath) {
    if (-not (Test-Path -LiteralPath $SettingsPath -PathType Leaf)) {throw 'The specified settings file does not exist.'}
    $settingsFile = $SettingsPath
}
function Read-Settings([string]$file) {
    $settings = Get-Content -Raw -LiteralPath $file | ConvertFrom-Json
    if ($settings.version -ne 1 -or @($settings.items).Count -lt 1) {throw 'Settings must contain version 1 and at least one checklist item.'}
    $ids = @{}
    foreach ($entry in $settings.items) {
        if (-not $entry.Id -or -not $entry.Title -or $ids.ContainsKey([string]$entry.Id)) {throw 'Each checklist item needs a unique Id and a Title.'}
        $ids[[string]$entry.Id] = $true
        if ($entry.Group -and @($settings.groups | Where-Object Id -eq $entry.Group).Count -ne 1) {throw 'An item refers to an unknown or duplicate group.'}
    }
    return $settings
}
if (-not (Test-Path -LiteralPath $settingsFile)) {
    New-Item -ItemType Directory -Path $stateDirectory -Force | Out-Null
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'settings.example.json') -Destination $settingsFile
}
try { $settings = Read-Settings $settingsFile } catch {
    [System.Windows.Forms.MessageBox]::Show('Could not read checklist settings. Your file has not been changed.' + [Environment]::NewLine + $_.Exception.Message,'Backup Reminder') | Out-Null
    return
}
$items = @($settings.items)
$groups = @($settings.groups)
foreach ($entry in @($items) + @($groups)) {
    if ($entry.Target) { $entry.Target = [Environment]::ExpandEnvironmentVariables([string]$entry.Target) }
    if ($entry.IconPath) { $entry.IconPath = [Environment]::ExpandEnvironmentVariables([string]$entry.IconPath) }
}
$script:savedChecks = @()
$script:lastCompleted = $null
if (Test-Path -LiteralPath $stateFile) {
    try {
        $saved = Get-Content -Raw -LiteralPath $stateFile | ConvertFrom-Json
        $script:savedChecks = @($saved.checked)
        $script:lastCompleted = $saved.lastCompleted
    } catch {
        [System.Windows.Forms.MessageBox]::Show('Saved progress could not be read. The existing file will be preserved.','Backup Reminder') | Out-Null
        return
    }
}
$dark = $false
if ($Appearance -eq 'Dark') { $dark = $true }
elseif ($Appearance -eq 'System') {
    try { $dark = (Get-ItemPropertyValue -LiteralPath 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize' -Name AppsUseLightTheme) -eq 0 } catch {}
}
if ($dark) {
    $palette = @{Window='#181B20';Surface='#22262D';Text='#F2F4F8';Muted='#B0B8C6';Line='#363E49';Accent='#78B7FF';Button='#303845';ButtonText='#E8EEF8';Primary='#78B7FF';PrimaryText='#142337';Disabled='#353C46'}
} else {
    $palette = @{Window='#F3F5F8';Surface='#FFFFFF';Text='#1C2534';Muted='#596779';Line='#DCE2EB';Accent='#2468BB';Button='#EDF2F9';ButtonText='#235993';Primary='#2165B6';PrimaryText='#FFFFFF';Disabled='#DDE3EB'}
}
function Color([string]$name) { [System.Drawing.ColorTranslator]::FromHtml($palette[$name]) }
$bodyFont = New-Object System.Drawing.Font('Segoe UI',10)
$smallFont = New-Object System.Drawing.Font('Segoe UI',9)
$titleFont = New-Object System.Drawing.Font('Segoe UI Semibold',10.5)
function Style-Button($button, [bool]$primary = $false) {
    $button.FlatStyle = 'Flat'
    $button.FlatAppearance.BorderSize = 0
    $button.BackColor = Color 'Button'
    $button.ForeColor = Color 'ButtonText'
    $button.Font = $titleFont
    $button.Cursor = [System.Windows.Forms.Cursors]::Hand
    if ($primary) { $button.BackColor = Color 'Primary'; $button.ForeColor = Color 'PrimaryText' }
}
$script:ownedImages = New-Object System.Collections.Generic.List[System.IDisposable]
function Get-AppImage([string]$file, [string]$fallback) {
    if ($file -and (Test-Path -LiteralPath $file -PathType Leaf)) {
        try {
            $extension = [System.IO.Path]::GetExtension($file).ToLowerInvariant()
            if ($extension -in @('.png','.jpg','.jpeg','.bmp','.gif')) {
                $loadedImage = [System.Drawing.Image]::FromFile($file)
                try { $bitmap = New-Object System.Drawing.Bitmap($loadedImage) } finally { $loadedImage.Dispose() }
            } else {
                if ($extension -eq '.ico') {
                    $nativeIcon = New-Object System.Drawing.Icon($file,64,64)
                } else {
                    $nativeIcon = [System.Drawing.Icon]::ExtractAssociatedIcon($file)
                }
                try { $bitmap = $nativeIcon.ToBitmap() } finally { $nativeIcon.Dispose() }
            }
            $script:ownedImages.Add($bitmap)
            return $bitmap
        } catch {}
    }
    $bitmap = New-Object System.Drawing.Bitmap(40,40)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $graphics.SmoothingMode = 'AntiAlias'
    $brush = New-Object System.Drawing.SolidBrush((Color 'Accent'))
    $graphics.FillEllipse($brush,2,2,36,36)
    $letterFont = New-Object System.Drawing.Font('Segoe UI Semibold',15)
    $whiteBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::White)
    $graphics.DrawString($fallback,$letterFont,$whiteBrush,9,4)
    $whiteBrush.Dispose();$letterFont.Dispose();$brush.Dispose();$graphics.Dispose()
    $script:ownedImages.Add($bitmap)
    return $bitmap
}
$form = New-Object System.Windows.Forms.Form
$form.Text = 'Backup Reminder'
if ($Development) {$form.Text = 'Backup Reminder - Development'}
$form.Size = New-Object System.Drawing.Size(760,960)
$form.MinimumSize = New-Object System.Drawing.Size(700,580)
$form.StartPosition = 'CenterScreen'
$form.Font = $bodyFont
$form.BackColor = Color 'Window'
$form.ForeColor = Color 'Text'
$form.Icon = New-Object System.Drawing.Icon((Join-Path $PSScriptRoot 'assets\backup-reminder.ico'))
$header = New-Object System.Windows.Forms.Panel
$header.Dock = 'Top'
$header.Height = 118
$header.BackColor = Color 'Surface'
$brand = New-Object System.Windows.Forms.PictureBox
$brand.Image = [System.Drawing.Image]::FromFile((Join-Path $PSScriptRoot 'assets\backup-reminder.png'))
$script:ownedImages.Add($brand.Image)
$brand.SizeMode = 'Zoom'
$brand.SetBounds(24,25,58,58)
$header.Controls.Add($brand)
$heading = New-Object System.Windows.Forms.Label
$heading.Text = 'Backup Reminder'
$heading.Font = New-Object System.Drawing.Font('Segoe UI Semibold',21)
$heading.SetBounds(99,21,490,42)
$header.Controls.Add($heading)
$lastText = 'No completion recorded'
if ($script:lastCompleted) { $lastText = ([datetime]$script:lastCompleted).ToString('MMM d, yyyy  h:mm tt') }
$subtitle = New-Object System.Windows.Forms.Label
$subtitle.Text = "Last completed: $lastText"
if ($Development) {$subtitle.Text = "Development profile  |  Last completed: $lastText"}
$subtitle.ForeColor = Color 'Muted'
$subtitle.SetBounds(102,67,520,25)
$header.Controls.Add($subtitle)
$form.Controls.Add($header)
$footer = New-Object System.Windows.Forms.Panel
$footer.Dock = 'Bottom'
$footer.Height = 112
$footer.BackColor = Color 'Surface'
$form.Controls.Add($footer)
$progressLabel = New-Object System.Windows.Forms.Label
$progressLabel.Location = New-Object System.Drawing.Point(26,14)
$progressLabel.AutoSize = $true
$footer.Controls.Add($progressLabel)
$closeButton = New-Object System.Windows.Forms.Button
$closeButton.Text = 'Close for now'
$closeButton.Size = New-Object System.Drawing.Size(145,36)
$closeButton.Location = New-Object System.Drawing.Point(26,57)
$closeButton.Add_Click({$form.Close()})
Style-Button $closeButton
$footer.Controls.Add($closeButton)
$completeButton = New-Object System.Windows.Forms.Button
$completeButton.Text = 'Complete backups'
$completeButton.Size = New-Object System.Drawing.Size(175,36)
$completeButton.Location = New-Object System.Drawing.Point(530,57)
$completeButton.Anchor = 'Top,Right'
Style-Button $completeButton $true
$footer.Controls.Add($completeButton)
$scroll = New-Object System.Windows.Forms.Panel
$scroll.Dock = 'Fill'
$scroll.AutoScroll = $true
$scroll.BackColor = Color 'Window'
$scroll.Padding = New-Object System.Windows.Forms.Padding(22,12,22,12)
$form.Controls.Add($scroll)
$scroll.BringToFront()
$script:boxes = @()
function Save-Checklist {
    param([string[]]$CheckedIds, [AllowNull()][string]$CompletionDate = $script:lastCompleted)
    New-Item -ItemType Directory -Path $stateDirectory -Force | Out-Null
    if (-not $PSBoundParameters.ContainsKey('CheckedIds')) {$CheckedIds = @($script:boxes | Where-Object Checked | ForEach-Object Tag)}
    $data = @{version=1;checked=@($CheckedIds);lastCompleted=$CompletionDate} | ConvertTo-Json
    $temporaryFile = Join-Path $stateDirectory 'checklist.pending.json'
    Set-Content -LiteralPath $temporaryFile -Value $data -Encoding UTF8
    Move-Item -LiteralPath $temporaryFile -Destination $stateFile -Force
}
function Update-Progress {
    $count = @($script:boxes | Where-Object Checked).Count
    $progressLabel.Text = "$count of $($items.Count) completed"
    $completeButton.Enabled = $count -eq $items.Count
    if ($completeButton.Enabled) {$completeButton.BackColor = Color 'Primary'} else {$completeButton.BackColor = Color 'Disabled'}
    if ($progressBar) {$progressBar.Value = $count}
}
function Resolve-BackupTarget([string]$target) {
    switch ($target) {
        'knownfolder:Documents' { return [Environment]::GetFolderPath('MyDocuments') }
        'knownfolder:Pictures' { return [Environment]::GetFolderPath('MyPictures') }
        default {
            if ($target -like 'knownfolder:*') {throw 'Unknown Windows folder target.'}
            return [Environment]::ExpandEnvironmentVariables($target)
        }
    }
}
function Open-BackupTarget([string]$target) {
    try {
        $target = Resolve-BackupTarget $target
        if ($target -match '^https?://') {
            Start-Process -FilePath $target
        } elseif (Test-Path -LiteralPath $target -PathType Container) {
            Start-Process -FilePath 'explorer.exe' -ArgumentList ('"' + $target + '"')
        } elseif (Test-Path -LiteralPath $target -PathType Leaf) {
            Start-Process -FilePath $target -WorkingDirectory (Split-Path -Parent $target)
        } else { throw "Target not found: $target" }
    } catch {
        [System.Windows.Forms.MessageBox]::Show($_.Exception.Message,'Unable to open') | Out-Null
    }
}
$progressBar = New-Object System.Windows.Forms.ProgressBar
$progressBar.SetBounds(26,38,680,5)
$progressBar.Anchor = 'Top,Left,Right'
$progressBar.Maximum = $items.Count
$footer.Controls.Add($progressBar)
$stack = New-Object System.Windows.Forms.TableLayoutPanel
$stack.Dock = 'Top'
$stack.AutoSize = $true
$stack.AutoSizeMode = 'GrowAndShrink'
$stack.ColumnCount = 1
$stack.ColumnStyles.Add((New-Object System.Windows.Forms.ColumnStyle('Percent',100))) | Out-Null
$stack.Margin = New-Object System.Windows.Forms.Padding(0)
$scroll.Controls.Add($stack)
$intro = New-Object System.Windows.Forms.Label
$intro.Text = 'Check each item after completing its backup.'
$intro.ForeColor = Color 'Muted'
$intro.Dock = 'Fill'
$intro.Height = 35
$intro.Margin = New-Object System.Windows.Forms.Padding(3,2,0,10)
$stack.Controls.Add($intro)
$shownGroups = @{}
foreach ($item in $items) {
    if ($item.Group -and -not $shownGroups.ContainsKey([string]$item.Group)) {
        $groupDefinition = @($groups | Where-Object Id -eq $item.Group)[0]
        $shownGroups[[string]$item.Group] = $true
        $group = New-Object System.Windows.Forms.Panel
        $group.Height = 78
        $group.Dock = 'Fill'
        $group.Margin = New-Object System.Windows.Forms.Padding(0,16,0,6)
        $groupTarget = $groupDefinition.Target
        $groupIcon = New-Object System.Windows.Forms.PictureBox
        $groupIconPath = $groupTarget
        if ($groupDefinition.IconPath) {$groupIconPath = $groupDefinition.IconPath}
        $groupIcon.Image = Get-AppImage $groupIconPath 'B'
        $groupIcon.SizeMode = 'Zoom'
        $groupIcon.SetBounds(12,9,32,32)
        $group.Controls.Add($groupIcon)
        $groupTitle = New-Object System.Windows.Forms.Label
        $groupTitle.Text = $groupDefinition.Title
        $groupTitle.Font = New-Object System.Drawing.Font('Segoe UI Semibold',14)
        $groupTitle.SetBounds(54,7,320,30)
        $group.Controls.Add($groupTitle)
        $groupDetail = New-Object System.Windows.Forms.Label
        $groupDetail.Text = $groupDefinition.Detail
        $groupDetail.ForeColor = Color 'Muted'
        $groupDetail.Font = $smallFont
        $groupDetail.SetBounds(12,48,630,25)
        $group.Controls.Add($groupDetail)
        $ffsButton = New-Object System.Windows.Forms.Button
        $ffsButton.Text = 'Open'
        if ($groupDefinition.Button) {$ffsButton.Text = $groupDefinition.Button}
        $ffsButton.Tag = $groupTarget
        $ffsButton.Visible = [bool]$groupTarget
        $ffsButton.SetBounds(493,7,164,36)
        $ffsButton.Anchor = 'Top,Right'
        Style-Button $ffsButton
        $ffsButton.Add_Click({param($sender,$eventArgs) Open-BackupTarget ([string]$sender.Tag)})
        $group.Controls.Add($ffsButton)
        $stack.Controls.Add($group)
    }
    $row = New-Object System.Windows.Forms.Panel
    $row.Dock = 'Fill'
    $row.Height = 80
    $row.Margin = New-Object System.Windows.Forms.Padding(0,0,0,6)
    $row.BackColor = Color 'Surface'
    $row.Padding = New-Object System.Windows.Forms.Padding(12)
    $box = New-Object System.Windows.Forms.CheckBox
    $box.Tag = $item.Id
    $box.Checked = $script:savedChecks -contains $item.Id
    $box.AccessibleName = $item.Title + ' backup completed'
    $box.SetBounds(14,27,25,26)
    $row.Controls.Add($box)
    $script:boxes += $box
    $titleX = 87
    if ($item.Target) {
        $iconPath = $item.Target
        if ($item.IconPath) { $iconPath = $item.IconPath }

        $picture = New-Object System.Windows.Forms.PictureBox
        $picture.Image = Get-AppImage $iconPath ($item.Title.Substring(0,1))
        $picture.SizeMode = 'Zoom'
        $iconWidth = 30
        $iconHeight = 30
        if ($item.IconWidth) { $iconWidth = [Math]::Min(40,[Math]::Max(16,[int]$item.IconWidth)) }
        if ($item.IconHeight) { $iconHeight = [Math]::Min(48,[Math]::Max(16,[int]$item.IconHeight)) }
        $picture.SetBounds([int](61-$iconWidth/2),[int](39-$iconHeight/2),$iconWidth,$iconHeight)
        $row.Controls.Add($picture)
    } else { $titleX = 47 }
    $name = New-Object System.Windows.Forms.Label
    $name.Text = $item.Title
    $name.Font = $titleFont
    $name.SetBounds($titleX,14,400,25)
    $row.Controls.Add($name)
    $detail = New-Object System.Windows.Forms.Label
    $detail.Text = $item.Detail
    $detail.Font = $smallFont
    $detail.ForeColor = Color 'Muted'
    $detail.SetBounds($titleX,40,400,35)
    $detail.Anchor = 'Top,Left,Right'
    $row.Controls.Add($detail)
    if ($item.Target) {
        $button = New-Object System.Windows.Forms.Button
        $button.Text = 'Open'
        if ($item.Button) { $button.Text = $item.Button }
        $button.Tag = $item.Target
        $button.SetBounds(537,22,120,36)
        $button.Anchor = 'Top,Right'
        Style-Button $button
        $button.Add_Click({param($sender,$eventArgs) Open-BackupTarget ([string]$sender.Tag)})
        $row.Controls.Add($button)
    } else { $detail.Width = 580 }
    $box.Add_CheckedChanged({Update-Progress})
    $stack.Controls.Add($row)
}
# Establish the design width before anchors respond to the real client width.
$stack.Width = 680
foreach ($control in $stack.Controls) { $control.Width = 680 }
function Update-ChecklistLayout {
    foreach ($control in $stack.Controls) {
        if ($control -is [System.Windows.Forms.Panel]) {
            foreach ($child in $control.Controls) {
                if ($child -is [System.Windows.Forms.Button]) { $child.Left = $control.ClientSize.Width - $child.Width - 16 }
                elseif ($child -is [System.Windows.Forms.Label] -and $child.Font.Size -lt 10) {
                    $rightSpace = 16
                    if (@($control.Controls | Where-Object {$_ -is [System.Windows.Forms.Button]}).Count) {$rightSpace = 150}
                    $child.Width = [Math]::Max(100,$control.ClientSize.Width - $child.Left - $rightSpace)
                }
            }
        }
    }
}
$stack.Add_SizeChanged({Update-ChecklistLayout})
$form.Add_Shown({Update-ChecklistLayout})
$script:completedThisSession = $false
$completeButton.Add_Click({
    $completionDate = (Get-Date).ToString('o')
    try {
        Save-Checklist -CheckedIds @() -CompletionDate $completionDate
        $script:lastCompleted = $completionDate
        $script:savedChecks = @()
        $script:completedThisSession = $true
        [System.Windows.Forms.MessageBox]::Show('Your backup checklist completion has been recorded.','Backup Reminder') | Out-Null
        $form.Close()
    } catch {
        [System.Windows.Forms.MessageBox]::Show('Could not save completion: ' + $_.Exception.Message,'Backup Reminder') | Out-Null
    }
})
$form.Add_FormClosing({param($sender,$eventArgs)
    if (-not $script:completedThisSession) {
        try { Save-Checklist } catch {
            $eventArgs.Cancel = $true
            [System.Windows.Forms.MessageBox]::Show('Could not save progress: ' + $_.Exception.Message,'Backup Reminder') | Out-Null
        }
    }
})
. (Join-Path $PSScriptRoot 'ui\Menus.ps1')
if ($activationEvent) {
    $activationTimer = New-Object System.Windows.Forms.Timer
    $activationTimer.Interval = 200
    $activationTimer.Add_Tick({
        if ($activationEvent.WaitOne(0)) {
            if ($form.WindowState -eq 'Minimized') {$form.WindowState='Normal'}
            $form.Show()
            [void]$form.Activate()
        }
    })
    $activationTimer.Start()
}
Update-Progress
if ($PreviewPath) {
    # Render only this application's own controls; never launch backup applications or save progress.
    $initialAppearance = $script:appearanceChoice
    Set-AppAppearance 'Dark'
    if ($form.BackColor.ToArgb() -ne [Drawing.ColorTranslator]::FromHtml('#181B20').ToArgb()) {throw 'Dark appearance switch failed'}
    Set-AppAppearance 'Light'
    if ($form.BackColor.ToArgb() -ne [Drawing.ColorTranslator]::FromHtml('#F3F5F8').ToArgb()) {throw 'Light appearance switch failed'}
    Set-AppAppearance $initialAppearance
    if ($menuBar.Items.Count -ne 3 -or $viewHelpMenu.ShortcutKeys -ne [Windows.Forms.Keys]::F1 -or $editMenu.Enabled -or $scheduleMenu.Enabled) {throw 'Menu availability or Help shortcut is incorrect'}
    $form.Show()
    [System.Windows.Forms.Application]::DoEvents()
    if ($completeButton.Enabled -ne (@($script:boxes | Where-Object Checked).Count -eq $items.Count)) {throw 'Completion gate failed'}
    foreach ($panel in $stack.Controls) {
        if ($panel -is [System.Windows.Forms.Panel]) {
            foreach ($action in $panel.Controls) {
                if ($action -is [System.Windows.Forms.Button] -and $action.Visible -and ($action.Left -lt 0 -or $action.Right -gt $panel.ClientSize.Width)) {throw 'A launch button is outside its row'}
            }
        }
    }
    $originalChecks = @($script:boxes | ForEach-Object Checked)
    foreach ($check in $script:boxes) {$check.Checked = $true}
    if (-not $completeButton.Enabled -or $progressBar.Value -ne $items.Count) {throw 'All-complete gate failed'}
    $script:boxes[0].Checked = $false
    if ($completeButton.Enabled -or $progressBar.Value -ne ($items.Count - 1)) {throw 'Incomplete gate failed'}
    for ($i=0;$i -lt $script:boxes.Count;$i++) {$script:boxes[$i].Checked=$originalChecks[$i]}
    [System.Windows.Forms.Application]::DoEvents()
    $render = New-Object System.Drawing.Bitmap($form.Width,$form.Height)
    $form.DrawToBitmap($render,(New-Object System.Drawing.Rectangle(0,0,$form.Width,$form.Height)))
    $render.Save($PreviewPath,[System.Drawing.Imaging.ImageFormat]::Png)
    $scroll.ScrollControlIntoView($stack.Controls[$stack.Controls.Count-1])
    [System.Windows.Forms.Application]::DoEvents()
    $form.DrawToBitmap($render,(New-Object System.Drawing.Rectangle(0,0,$form.Width,$form.Height)))
    $lowerPreview = Join-Path (Split-Path -Parent $PreviewPath) (([System.IO.Path]::GetFileNameWithoutExtension($PreviewPath)) + '-jobs.png')
    $render.Save($lowerPreview,[System.Drawing.Imaging.ImageFormat]::Png)
    Show-AppHelp -RenderPath (Join-Path (Split-Path -Parent $PreviewPath) (([IO.Path]::GetFileNameWithoutExtension($PreviewPath))+'-help.png'))
    Show-AppAbout -RenderPath (Join-Path (Split-Path -Parent $PreviewPath) (([IO.Path]::GetFileNameWithoutExtension($PreviewPath))+'-about.png'))
    $form.Size = $form.MinimumSize
    $scroll.ScrollControlIntoView($stack.Controls[0])
    [System.Windows.Forms.Application]::DoEvents()
    $smallRender = New-Object System.Drawing.Bitmap($form.Width,$form.Height)
    $form.DrawToBitmap($smallRender,(New-Object System.Drawing.Rectangle(0,0,$form.Width,$form.Height)))
    $smallPreview = Join-Path (Split-Path -Parent $PreviewPath) (([System.IO.Path]::GetFileNameWithoutExtension($PreviewPath)) + '-narrow.png')
    $smallRender.Save($smallPreview,[System.Drawing.Imaging.ImageFormat]::Png)
    $smallRender.Dispose()
    $render.Dispose()
    $script:completedThisSession = $true
    $form.Close()
    'Rendered preview; completion gates passed; no state saved.'
} else { [void]$form.ShowDialog() }
$form.Dispose()
foreach ($image in $script:ownedImages) {$image.Dispose()}
} finally {
    if ($activationTimer) {$activationTimer.Stop();$activationTimer.Dispose()}
    if ($activationEvent) {$activationEvent.Dispose()}
    if ($ownsProfileMutex) {$instanceMutex.ReleaseMutex()}
    if ($instanceMutex) {$instanceMutex.Dispose()}
}
