# Development and verification

The 1.0.2 desktop release builds from `src/BackupReminder.Desktop/BackupReminder.Desktop.csproj`. It uses the shared templates in `src/DgxDesktop`. Every compile/resource reference is repository-relative; another application's checkout is not required.

## Build the installer and portable archive

Install the .NET 10 SDK and Inno Setup 7.1.1 on Windows x64. From this repository root:

```powershell
./scripts/Build-DesktopRelease.ps1 -Compiler 'C:/Program Files (x86)/Inno Setup 7/ISCC.exe'
```

Use `-DotNet` or `-Compiler` to select installed tool executables, and `-OutputDirectory` to select an empty output location. Defaults produce `artifacts/releases/Portable`, a portable ZIP, a Setup EXE and SHA256SUMS.txt. Restore requires the Windows runtime packs, either cached or available from NuGet. End users need no separate .NET runtime.

The portable archive contains only the executable, offline instructions, original license, runtime notices and portable.mode. It contains no user profile, test fixtures, private notes or machine-specific configuration. The installer omits portable.mode and uses the installed profile location. Building does not enable Windows startup or monitoring and does not publish anything.

## Isolated functional verification

Exit any running copy before testing. Run the packaged executable with an explicit disposable directory:

```powershell
./artifacts/releases/Portable/Backup-Reminder.exe --data-directory "$PWD/artifacts/verification" --verify
```

Inspect the generated JSON/text reports and process exit status. These checks use isolated profiles; they do not prove a real sign-in or a real destructive action. Never use personal backup files as disposable test input. Installer verification must preserve any existing installation and personal profile.

## Shared UI template updates

The three shared files are DgxAboutView.cs, ForgePresentation.xaml and Shell.xaml. Keep the files byte-identical across the three applications. `template-manifest.json` records their SHA-256 hashes and canonical source identity. Product-specific content stays in its own page adapters. Do not make per-app copies of common layout logic or tune each page separately.

After changing a template, update all consumers and hashes, rebuild each package, inspect light/dark states, and repeat the relevant behavior tests. The shared factories cover About, notices, setup, Help articles, settings rows, unsaved-change prompts and sample content. Date-picker button and calendar styling live in ForgePresentation.xaml.

## Release preparation

Confirm version metadata, Help and offline/online guides, installer wording, package allowlists, hashes, backup/restore round trips and install/reinstall/uninstall behavior. In an isolated Windows account, verify that the installed copy has a startup entry pointing to its executable. Leave startup enabled, uninstall, and confirm the entry is gone and the profile follows the selected Keep or Remove option. Record an untested option separately. Use generic optional samples for screenshots. Keep monitoring paused during presentation captures. Publishing and changing repository metadata are separate steps.
