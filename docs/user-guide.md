# Backup Reminder user guide

Version 1.0.2. Prepared locally; publication pending.

## Getting started

Backup Reminder helps you remember and track the backups you perform. It does not run backup jobs or verify their results.

### Start your checklist
1. Choose Edit checklist to add your tasks, or restore a configuration backup.
2. Open the tool, folder or website for a task.
3. Perform the backup and review the result.
4. Check the item only when that work is complete.

### Keep in mind
- New profiles start empty.
- Opening a tool does not check off the task.
- Complete backups records your confirmation, not proof that your files can be restored.

## Setup wizard

The first manual launch of a new profile opens setup. Help > Run setup again opens it later.

Next and Back move between pages. Choose appearance, window behavior and reminder timing, then Finish to save and apply them.

Appearance changes in the wizard are applied at Finish. Skip setup closes the wizard without applying those choices; the existing checklist remains available.

Restore from a backup opens a Backup Reminder configuration ZIP. After confirmation, restoration applies immediately and closes setup; you do not need to click Finish.

Your previous configuration is kept in recovery/before-restore.zip. Reminders are disabled after restoration.

Review Settings and program paths before enabling them again.

## Edit checklist

Select a task or group in the left list to edit it. Add task creates a separate task; choose its Group to place it under a group heading.

Add group creates a heading that can have a shared launcher. A group is not itself a checkable task.

Its indented tasks can be checked separately.

Name labels the entry. Description explains the backup step.

What to open selects Nothing, Program or file, Folder, or Website. Target is the program/file path, folder path or full HTTP/HTTPS website address.

Include https:// for a website. Browse target selects a local file or folder.

Button label supplies the launcher text. Choose None (separate task) to keep a task outside groups.

Drag the dotted handle, or use Ctrl+Up/Down, to reorder within a section. Moving a group moves its section.

Use the Group field to change a task's membership. Choose icon selects a local image or an icon from an EXE/DLL.

Use target icon clears the custom selection. Save changes applies the draft and leaves the editor open.

Saved means there are no pending edits.

## Arguments and working directory

Advanced contains optional launch settings. Most tasks can leave both fields blank.

Arguments are passed to a program or file, not a folder or website. For Notepad, an example argument is "D:\Notes\Backup notes.txt".

Quote paths that contain spaces and follow the target program's argument syntax.

Working directory is the starting folder for a program/file. When blank, the app uses the target file's folder.

A custom path can contain Windows environment variables. This field does not select where your backups are stored.

Only configure programs and arguments you trust.

## Saving and unsaved changes

In Edit checklist, Save changes saves without closing. Closing an edited checklist asks whether to Save changes, Discard changes or Keep editing.

The confirmation's Escape or X keeps editing.

Settings uses Save and Cancel. Save applies all three settings pages and closes the window.

Cancel or X discards unsaved settings and restores the saved appearance. Settings does not show the editor's unsaved-changes prompt.

Preview sound does not save changes. A failed save reports an error and leaves the window open.

Main checklist checkmarks are saved as they change.

## Progress and completion

Check each item only after performing and reviewing its backup. The timestamp records when you checked the box, not proof of when a backup ran.

Unchecking clears that timestamp.

Complete backups becomes available when the checklist has at least one task and all tasks are checked. It records completion, clears the checkmarks and shows a confirmation; the window stays open.

An unfinished checklist retains its progress when another reminder arrives. Backup Reminder does not independently verify backup integrity.

Close for now and File > Exit quit the app. By default, X and Minimize hide it in the system tray.

Double-click the tray icon or choose Open Backup Reminder from its menu to reopen it. Settings > Window controls these behaviors.

## Reminders and schedules

Open Settings > Reminder schedule, click the reminder icon beside Pin, or right-click the tray icon and choose Reminder schedule. The tray command uses a calendar icon and opens the schedule page directly.

Turn on Remind me to back up. Choose Every month with a day, or Every few days with an interval of 1-365 days and a start date.

Type a starting date or use the calendar button to choose it. The date field and calendar follow the selected appearance.

Dates use MM/DD/YYYY and times use a 12-hour clock with AM/PM, in your local time. Days 29-31 use the last day in a shorter month.

Review the summary, then Save. During setup, Finish applies these choices.

Save registers or updates a real Windows scheduled task for this profile. Turn Remind me to back up off and Save to remove that task while retaining the timing choices.

The main reminder icon reflects the actual Windows task state; its tooltip provides status and the next run when available.

Reminders can launch the app when it is closed and reopen it from the tray or a minimized state. An existing visible window and open editor are preserved.

Sound is requested once if enabled; the taskbar is highlighted when the app is not foreground. No backup jobs run automatically.

You must be signed in. The task uses your Windows account without a stored password or administrator elevation.

It does not wake the PC. Windows may run a missed reminder later.

Completing a checklist early does not move the schedule. Each profile owns a separate task.

## Reminder sound

Sound offers No sound, Windows default, or Custom WAV. Preview sound tries the unsaved selection.

For Custom WAV, Browse chooses a file and starts beside the selected file when its folder is available, otherwise in Windows Media. Save in Settings, or Finish in setup, keeps the choice and imports the WAV into the profile.

Use a supported WAV file; renaming another audio format to .wav does not convert it. Preview reports invalid files.

At reminder time, a missing or unplayable custom sound falls back to the Windows Information sound. Windows volume and sound settings still apply.

Manual launches are quiet.

## Appearance and window behavior

Settings > Appearance offers System, Light and Dark with a fixed blue accent. Changes preview immediately in Settings; Save keeps them and Cancel restores the saved appearance.

Pin keeps the main window above ordinary windows and uses the accent color when enabled.

Settings > Window controls close/minimize to tray, Start when I sign in to Windows and Start minimized in the system tray. Startup choices take effect after Save.

Closing the app does not remove a scheduled reminder or sign-in registration.

Lock window sizes preserves chosen sizes while allowing moves and minimization. Settings remains resizable so you can unlock it.

Reset window sizes takes effect when you Save; it clears the lock and saved sizes. Window preferences are included in configuration backups.

## Configuration and backups

File > Back up configuration saves a ZIP containing checklist settings, progress, window preferences, available custom icons and reminder sound. Available program-derived icons are embedded too.

It does not copy your backup tools, documents or Windows task definitions. Missing files are reported.

Keep configuration ZIPs private because they contain personal paths and preferences.

File > Restore configuration validates the ZIP and keeps the prior configuration in recovery/before-restore.zip. It turns reminders off.

Review paths and Settings before re-enabling reminders. Restoring preferences does not automatically register Windows startup.

If this profile already had a sign-in entry, review and save the startup choice to update or remove it.

Custom artwork and WAV files are copied into managed icons and sounds folders on save; originals stay untouched. For a manual backup, exit the app and copy the entire profile folder.

Do not replace your data with starter examples.

## Install, portable and uninstall

The installer defaults to Programs\Backup Reminder under your Windows LocalAppData folder; its profile is %LOCALAPPDATA%\Backup Reminder. You can choose a different application folder during installation.

Installing alone does not enable reminders or Windows startup; setup appears when you launch a new profile.

For portable use, extract the whole ZIP and run Backup-Reminder.exe. Keep portable.mode beside it.

The profile folder beside the executable is created on first launch, or reused if already present. The portable app does not automatically import an installed profile.

Restore a configuration ZIP during setup or from File.

Before moving or deleting a portable folder, turn off reminders and Windows startup and Save. After moving, open the new location and enable the choices again.

Do not discard the profile folder.

Exit the installed app before uninstalling. Uninstall removes that installed profile's Windows reminder and sign-in registrations, then offers Keep settings or Remove settings.

Keep retains the profile for reinstall; Remove deletes that profile, including its managed icons, sound and recovery copy. External documents, tools and separately saved backup ZIPs are left alone.

Silent uninstall keeps settings. If the profile contains a linked file or folder, Remove settings leaves the profile in place and explains why. The installer does not remove registrations belonging to a separate portable profile.

## Profiles and updates

The active profile contains settings.json, checklist.json and window-preferences.json, plus managed icons/sounds and setup/window state. An explicit --data-directory selects another profile.

Only one app instance opens per profile. Relaunching activates its existing window or dialog.

Help > Check for updates reads the latest stable GitHub release. It reports an available update, an equal version, or that this copy is newer.

There is no automatic updater.

Exit before replacing application files. Keep the profile and use the same executable path to preserve registrations.

For portable updates, replace the supplied application files without replacing or deleting your profile folder. If moving the app, disable reminder/startup registrations first and re-enable them from the new location.

## Website and support

Help > Report an issue asks before opening Backup Reminder's public GitHub new-issue page in your browser. A GitHub account is required to submit an issue.

Nothing is submitted automatically and Backup Reminder stays open. Include the version and steps to reproduce the problem, leaving out private paths and data.

About links to dgx.digitalguy.me and includes license information.

## Keyboard

F1 opens Help. Ctrl+Q exits.

Tab moves between controls and Space toggles a focused checkbox. In Edit checklist, the dotted handle supports dragging and Ctrl+Up/Down reordering.

Escape cancels dialogs where Cancel or Close is available; an unsaved editor may first ask you to keep or discard changes.

## Sample content

Samples are optional. New profiles start empty.

### Load examples
1. Open Help > Run setup again, or use the welcome page on first launch.
2. Choose Load sample content. This applies immediately.
3. Explore the entries labeled Sample, then edit them or add your own.

Loading again does not duplicate existing sample IDs. Loading does not replace your content or change reminder, monitoring or startup settings. Restoring a backup does not automatically load samples.

### Remove examples
Choose Remove samples on the same page. It removes only unchanged sample entries. Edited or completed examples are kept; you can remove those individually using the editor. Linked files are not deleted.

