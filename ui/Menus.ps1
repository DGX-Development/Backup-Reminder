# Menu commands and information windows. Loaded after the main checklist controls.
$script:appVersion = '0.1.0-dev'
$script:projectWebsite = 'https://dgx.digitalguy.me/projects/backup-reminder.html'
$script:issuesWebsite = 'https://github.com/DGX-Development/Backup-Reminder/issues'
# Enable each destination after it has been published and verified.
$script:projectWebsiteAvailable = $false
$script:issuesWebsiteAvailable = $false
function Open-AppWebsite([string]$Url) {
    try { Start-Process -FilePath $Url -ErrorAction Stop | Out-Null }
    catch { [void][System.Windows.Forms.MessageBox]::Show($form,'Unable to open your browser. Please try again.','Backup Reminder','OK','Warning') }
}
$script:appearanceChoice = $Appearance
if (-not ('BackupReminderMenuColors' -as [type])) {
    Add-Type -ReferencedAssemblies @([System.Windows.Forms.MenuStrip].Assembly.Location,[System.Drawing.Color].Assembly.Location) -TypeDefinition @"
using System.Drawing;
using System.Windows.Forms;
public class BackupReminderMenuColors : ProfessionalColorTable {
    public Color Surface { get; set; }
    public Color Hover { get; set; }
    public Color Line { get; set; }
    public override Color ToolStripDropDownBackground { get { return Surface; } }
    public override Color ImageMarginGradientBegin { get { return Surface; } }
    public override Color ImageMarginGradientMiddle { get { return Surface; } }
    public override Color ImageMarginGradientEnd { get { return Surface; } }
    public override Color MenuItemSelected { get { return Hover; } }
    public override Color MenuItemSelectedGradientBegin { get { return Hover; } }
    public override Color MenuItemSelectedGradientEnd { get { return Hover; } }
    public override Color MenuItemPressedGradientBegin { get { return Hover; } }
    public override Color MenuItemPressedGradientMiddle { get { return Hover; } }
    public override Color MenuItemPressedGradientEnd { get { return Hover; } }
    public override Color MenuItemBorder { get { return Line; } }
    public override Color MenuBorder { get { return Line; } }
    public override Color SeparatorDark { get { return Line; } }
    public override Color SeparatorLight { get { return Surface; } }
}
"@
}
function Update-MenuColors {
    $table = New-Object BackupReminderMenuColors
    $table.Surface = Color 'Surface'
    $table.Hover = Color 'Button'
    $table.Line = Color 'Line'
    $menuBar.Renderer = New-Object System.Windows.Forms.ToolStripProfessionalRenderer($table)
    $menuBar.BackColor = Color 'Surface'
    $menuBar.ForeColor = Color 'Text'
    function Color-MenuItems($entries) {
        foreach ($entry in $entries) {
            $entry.ForeColor = Color 'Text'
            $entry.BackColor = Color 'Surface'
            if ($entry -is [System.Windows.Forms.ToolStripMenuItem]) {Color-MenuItems $entry.DropDownItems}
        }
    }
    Color-MenuItems $menuBar.Items
}
function Set-AppAppearance([string]$choice) {
    $oldPalette = $script:palette
    $newDark = $choice -eq 'Dark'
    if ($choice -eq 'System') {
        try {$newDark = (Get-ItemPropertyValue -LiteralPath 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize' -Name AppsUseLightTheme) -eq 0} catch {$newDark=$false}
    }
    $records = New-Object System.Collections.Generic.List[object]
    function Collect-ThemeControls($control) {
        $records.Add(@{Control=$control;Back=$control.BackColor.ToArgb();Fore=$control.ForeColor.ToArgb()})
        foreach ($child in $control.Controls) {Collect-ThemeControls $child}
    }
    Collect-ThemeControls $form
    if ($newDark) {
        $script:palette = @{Window='#181B20';Surface='#22262D';Text='#F2F4F8';Muted='#B0B8C6';Line='#363E49';Accent='#78B7FF';Button='#303845';ButtonText='#E8EEF8';Primary='#78B7FF';PrimaryText='#142337';Disabled='#353C46'}
    } else {
        $script:palette = @{Window='#F3F5F8';Surface='#FFFFFF';Text='#1C2534';Muted='#596779';Line='#DCE2EB';Accent='#2468BB';Button='#EDF2F9';ButtonText='#235993';Primary='#2165B6';PrimaryText='#FFFFFF';Disabled='#DDE3EB'}
    }
    foreach ($record in $records) {
        foreach ($role in $oldPalette.Keys) {
            $oldColor=[System.Drawing.ColorTranslator]::FromHtml($oldPalette[$role]).ToArgb()
            if ($record.Back -eq $oldColor) {$record.Control.BackColor=Color $role}
            if ($record.Fore -eq $oldColor) {$record.Control.ForeColor=Color $role}
        }
    }
    $script:appearanceChoice=$choice
    foreach ($entry in $appearanceMenu.DropDownItems) {$entry.Checked=$entry.Tag -eq $choice}
    Style-Button $completeButton $true
    Update-Progress
    Update-MenuColors
    $form.Refresh()
}
function Show-InformationWindow($dialog,[string]$renderPath) {
    $dialog.Icon=$form.Icon
    $dialog.StartPosition='CenterParent'
    $dialog.BackColor=Color 'Surface'
    $dialog.ForeColor=Color 'Text'
    $dialog.Font=$bodyFont
    $dialog.MinimizeBox=$false
    if ($renderPath) {
        $dialog.Show()
        [System.Windows.Forms.Application]::DoEvents()
        $bitmap=New-Object System.Drawing.Bitmap($dialog.Width,$dialog.Height)
        $dialog.DrawToBitmap($bitmap,(New-Object System.Drawing.Rectangle(0,0,$dialog.Width,$dialog.Height)))
        $bitmap.Save($renderPath,[System.Drawing.Imaging.ImageFormat]::Png)
        $bitmap.Dispose()
        $dialog.Close()
    } else {[void]$dialog.ShowDialog($form)}
    $dialog.Dispose()
}
function Show-AppHelp([string]$RenderPath) {
    $topics=[ordered]@{
        'Getting started'="Use Open to launch the tool for a backup step. Perform the backup yourself, review its results, then check the item.`r`n`r`nOpening a tool does not mark a backup complete. Backup Reminder does not copy files or run backup jobs automatically."
        'Items and grouped jobs'="Standalone items have their own Open buttons.`r`n`r`nA category can have one tool launcher and several jobs underneath it. Check each job after completing it. The category heading is not an extra task.`r`n`r`nThe development examples use Notepad and File Explorer. Replace their targets with your preferred tools when configuring your checklist."
        'Programs, folders and icons'="Open can launch a configured program, folder, or HTTP/HTTPS webpage. Windows folder targets can follow your Documents or Pictures location automatically.`r`n`r`nCustom icons are independent of launch targets. An icon can come from a local PNG, ICO, or installed executable.`r`n`r`nThe visual checklist editor is still being built; this version reads the selected profile's settings.json."
        'Progress and completion'="Close for now or File > Exit saves partial progress.`r`n`r`nComplete backups becomes available after all items are checked. It records your confirmation and closes the window. The next session starts with unchecked items.`r`n`r`nCompletion is your confirmation; the app does not independently verify backup integrity or restoreability."
        'Reminders and schedules'="Scheduling is not enabled in this development build. No Windows scheduled task has been installed.`r`n`r`nThe planned schedule supports a monthly day/time or a repeating interval. Completing an extra session will not move or skip its regular scheduled reminder."
        'Development and personal use'="The Development window uses an isolated starter checklist and separate progress.`r`n`r`nYour personal shortcut opens your own profile. Changing or completing development examples does not replace personal checklist progress.`r`n`r`nOnly one window opens per data profile. Launching the same profile again activates its existing window."
        'Keyboard and appearance'="Alt+F opens File. Alt+S opens Settings. Alt+H opens Help. F1 opens this help window. Tab moves between controls; Space toggles a focused checkbox.`r`n`r`nSettings > Appearance switches between System, Light, and Dark for the current session. System reads the Windows app appearance preference."
    }
    $dialog=New-Object System.Windows.Forms.Form
    $dialog.Text='Backup Reminder Help'
    $dialog.Size=New-Object System.Drawing.Size(740,550)
    $dialog.MinimumSize=New-Object System.Drawing.Size(640,450)
    $search=New-Object System.Windows.Forms.TextBox
    $searchLabel=New-Object System.Windows.Forms.Label
    $searchLabel.Text='Search help'
    $searchLabel.SetBounds(20,21,95,25)
    $dialog.Controls.Add($searchLabel)
    $search.PlaceholderText='Enter a word or topic'
    $search.SetBounds(118,18,582,30)
    $search.Anchor='Top,Left,Right'
    $search.BackColor=Color 'Window';$search.ForeColor=Color 'Text'
    $dialog.Controls.Add($search)
    $topicList=New-Object System.Windows.Forms.ListBox
    $topicList.SetBounds(20,63,205,390)
    $topicList.Anchor='Top,Bottom,Left'
    $topicList.BorderStyle='None'
    $topicList.BackColor=Color 'Window';$topicList.ForeColor=Color 'Text'
    $topicList.IntegralHeight=$false
    $dialog.Controls.Add($topicList)
    $content=New-Object System.Windows.Forms.RichTextBox
    $content.SetBounds(243,63,457,390)
    $content.Anchor='Top,Bottom,Left,Right'
    $content.ReadOnly=$true
    $content.BorderStyle='None'
    $content.BackColor=Color 'Surface';$content.ForeColor=Color 'Text'
    $content.Font=New-Object System.Drawing.Font('Segoe UI',11)
    $dialog.Controls.Add($content)
    $helpClose=New-Object System.Windows.Forms.Button
    $helpClose.Text='Close';$helpClose.SetBounds(580,466,120,34);$helpClose.Anchor='Bottom,Right'
    Style-Button $helpClose
    $helpClose.Add_Click({$dialog.Close()})
    $dialog.Controls.Add($helpClose)
    $dialog.CancelButton=$helpClose
    $topicList.Add_SelectedIndexChanged({
        if($topicList.SelectedItem){$content.Text=[string]$topicList.SelectedItem+"`r`n`r`n"+$topics[[string]$topicList.SelectedItem]}
    })
    $search.Add_TextChanged({
        $query=$search.Text.Trim()
        $topicList.Items.Clear()
        foreach($topic in $topics.Keys){if(-not $query -or $topic.IndexOf($query,[StringComparison]::OrdinalIgnoreCase) -ge 0 -or $topics[$topic].IndexOf($query,[StringComparison]::OrdinalIgnoreCase) -ge 0){[void]$topicList.Items.Add($topic)}}
        if($topicList.Items.Count){$topicList.SelectedIndex=0}else{$content.Text='No matching help topics.'}
    })
    foreach($topic in $topics.Keys){[void]$topicList.Items.Add($topic)}
    $topicList.SelectedIndex=0
    if ($RenderPath) {
        $search.Text='progress'
        if ($topicList.Items.Count -lt 1) {throw 'Help search did not find progress'}
        $search.Text='unmatched-search-test-9751'
        if ($topicList.Items.Count -ne 0 -or $content.Text -ne 'No matching help topics.') {throw 'Empty help search failed'}
        $search.Text=''
        if ($topicList.Items.Count -ne $topics.Count) {throw 'Help search did not restore topics'}
    }
    Show-InformationWindow $dialog $RenderPath
}
function Show-AppAbout([string]$RenderPath) {
    $dialog=New-Object System.Windows.Forms.Form
    $dialog.Text='About Backup Reminder'
    $dialog.Size=New-Object System.Drawing.Size(530,405)
    $dialog.FormBorderStyle='FixedDialog';$dialog.MaximizeBox=$false
    $picture=New-Object System.Windows.Forms.PictureBox
    $picture.Image=$brand.Image;$picture.SizeMode='Zoom';$picture.SetBounds(25,24,76,76)
    $dialog.Controls.Add($picture)
    $title=New-Object System.Windows.Forms.Label
    $title.Text='Backup Reminder';$title.Font=New-Object System.Drawing.Font('Segoe UI Semibold',19);$title.SetBounds(119,25,365,37)
    $dialog.Controls.Add($title)
    $version=New-Object System.Windows.Forms.Label
    $version.Text='Version '+$script:appVersion;$version.SetBounds(122,68,355,25);$version.ForeColor=Color 'Muted'
    $dialog.Controls.Add($version)
    $description=New-Object System.Windows.Forms.Label
    $description.Text="A checklist and tool launcher for your manual backup routine.`r`n`r`nCreated by DGX Development.`r`n`r`nDevelopment build. Reminder scheduling is not yet enabled. Application icons belong to their respective owners."
    $description.SetBounds(28,119,465,145)
    $dialog.Controls.Add($description)
    $website=New-Object System.Windows.Forms.LinkLabel
    $website.Text=if($script:projectWebsiteAvailable){'Project website'}else{'Project website (coming soon)'}
    $website.SetBounds(28,268,330,25)
    $website.LinkColor=Color 'Accent';$website.ActiveLinkColor=Color 'Accent'
    $website.Enabled=$script:projectWebsiteAvailable
    $website.Add_LinkClicked({Open-AppWebsite $script:projectWebsite})
    $dialog.Controls.Add($website)
    $aboutClose=New-Object System.Windows.Forms.Button
    $aboutClose.Text='Close';$aboutClose.SetBounds(373,315,120,35);Style-Button $aboutClose
    $aboutClose.Add_Click({$dialog.Close()})
    $dialog.Controls.Add($aboutClose);$dialog.CancelButton=$aboutClose
    Show-InformationWindow $dialog $RenderPath
}
$menuBar=New-Object System.Windows.Forms.MenuStrip
$menuBar.Font=$bodyFont
$menuBar.Padding=New-Object System.Windows.Forms.Padding(15,4,0,4)
$menuBar.Dock='Top';$menuBar.ShowItemToolTips=$true
$fileMenu=New-Object System.Windows.Forms.ToolStripMenuItem('&File')
$editMenu=New-Object System.Windows.Forms.ToolStripMenuItem('Edit checklist...')
$editMenu.Enabled=$false;$editMenu.ToolTipText='The visual checklist editor is under development.'
[void]$fileMenu.DropDownItems.Add($editMenu)
[void]$fileMenu.DropDownItems.Add((New-Object System.Windows.Forms.ToolStripSeparator))
$exitMenu=New-Object System.Windows.Forms.ToolStripMenuItem('E&xit')
$exitMenu.Add_Click({$form.Close()});[void]$fileMenu.DropDownItems.Add($exitMenu)
$settingsMenu=New-Object System.Windows.Forms.ToolStripMenuItem('&Settings')
$scheduleMenu=New-Object System.Windows.Forms.ToolStripMenuItem('Reminder schedule...')
$scheduleMenu.Enabled=$false;$scheduleMenu.ToolTipText='Reminder scheduling is not yet implemented.'
[void]$settingsMenu.DropDownItems.Add($scheduleMenu)
$appearanceMenu=New-Object System.Windows.Forms.ToolStripMenuItem('&Appearance')
foreach($choice in @('System','Light','Dark')){
    $entry=New-Object System.Windows.Forms.ToolStripMenuItem($choice)
    $entry.Tag=$choice;$entry.Checked=$choice -eq $Appearance
    $entry.Add_Click({param($sender,$eventArgs) Set-AppAppearance ([string]$sender.Tag)})
    [void]$appearanceMenu.DropDownItems.Add($entry)
}
[void]$settingsMenu.DropDownItems.Add($appearanceMenu)
$helpMenu=New-Object System.Windows.Forms.ToolStripMenuItem('&Help')
$viewHelpMenu=New-Object System.Windows.Forms.ToolStripMenuItem('View &Help')
$viewHelpMenu.ShortcutKeys=[System.Windows.Forms.Keys]::F1
$viewHelpMenu.Add_Click({Show-AppHelp});[void]$helpMenu.DropDownItems.Add($viewHelpMenu)
$reportIssueMenu=New-Object System.Windows.Forms.ToolStripMenuItem('Report an &issue')
$reportIssueMenu.Enabled=$script:issuesWebsiteAvailable
$reportIssueMenu.ToolTipText='GitHub issue reporting will be available when the repository is published.'
$reportIssueMenu.Add_Click({Open-AppWebsite $script:issuesWebsite})
[void]$helpMenu.DropDownItems.Add($reportIssueMenu)
[void]$helpMenu.DropDownItems.Add((New-Object System.Windows.Forms.ToolStripSeparator))
$aboutMenu=New-Object System.Windows.Forms.ToolStripMenuItem('&About Backup Reminder')
$aboutMenu.Add_Click({Show-AppAbout});[void]$helpMenu.DropDownItems.Add($aboutMenu)
[void]$menuBar.Items.Add($fileMenu);[void]$menuBar.Items.Add($settingsMenu);[void]$menuBar.Items.Add($helpMenu)
$form.MainMenuStrip=$menuBar
$form.Controls.Add($menuBar)
Update-MenuColors
