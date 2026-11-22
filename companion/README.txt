Noita F5 / F8 companion (Windows, .NET Framework 4)

Install the whole 3835902155 folder under Noita/mods, allow unsafe mods and
enable the local copy. Its Lua bootstrap verifies/extracts this helper and
starts it automatically when the mod initializes. Normal game exit closes it.
No Steam Launch Options or separate helper launch is needed. See START_HERE.txt.
Standalone command-line use remains available for diagnostics.
F5: ask Noita to save and quit; verify and commit a full save00 snapshot;
    restart the same executable with its original arguments.
F8: verify the full snapshot; immediately terminate Noita without saving;
    stage and replace save00 transactionally; restart the same executable.
    If copying fails after termination, the unchanged on-disk run is reopened.
After an F5/F8 restart, Continue is automatic on the supported Noita build.
The initial normal Steam launch keeps its usual main menu. Unknown game
builds fall back to manual Continue, with a message in helper.log.
Keep the Noita F5/F8 mod enabled so its gameplay-ready signal can confirm the
loaded character and immediately end automatic Continue/focus requests.
Large saves can take several seconds to copy. Wait for the helper to reopen
Noita; do not launch another game during copying. The tray shows busy status.
If Noita is reopened before the copy commits, the previous snapshot is kept
and the helper avoids opening a duplicate game instance.

Only keys pressed while the configured game is in the foreground are handled.
Holding a key does not repeat the operation. Other applications are ignored.
Run only one Noita instance. Custom save-slot, save-path, clean and Workshop
upload launch arguments are refused because this helper operates on save00.
Noita can take time to finish saving. F5 waits up to 90 seconds and never
replaces a snapshot if save-and-quit or verification fails.

Options:
  --game "C:\path\to\Noita\noita.exe"
  --save-root "C:\Users\NAME\AppData\LocalLow\Nolla_Games_Noita"
  --data-root "C:\Users\NAME\AppData\Local\NoitaF5F8"
  --quiet (compatibility option; silence is already the default)
  --manual-continue (disable automatic Continue after F5/F8 restarts)
  --self-test (isolated fixture tests; does not touch actual game saves)
  --save-now (save the sole configured running game once, then exit)
  --load-now (load the sole configured running game once, then exit)
  --launch-game "C:\path\to\Noita\noita.exe" [original game arguments]

Optional legacy Steam wrapper (not required for the local mod):
  "C:\path\to\NoitaF5F8.exe" --launch-game %command%
All arguments after --launch-game belong to the game and are preserved.
Place any helper options, such as --manual-continue, BEFORE --launch-game.
The wrapper keeps the helper alive during F5/F8 restarts and releases Steam's
Running state about two seconds after a normal game exit.

The one-shot options use the same operation pipeline as F5/F8, return 0 on
success and 1 on failure, and log their result. They never send key input.

The save root is the parent containing save00, NOT save00 itself.
Backups default to %LOCALAPPDATA%\NoitaF5F8:
  quick\save00      current full snapshot
  quick\manifest.xml  file lengths and SHA-256 checksums
  history\          previous quick snapshots
  staging\          uncommitted data if interrupted
  helper.log        local operation/error log
Before-load recovery saves are retained on the original save volume under
  <save root>\NoitaF5F8-Recovery\before-load-...
To recover manually, exit Noita and copy the desired full save00 directory
back to the active save location. Never modify saves while Noita is running.

The helper does not change game DLLs or install background services. To stop
it, use its notification-area menu > Exit helper. The local mod launches it
automatically the next time the mod initializes in a new game process.
The helper is silent: no Windows notification toasts or popup dialogs. Status
and errors are available in its tray menu/tooltip and helper.log. The --quiet
option is retained for compatibility; silence is now the default.
Automatic Continue is limited to the exact supported Noita.exe build, checks
the newly restarted process and a complete save, and never sends keyboard or
mouse input. Use --manual-continue if you prefer to continue manually.
Keep Steam Cloud from restoring an older save over a local snapshot; in case
Steam asks about a conflict, select the local save you intend to keep.

Build: powershell -File build.ps1 -Test
Only the installed .NET Framework C# compiler is needed; no package downloads.
