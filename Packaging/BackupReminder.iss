#ifndef PackageDir
#error Supply PackageDir
#endif
[Setup]
AppId={{2B8BE26C-89CF-4335-952A-047CA28A7C47}
AppName=Backup Reminder
AppVersion=1.0.2
VersionInfoVersion=1.0.2.0
AppPublisher=DGX Development
AppPublisherURL=https://dgx.digitalguy.me/
DefaultDirName={localappdata}\Programs\Backup Reminder
DefaultGroupName=Backup Reminder
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.19045
OutputDir={#OutputFolder}
OutputBaseFilename=Backup-Reminder-1.0.2-Setup
SetupIconFile={#ArtworkDir}\brand-review.ico
UninstallDisplayIcon={app}\Backup-Reminder.exe
WizardStyle=modern dynamic
WizardSmallImageFile={#ArtworkDir}\brand-review.png
WizardSmallImageFileDynamicDark={#ArtworkDir}\brand-review.png
Compression=lzma2/fast
SolidCompression=yes
CloseApplications=no
RestartApplications=no
DisableProgramGroupPage=yes
[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; Flags: unchecked
[Files]
Source: "{#PackageDir}\Backup-Reminder.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#PackageDir}\README.txt"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#PackageDir}\LICENSE.txt"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#PackageDir}\DOTNET-LICENSE.txt"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#PackageDir}\DOTNET-THIRD-PARTY-NOTICES.txt"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#PackageDir}\WINDOWS-DESKTOP-LICENSE.txt"; DestDir: "{app}"; Flags: ignoreversion
[Icons]
Name: "{group}\Backup Reminder"; Filename: "{app}\Backup-Reminder.exe"; WorkingDir: "{app}"
Name: "{autodesktop}\Backup Reminder"; Filename: "{app}\Backup-Reminder.exe"; WorkingDir: "{app}"; Tasks: desktopicon
[Run]
Filename: "{app}\Backup-Reminder.exe"; Description: "Open Backup Reminder"; Flags: nowait postinstall skipifsilent
[Code]
function GetFileAttributes(Path: String): LongInt;
 external 'GetFileAttributesW@kernel32.dll stdcall';
function SafeTree(Path: String): Boolean;
var Find: TFindRec;
begin
 Result := False;
 if (GetFileAttributes(Path) and $400) <> 0 then Exit;
 if FindFirst(AddBackslash(Path) + '*', Find) then begin
  try
   repeat
    if (Find.Name <> '.') and (Find.Name <> '..') then begin
     if (Find.Attributes and $400) <> 0 then Exit;
     if (Find.Attributes and FILE_ATTRIBUTE_DIRECTORY) <> 0 then
      if not SafeTree(AddBackslash(Path) + Find.Name) then Exit;
    end;
   until not FindNext(Find);
  finally FindClose(Find); end;
 end;
 Result := True;
end;
function InitializeUninstall(): Boolean;
var Code: Integer;
begin
 Result := Exec(ExpandConstant('{app}\Backup-Reminder.exe'), '--data-directory "' + ExpandConstant('{localappdata}\Backup Reminder') + '" --remove-integrations', '', SW_HIDE, ewWaitUntilTerminated, Code) and (Code = 0);
 if not Result then MsgBox('Close Backup Reminder and try again. Uninstall could not finish removing Windows registrations and has stopped.', mbError, MB_OK);
end;
function GetDpiForWindow(hWnd: THandle): Integer;
 external 'GetDpiForWindow@user32.dll stdcall';
function RemoveSettingsChosen(): Boolean;
var Form: TSetupForm; Heading, Detail: TNewStaticText; KeepButton, RemoveButton: TNewButton; Dpi, Inset, ContentWidth, Gap: Integer;
begin
 Form := CreateCustomForm(400, 240, False, True);
 try
  Form.Caption := 'Backup Reminder Uninstall';
  Dpi := GetDpiForWindow(Form.Handle); if Dpi = 0 then Dpi := 96;
  Form.Width := MulDiv(400, Dpi, 96);
  Inset := MulDiv(20, Dpi, 96); Gap := MulDiv(10, Dpi, 96);
  ContentWidth := Form.ClientWidth - 2 * Inset;
  Heading := TNewStaticText.Create(Form); Heading.Parent := Form;
  Heading.AutoSize := False; Heading.WordWrap := True;
  Heading.Left := Inset; Heading.Top := Inset; Heading.Width := ContentWidth;
  Heading.Font.Height := -MulDiv(16, Dpi, 96);
  Heading.Caption := 'Keep Backup Reminder settings?'; Heading.AdjustHeight;
  Detail := TNewStaticText.Create(Form); Detail.Parent := Form;
  Detail.AutoSize := False; Detail.WordWrap := True;
  Detail.Left := Inset; Detail.Top := Heading.Top + Heading.Height + Gap; Detail.Width := ContentWidth;
  Detail.Caption := 'Keep settings retains your settings for reinstall. Remove settings deletes its checklist, progress, icons, sound and recovery copy. Windows registrations belonging to this installation are removed either way.';
  Detail.AdjustHeight;
  KeepButton := TNewButton.Create(Form); KeepButton.Parent := Form;
  KeepButton.Style := bsCommandLink; KeepButton.Caption := 'Keep settings';
  KeepButton.Font.Height := MulDiv(KeepButton.Font.Height, 12, 9);
  KeepButton.Left := Inset; KeepButton.Top := Detail.Top + Detail.Height + Gap; KeepButton.Width := ContentWidth;
  KeepButton.Height := MulDiv(50, Dpi, 96); KeepButton.AdjustHeightIfCommandLink;
  KeepButton.ModalResult := mrCancel; KeepButton.Cancel := True; KeepButton.Default := True;
  RemoveButton := TNewButton.Create(Form); RemoveButton.Parent := Form;
  RemoveButton.Style := bsCommandLink; RemoveButton.Caption := 'Remove settings';
  RemoveButton.Font.Height := MulDiv(RemoveButton.Font.Height, 12, 9);
  RemoveButton.Left := Inset; RemoveButton.Top := KeepButton.Top + KeepButton.Height + Gap; RemoveButton.Width := ContentWidth;
  RemoveButton.Height := KeepButton.Height; RemoveButton.AdjustHeightIfCommandLink;
  RemoveButton.ModalResult := mrYes;
  Form.ClientHeight := RemoveButton.Top + RemoveButton.Height + Inset;
  Form.ActiveControl := KeepButton;
  Form.FlipAndCenterIfNeeded(True, UninstallProgressForm, False);
  Result := Form.ShowModal() = mrYes;
 finally Form.Free(); end;
end;
procedure CurUninstallStepChanged(Step: TUninstallStep);
var Profile: String;
begin
 if (Step <> usPostUninstall) or UninstallSilent then Exit;
 Profile := ExpandConstant('{localappdata}\Backup Reminder');
 if not DirExists(Profile) then Exit;
 if not RemoveSettingsChosen() then Exit;
 if SafeTree(Profile) then begin
  if not DelTree(Profile, True, True, True) then MsgBox('Some settings could not be removed. Your external documents and backup tools were not changed.', mbInformation, MB_OK);
 end else MsgBox('The profile contains a linked folder or file and has been left in place. Remove it manually if intended.', mbInformation, MB_OK);
end;
