# Changelog

## 0.2.1 - 2026-10-18

- Show Developer Website in About, with DGX.digitalguy.me linking to the main developer website.
- Give every Browse dialog an explicit starting location. Program/file defaults to Program Files; folder and working-directory selection use Documents when no current target is available. Windows Documents/Pictures shortcuts resolve to the actual folders.
- Add a themed icon chooser for individual icons from programs, DLLs and ICO files, plus image files. With no custom icon or program, it shows Windows shell32 icons. Choices remain drafts until Save; Cancel imports nothing.
- Keep readable custom icon and sound filenames, number conflicting names, reuse identical imports and migrate older hashed names on Save. Preserve originals, manually placed files and modified assets.
- Include asset checksums in configuration backups while retaining compatibility with older backups.
- Allow empty groups to move before Save and retain their position after reopening or adding their first task. Use clearer Group and separate-task wording.
- Improve custom WAV diagnostics, distinguishing unsupported contents from missing or unreadable files. Sound Browse returns to the selected sound's folder, with Windows Media as its fallback.
- Fix circular window ownership when an unsaved-changes confirmation opens from a checklist or schedule editor.
- Update Help, portable instructions and screenshots to match the current behavior.

## 0.2.0 - 2026-10-18

- First release without the beta label.
- Show disabled Saved buttons in both editors, re-enable saving after edits, and confirm Save changes / Discard changes / Keep editing before closing unsaved drafts. Successful saves keep the editor open; failures retain edits.
- Link About directly to the Backup Reminder project page and refresh Help, documentation and screenshots.
- Import custom icons and WAV sounds into the active profile on Save, with relative paths, executable icon extraction, duplicate reuse and rollback on save failure. Original files and manually placed assets are preserved.
- Add File menu configuration ZIP backup and restore, including checklist progress and custom assets. Restore validates entries, preserves one prior configuration and leaves reminders off until explicitly enabled.
- Preserve legacy external custom-file references and import available files on settings saves. Report unavailable files during export and show icon/sound fallback hints.
- Clarify manual updates in Help and both READMEs: close, replace supplied files in the same location, reopen; personal data and the existing reminder path are retained.

## 0.2.0-beta.1 - 2026-10-17

- First public portable Windows x64 beta, under the unchanged PolyForm Noncommercial License 1.0.0 with optional DigitalGuy credit.
- Regenerated brand PNG/ICO from the unchanged vector master and improved header/About scaling for smoother clock edges.
- Added readable Windows-user/profile task names, safe legacy-name migration, collision handling and rollback.
- Grouped Notification settings with expandable custom WAV selection, Browse/Preview and Windows-sound fallback.

- Corrected Help and portable/README guidance for optional launchers, scheduled versus manual activation, configuration contents, task location/removal and restoring reminders on another PC.

- Follow Windows regional date/time formats in Reminder schedule, with themed time controls, a start-date calendar and a live schedule summary.

- Added per-item checked dates/times, retained across close/reopen and checklist edits; unchecking or completing clears them. Existing undated checkmarks remain explicitly undated.

- Added one optional Windows sound per scheduled reminder, with a saved opt-out in Reminder schedule; manual launches remain quiet.
- Added finite taskbar attention for background reminders, preserving open dialogs, edits and unfinished progress.
- Distinguished scheduled launches from manual activation, including duplicate-instance signaling, and upgraded legacy owned task arguments without moving their schedule.
- Clarified that Close saves progress and exits, while Complete backups records completion.

- Made all secondary pages explicitly owned modal dialogs, removed their separate taskbar entries, and redirected close/relaunch attempts to the open dialog.
