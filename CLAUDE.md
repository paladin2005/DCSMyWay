# DCSMyWay

A modern, low-memory replacement for DCSTheWay (`C:\Workspace\DCSTheWay`) that imports waypoints into DCS World aircraft. It will eventually replace all of TheWay, including its DCS Lua scripts.

## General

- Code, comments, UI text, documentation and commit messages in English; responses to the user in Spanish
- Never add comments when editing code
- Always `async/await` (no `.Result` / `.Wait()`)
- Indent: 2 spaces (`.editorconfig`)
- Git commits: `type(scope): brief description` - no Co-Authored-By
  - Types: feat, fix, refactor, style, docs, chore, test, ci
  - Scope: primary area affected (window, aircraft, dcs, settings, build, etc.)
  - Description in English, lowercase, no trailing period

## Permissions

- Do not ask for permission for read operations (Read, grep, find, ls, cat)
- Only ask for permission before writing, modifying, or deleting files
- During the planning phase, explore freely without interrupting

## Before Writing Code

- Inspect the repository structure
- Locate an existing module with similar functionality
- Use that module as the implementation reference
- Follow the existing design and UX patterns of the application

## Consistency

- Reuse existing controls and views whenever possible
- Maintain consistent buttons, colors, spacing and layouts
- Prefer existing UI components over creating new ones
- Do not introduce new visual styles unless explicitly requested
- Keep dialogs inline in the view; extract to a separate control only when the view or dialog is complex enough to hurt readability

---

# C# - .NET 10 + WinUI 3

## Rules

- ALWAYS use the dedicated WinUI control for the job (`NumberBox` for numbers, `InfoBar` for status messages, `ContentDialog` for confirmations), never a generic control + property hack or hand-rolled markup. If the control you need does not exist in WinUI, ASK first
- Use theme resources (`{ThemeResource}` brushes, built-in text styles) instead of hard-coded colors and fonts
- Primary constructors for DI
- File-scoped namespaces
- `DateTimeOffset` UTC for all dates
- Single-statement `if`/`for` on one line: `if (!valid) return;`
- Flat namespaces - subfolders are for organization only, never nest the namespace (everything under `Services/` is `DCSMyWay.Services`, whatever its subfolder)

## Do NOT

- No separate projects (Core/UI/Infrastructure) - single app project with logical folders (a test project is fine)
- No interfaces for services - no interface-per-class without reason
- No MediatR/CQRS, no AutoMapper/Mapster

## Stack

- .NET 10, WinUI 3 (Windows App SDK 2.5), x64 only. Only the WinUI component packages are referenced (`Microsoft.WindowsAppSDK.WinUI` and `InteractiveExperiences`): the `Microsoft.WindowsAppSDK` metapackage also ships AI, ML, Search, Widgets and DWriteCore (onnxruntime and DirectML alone are about 40 MB)
- Unpackaged and self-contained: the published exe runs on any PC with Windows 10 2004 or later without installing runtimes
- No Visual Studio on this machine; everything builds with the `dotnet` CLI

## Commands

```bash
dotnet build src/DCSMyWay
dotnet test
dotnet publish src/DCSMyWay -c Release
```

- Run: `src/DCSMyWay/bin/Debug/net10.0-windows10.0.26100.0/win-x64/DCSMyWay.exe`
- Publish output: `dist/DCSMyWay.exe` (git-ignored; `bin/Release` only holds the intermediate build), a single compressed exe (about 37 MB) that is the whole distribution: no installer, the app installs its DCS scripts itself
- Validate changes with `dotnet build`; ask the user to confirm anything visual

## Gotchas

- Keep `EnableMsixTooling` on even though the app is unpackaged: without it `dotnet publish` leaves out the app's `.pri`/gh
- Release publishes as a single file (`PublishSingleFile` with `IncludeAllContentForSelfExtract`, supported for unpackaged self-contained WinUI since Windows App SDK 1.5): the exe extracts everything to `%TEMP%\.net\DCSMyWay\<hash>` on first launch and `AppContext.BaseDirectory` points there. Windows App SDK generates the exe manifest with DLL redirection only when `PublishSingleFile` is set, and MSBuild reuses a manifest left in `obj/Release` by a build without it, which crashes at startup with `ClassFactory cannot supply requested class`: delete `obj/Release` after changing these properties
- Release is trimmed, so everything handed to WinUI must be trim safe: `AllowUnsafeBlocks` lets CsWinRT generate the vtables of generic collections like `ObservableCollection<WaypointItem>` (without it the trimmed exe crashes setting `ItemsSource`), classes passed to WinUI are `partial`, and types read by reflection bindings (`DisplayMemberPath`) carry `[GeneratedBindableCustomProperty]`. `-p:CsWinRTAotWarningLevel=2` lists the problems at build time
- WinUI copies its `.mui` translations for about 90 languages; the `RemoveNonEnglishMuiFiles` target keeps only `en-us`
- Don't use `RuntimeInfo.AsString`: in self-contained mode it throws trying to load `Microsoft.WindowsAppRuntime.Insights.Resource.dll`
- Resolve file paths from `AppContext.BaseDirectory`, never from the working directory
- The theme setting (System/Light/Dark) is applied per window by `WindowInterop.SetTheme`: `RequestedTheme` on the window's root element plus `AppWindow.TitleBar.PreferredTheme` for the caption buttons, which do not follow the XAML theme
- Native AOT needs the MSVC linker (Visual Studio Build Tools, "Desktop development with C++"), which is not installed
- Startup crashes show up as `0xC000027B`; the real exception is written to `%TEMP%\DCSMyWay-crash.log`
- Tests run on Microsoft.Testing.Platform (xUnit v3), enabled in `global.json`; the old VSTest packages are not needed
- The app build is self-contained, so the test project sets `ValidateExecutableReferencesMatchSelfContained=false` to reference it; tests must only use plain logic, never WinUI types

## Aircraft

- Each aircraft lives in `Aircraft/` and is ported from TheWay's `src/moduleCommands/*.js`
- `tests/DCSMyWay.Tests/Fixtures/` holds JSON generated by running TheWay's own JS (coordinate conversion + button sequences); a port is correct when it produces exactly the same button presses (for aircraft that read their displays, the same typing presses)
- `SupportedAircraft` describes each aircraft for the UI (module names, steerpoint name and label, valid range, hint when the active steerpoint is unknown) and creates its `TransferSequence` of `CockpitStep`s
- F-16 transfers press RTN three times (back to the CNI page from any submenu), open STPT with `4`, type the start steerpoint + ENTR (the STPT page cursor starts on the number field: number, AUTO/MAN, LAT, LNG, ELEV), then enter each waypoint with ▲ between them. Only waypoints without a checkmark are sent, starting right after the steerpoint of the previous entered waypoint; the first transfer starts at the active steerpoint read from the DED
- The Transfer button is a `SplitButton`: a click uses the start above (the active steerpoint for a first transfer), while its menu forces "Overwrite from" the active steerpoint or "Add after" it for that transfer, sending the waypoints without a checkmark (all of them if every one is checked) without the repeat dialog. It gets the accent look through the SplitButton lightweight styling resources pointed at the `AccentButton*` brushes; "Cancel transfer" is a plain button stacked in the same grid cell so the width never jumps
- F/A-18C transfers never press blind: every AMPCD step checks the page it should reach (`list_indication(4)`) and the UFC its prompts (`list_indication(6)`). They press MENU until the SUPT menu, HSI or DATA shows (MENU alternates TAC and SUPT), open HSI > DATA, set LATLN to decimal minutes on the A/C tab and box PRECISE on the WYPT tab, then select each waypoint by reading `WYPT_Number` and type it on the UFC like TheWay (POSN, lat, lon, ELEV, FEET). After each waypoint they read latitude, longitude and elevation back from the AMPCD (`N 42°14.6479'`, `2412FT`, ±1 in the last digit); the checkmark comes from the elevation readback. They end selecting the first new waypoint with the AMPCD back on the HSI. Waypoint 0 (usually the departure field) is never used; waypoints go up to 59. The active waypoint comes from `WYPT_Page_Number` on the AMPCD, or a DDI (indicators 2 and 3) showing the HSI
- A-10C and A-10C II share the same CDU (device 9, `list_indication(3)`), so one port serves both. Transfers overwrite mission waypoints 1-50 like the F-16 (0 is the initial position; 51-2050 are navigation points that cannot be edited), starting at the active steerpoint from the CDU top line (`CurrSteerPointNumber`). They set the AAP PAGE knob to OTHER (function keys only work there), CLR the scratchpad, open WP > WAYPOINT, then per waypoint type its number on LSK 3L (`CDU INPUT ERR` when it does not exist, in which case LSK 7R copies to the next free mission waypoint and must land on that number), then latitude (7L), longitude (9L), elevation (5L) and name (3R), reading each back (`N41°36.618`, `E041°35.976`, `1234`). Elevation goes after the coordinates because the CDU recomputes it from the terrain (CR) when the position changes, and the name goes last because an identifier already used by another waypoint makes the CDU jump to that waypoint; the final check that the number did not change catches it. Afterwards each waypoint is added to the active flight plan (FPM, the row marked `*`, FPBUILD) unless its number is already there: back to page 1 with PG UP, type the number, then PG DN until the number shows (CLR) or the "(NEXT)" slot shows (its row's LSK), and check the name appears. PG DN is `3063` with value -1, PG UP `3062` with 1
- AH-64D: both seats have their own MPDs and KU, but the point database and routes are shared, and `list_indication` only renders the displays of the seat the player is in, so transfers use the seat's right MPD and KU (pilot: devices 43/29, indicators 9/16; CPG: 45/30, 13/15). The export script reads the seat from the `SEAT` cockpit parameter (`list_cockpit_params`, 0 pilot, 1 CPG) and reports it as `seat`. Points W01-W50 share their slots with hazards (H); waypoint 0 is never used. Per point the transfer enters POINT > ADD, picks the WP type (the ATK phase defaults to targets), and if the ADD window's next free index is the target it adds the point (IDENT> and ENTER keep the WP identifier); otherwise it leaves ADD, selects the point with POINT> and uses EDIT, so `unless` checks on the KU prompt (`Standby_text`: `IDENT:`, `FREE:`, `UTM LAT/LONG:`, `ALTITUDE:`, `POINT:`) skip the steps of the other path. It clears the free text with CLR (BKS on an empty entry cancels the KU prompt, CLR never does) and types it (first 3 letters or digits of the name), CLR + MGRS (`38TKM56448074`), CLR + altitude, and reads MGRS, altitude and text back from the review window. Coordinates go in as MGRS because the Apache stores points in 8-digit MGRS (lat/long input gets rounded twice); `Mgrs` is standard WGS84 UTM rounded to 10 m, which matches DCS. Right after each waypoint, before its checkmark, it is added to the end of the current route on TSD > RTE (ADD, POINT>, then the R button next to `END`, scrolling with R1) unless a route row already shows it (`..05`), so a later failure never leaves an entered waypoint out of the route; the TSD FAB then leads back to POINT for the next one and to the TSD at the end. The active waypoint is `NextWaypointStatusWindow_text_1` (`W02`) on the seat's MPDs
- Targets: aircraft with `SupportedAircraft.SupportsTargets` (AH-64D, Ka-50) show a red crosshair `ToggleButton` per waypoint (subtle look through ToggleButton lightweight styling resources in the list). A waypoint with `IsTarget` is added as a new Target/Threat point (POINT > ADD, TG type on L6, IDENT> and ENTER keep the TG identifier, next free T##), never edited and never added to the route; the review index must match `^T%d%d$`. Targets take no steerpoint number: `TransferSequence.Steerpoints` holds each waypoint's number or null for targets, `TransferHistory` stores it, and new waypoints continue after the last entered numbered waypoint. Toggling changes the `Waypoint` record, so the point is sent again. Aircraft with numbered target slots get `TransferStart.Target`, the position of the first pending target among the targets of the list, so a later transfer continues after the last target sent; `SupportedAircraft.LastTarget` caps them
- Apache threats (`SupportedAircraft.SupportsThreats`): a target can carry a `Waypoint.Threat` code from `ApacheThreats` (the identifiers and engagement ranges come from the module's `Computers/DTU/DTC.lua` threat rings, e.g. `8` SA-8 10.3 km, `ZU` ZSU-23-4 2.5 km); null is a generic `TG` target. The edit dialog (pencil) offers the type for targets; the transfer types the code at `IDENT:` and reads it back from the review window, so the TSD draws the threat rings. The F10 route shows the type in the label and the engagement ring as a dashed red circle
- Ka-50 and Ka-50 III share the PVI-800 (device 20, `list_indication(5)`), so one port serves both; the ABRIS keeps its own routes and is never touched. The PVI stores waypoints 1-6 and NAV TGT targets (1-9 for now: whether `0` selects target 10 is untested) and no elevation. Transfers set the mode switch (`3026`) to OPER (0.3) and then ENT (0.2), which turns the WPT and NAV TGT lights off; WPT then shows the number of stored waypoints in `txt_OIT_PPM`. Per point: WPT or NAV TGT (pressed twice when already lit, since it toggles), the number (`txt_OIT_PPM` or `txt_OIT_NOT`), then the sign key (`0` N/E, `1` S/W) and digits of the latitude (`txt_VIT` ` 43190`) and longitude (`txt_NIT` `040144`), each checked as typed, and ENTER. The mode button off and on plus the number again show what was stored, read back with ±1 in the last digit (`txt_VIT_sign`/`txt_NIT_sign` show `-`); a lost ENTER shows the old coordinates there, and this readback gives the checkmark. They end with CANCEL and OPER, which brings back the waypoint that was active. The active waypoint is `txt_OIT_PPM` while `txt_VIT` shows (OPER with WPT lit); in ENT the same element holds the waypoint count
- Mirage F1EE is the only F1 with an INS (the CE and BE have none); its PCN (device 1) stores waypoints 1-9 and no elevation. The PCN display is not in `list_indication`, so transfers check cockpit arguments: each digit is an argument at 0.1 per digit (latitude window 1342-1346, longitude window 1347-1352), hemisphere lights N 1335, S 1336, E 1359, W 1360, the INSER light 1274 (on while an entry waits for INSER) and the waypoints wheel 667 ((n - 1) × 0.111, cycling 1-9). The parameters selector (3690, argument 665) is POS at 0 and PP at 0.2 (TheWay's comments swap them), and data entry only works in POS. Transfers turn it to POS and press CLR when the INSER light is on, because an entry left pending survives wheel turns and INSER would store it in the next waypoint. Per waypoint they turn the wheel (3694 with +0.111) until argument 667 shows it, press the hemisphere key (N 2, S 8, E 6, W 4), type and check the digits, and press INSER, which must turn its light off with the digits still shown; the checkmark comes after the longitude INSER. They end on PP and never touch the modes selector (NAV is 0.125). The active waypoint is the destination (VERS on the PCN, argument 1361 × 10)
- Waypoint names follow the A-10 CDU rules (`Waypoint.IsValidName`: up to 12 letters, digits or periods, starting with a letter, unique in the list); the app renames them in a dialog. Renaming changes the `Waypoint` record, so it loses its checkmark and is sent again

## DCS scripts

- `dcs/` is copied next to the exe as `DCS\` and mirrors a DCS write folder. On every start the app installs it (`DcsScripts`) into each write folder `DcsFolders` finds: `Saved Games` (the known folder, which can be moved) plus `DCS` or `DCS.<dcs_variant.txt>` for every install under `HKCU\Software\Eagle Dynamics` (`Path`), falling back to every `Saved Games\DCS` or `DCS.*` folder with a `Config` folder (`DCS_F4E` and `DCS_OH58D` are module data, not write folders). It copies the files that are missing or differ, appends the `Export.lua` line unless a line not commented out already loads `DCSMyWayExport.lua`, and appends a block to `Config\autoexec.cfg` that adds `"userhooks"` and `"mission"` to the `net` lists unless they are already listed (DCS allows none by default). The InfoBar says what changed and whether DCS (hook, GUI files, autoexec) or only the mission (export script) must restart. Settings > "Remove from DCS" deletes the shipped files, the empty folders they leave and only the lines the app added, then closes the app; files are edited as Latin-1 so every other byte stays the same
- `dcs/Scripts/` mirrors `Saved Games\DCS\Scripts\` and holds two scripts:
  - `Hooks/DCSMyWay-hook.lua` loads `DCSMyWay/DCSMyWay.lua` into the DCS GUI Lua state at DCS startup (restart DCS after changing it). It owns everything visual and camera related: crosshair overlay, accept button, status text, camera position and map drawing
  - `DCSMyWay/DCSMyWayExport.lua` runs in the export Lua state, loaded by one line appended to the user's `Export.lua` (reloaded on every mission). It owns the cockpit: reads the active steerpoint (F-16 DED `list_indication(6)`, Hornet HSI, A-10 CDU, Apache TSD, Ka-50 PVI-800, Mirage F1EE PCN) and the seat of two-seat aircraft and runs transfers with `GetDevice(...):performClickableAction`. It chains the `LuaExport*` functions of the scripts loaded before it
  - The split exists because multiplayer clients cannot reach the export state from a hook (`net.dostring_in` returns nothing there), while the export state works everywhere, like TheWay and DCS-BIOS
- Protocol: both scripts send JSON over UDP 42080. The hook sends `camera` at 10 Hz and `mark` when the in-game accept button is clicked, both with the player's `aircraftId` from `Export.LoGetPlayerPlaneId`, which changes on every respawn. The export script sends `cockpit` at 10 Hz with the module, active `steerpoint`, `seat` (two-seat aircraft), `busy`, and the `transferId`, `done` steps and `failed` reason of the last transfer. The app sends one JSON line per TCP connection: `crosshair`, `status` and `route` to the hook on 42081; `waypoints` (with an `id`) and `cancel` to the export script on 42082. TheWay keeps 42069/42070, so both can run side by side
- A transfer is a list of steps, one per frame. A step may press a button (held for `delay` ms) and may read the displays with `list_indication`: `unless` skips the whole step when a check holds, `until` skips the press when a check already holds and presses again up to `tries` times, `wait` always presses and then must see its checks within `timeout`, `select` presses `device`/`down` until the number in an element reaches `target`, and `options` presses the button of the first option whose `when` check holds (none for an option without a button), pressing `otherwise` (a page turn) up to `tries` times while none holds. A check holds when an element whose name matches the Lua pattern `element` exists, with one of `values` or matching `pattern` if given; a check with `arguments` (`ArgumentCheck`, set through the `UnlessArguments`/`UntilArguments`/`WaitArguments` step properties) holds when every cockpit argument (`GetDevice(0):get_argument_value`) is within 0.03 of its entry in `levels`. A step that times out stops the transfer and reports its `failure` text, which the app shows; the export script logs what the display showed
- A waypoint gets its checkmark as soon as `done` passes the step where that waypoint is confirmed (`TransferSequence.WaypointEnteredAfter`: the elevation ENTR on the F-16, the elevation readback on the Hornet, the name check on the A-10, the text readback on the Apache), so a cancelled or failed transfer keeps the marks of the waypoints that made it in
- The route is drawn on the F10 map (single player or when hosting; never as a multiplayer client, where mission scripts run on the server) through `net.dostring_in('mission', 'a_do_script(...)')`, which needs `"userhooks"` in `net.allow_unsafe_api` and `"mission"` in `net.allow_dostring_in` (`Saved Games\DCS\Config\autoexec.cfg`). Route points connect with cyan lines and circles; points flagged `target` are left out of the line and drawn in red with a cross. Map drawings use mark ids from 970001 and never reuse one: DCS applies `removeMark` after the script finishes, so a reused id deletes the new drawing. `a_do_script` return values are unreliable (DCS 2.9.30 returns nothing), so drawing errors are logged from the mission with `env.error`
- Both scripts run on Lua 5.1 (`setfenv`, no `goto`); the hook API is documented in the DCS install's `API/Sim_ControlAPI.md`, export functions are called as `Export.Lo*`, and LuaSocket UDP receives are capped at 8192 bytes
- The crosshair is centered on the monitor setup's `UIMainView`: WinWing setups make the DCS window taller than the main view
- Never reformat existing lines in the user's `Export.lua`: SimShaker, WinWing and similar tools look for their exact line and append a duplicate when they don't find it. Only append or remove the DCSMyWay line, byte for byte, keeping its CRLF line endings, and back the file up first
