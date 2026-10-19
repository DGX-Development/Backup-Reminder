Backup Reminder (0.2.1)
DGX Development

RUN
Extract this ZIP into a folder and double-click Backup-Reminder.exe.
This build targets Windows x64 and includes its .NET runtime. PowerShell is
not required to run it. No installer or background service is created automatically. This is an unsigned release. Windows may show an unknown-publisher warning.

CUSTOM FILES AND GROUPS
Imported icons and sounds keep readable filenames. Different files sharing a name
get numbered suffixes; identical imported files are reused. Originals stay untouched.
Older hashed names migrate on Save. Custom icon Browse offers icons from the current file/program, or Windows shell32
when neither is available. Browse inside the chooser selects another source file.
Individual library icons become managed PNGs on Save. The editor shows filenames. Choose a Group to place tasks under
one heading, or None (show as a separate task) for an independent item.
Configuration backups validate asset checksums and still accept older backups.

BROWSE DEFAULTS
Program/file: current target folder, otherwise Program Files.
Folder: current folder (including Windows known folders), otherwise Documents.
Working directory: current folder, program folder, then Documents.
Custom icon: current custom file, selected program, then Windows shell32 icons.
Sound: current sound folder, otherwise Windows Media.
Configuration backup/restore: Documents.
Cancel leaves the existing selection unchanged.

YOUR DATA AND BACKUPS
The default profile is stored in:
%LOCALAPPDATA%\Backup Reminder

settings.json contains checklist items, groups, launch targets, icon choices and
reminder timing and sound preferences. Use Settings > Open data folder to find
the active profile.
checklist.json contains saved checkmarks, per-item checkedAt timestamps and the
last completion timestamp. Checked times record checkbox confirmation, not
verified backups. Unchecking or completing clears the relevant item times.
Close the app before copying these files for backup or restoring them.
Save imports custom icons into icons/ and WAV sounds into sounds/ in the data
folder. Originals stay untouched. EXE icons are extracted as PNG images. Discarding
unsaved edits imports nothing. Relative asset paths keep working when the profile is moved.
Existing external references work and are imported on the next settings save
when available. Missing files keep their references and normal fallback.

File > Back up configuration exports settings, progress, icons and sound to ZIP.
File > Restore configuration validates a backup and preserves the current one
in recovery/before-restore.zip. Restore turns reminders off. Review program paths
and enable reminders in Reminder schedule when ready. Configuration ZIPs contain
personal paths/preferences; keep them private. Tools, documents and Windows
tasks are not included. Unavailable custom files are reported.
The starter does not contain your personal backup configuration.
Use File > Edit checklist to customize tasks, groups, paths and icons.
What to open can be Nothing for a checklist-only step with no Open button.
The checklist editor and Reminder schedule show a disabled Saved button when
unchanged. Edits enable Save changes or Save schedule; successful saves keep
the page open and return to Saved. Sound Preview does not count as an edit.
Cancel, Close, X or Escape with unsaved changes offers Save changes, Discard
changes or Keep editing. Failed saves keep the page open with edits intact.
Already saved pages close immediately. Main-window checkmarks still save on close.
Settings > Reminder schedule: choose a monthly day/time, turn on Enable scheduled
reminders, and Save schedule to create a real Windows task. The page displays
its actual status and next run. The task appears directly under Task Scheduler
Library as Backup Reminder - followed by your Windows username. Additional
profiles include a readable label, with a number if needed. Turn Enable off
and click Save schedule to remove this profile's task; timing preferences remain.
Date and time follow Windows regional settings. A calendar button selects the
interval start date, and a live summary confirms your choices.
Intervals from 1 to 365 days are also supported. Shorter months use their last day.
You must be signed in to Windows. The task opens the checklist, never backup jobs.
Completing backups early leaves the regular schedule unchanged.
Play a sound for scheduled reminders is on by default and can be turned off in
the Notification section. Expand Sound options for a custom .wav file, Browse
and Preview. Preview tries your unsaved choice; Save schedule keeps it. Saved
sounds show their original filename rather than an internal storage name. Browse
opens beside the selected file, or Windows Media when that folder is unavailable. Older
saved sounds may show Saved custom sound until reselected with Browse. A file
renamed to .wav may contain a different audio format; convert it to PCM WAV. If a
custom file cannot play, the Windows sound is used. Saved custom WAV files are included in configuration backups.
Manual launches are quiet. A scheduled reminder plays once
and briefly highlights the taskbar if needed, without interrupting open edits
or forcing an existing window to the front. Manual relaunches activate it.
Close saves unfinished checkmarks and exits completely. Complete backups records
a finished session. Later reminders keep unfinished progress; no tray app remains.
Keep the executable in a permanent folder. If you move it, open it from the new
location and save the schedule again. Disable reminders before resetting a profile.
Existing timing preferences are not automatically enabled during an update.
Owned tasks with recognized actions migrate from identifier names to readable
names when the app next opens, preserving the schedule and enabled state.
Windows tasks are separate from the JSON files. After restoring settings on
another PC, review paths, enable reminders in Reminder schedule and Save schedule
to register the task there.

UPDATES
1. Close Backup Reminder.
2. Download and extract the new release ZIP.
3. Replace all supplied application files in the same app folder.
4. Reopen Backup Reminder.
Keep the separate data folder, including icons/ and sounds/. Keeping the EXE
at the same path preserves the scheduled reminder. After moving it, save the
schedule again from the new location. Never replace personal data with starter
examples. Automatic update checking/installation is not implemented.

PROFILES
--development uses %LOCALAPPDATA%\Backup Reminder\Development.
--data-directory "%LOCALAPPDATA%\BackupReminderExample" selects a different data folder.
The default profile is independent of where the executable is stored.
Only one window is allowed per profile, including the earlier script version.

PURPOSE
Use Open to launch a backup tool, folder or webpage. Perform the backup yourself,
review its results, and check the item. Complete backups records your confirmation;
it does not verify that files were backed up or can be restored.

LICENSES
Backup Reminder uses the PolyForm Noncommercial License 1.0.0.
The unchanged official terms are included in LICENSE.txt.
Credit to DigitalGuy is appreciated but optional and adds no license conditions.
Microsoft runtime license terms and third-party notices accompany this build.
No third-party backup application or personal custom artwork is bundled.
