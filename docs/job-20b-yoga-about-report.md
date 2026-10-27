# Job 20b — Yoga kid-view flicker and the My OpControl About screen (v0.17.17)

**Status: complete and released.** v0.17.17 is on staging, production, the X1
and the Yoga.

- Branch: **`fix/job20b-yoga-about-0.17.17`** from `main` (`44552f0`).
- Feature commit: **`4241f53`** — “Job 20b: add the kid About screen and generated
  credits”. `main` fast-forwarded to the same commit; tag **v0.17.17**.
- Release workflow: **40587667497**, success; release published
  **2026-10-27 21:39:56 UTC**.
- Tests: **422/422 Linux SDK 10.0.402** and **422/422 Dell XPS**.
- Build host: **Dell XPS** (`Ziur-Dell-XPS`, `100.98.112.35`), SDK 10.0.402.
- No database migration.

## 1. The Yoga still flickered because it was still on 0.17.13

### What the Yoga was actually running (read-only, before the fix)

`EMMA-YOGALAPTOP`, Windows 10 21H2 19044.7807, 1366×768 at 100% (Intel HD
Graphics 520, driver 20.19.15.4474):

- Uninstall entry: **Parental OpControl 0.17.13**.
- `C:\ProgramData\OpControlParental\updates\update.log`: last update was
  **2026-10-27 01:30 UTC** (2026-10-26 18:30 PDT), `OpControlParental-0.17.13.msi`,
  `msiexec` exit 0, service healthy. There is no 0.17.14/0.17.15/0.17.16 entry.
- Job 19i (the in-place kid-view refresh) published **2026-10-27 15:34 UTC**, so
  the Yoga never received that fix. It was 7 hours late at the time of the report.
- Processes at the time of the check: one Client (session 0), one Helper and one
  Luna (session 1), all started at 01:30 UTC from the 0.17.13 install; a second
  Luna existed only while a My OpControl kid window was open at 20:48 UTC. No
  old pre-0.17.13 process was left behind.
- The X1 was on 0.17.15 and did have the 19i fix, which matches Ziur's report
  that only the Yoga flickered.

### Fix

The normal catalog update to **0.17.17** was requested from the dashboard
(Update Now, temporary parent; no owner or child login). The Yoga took it at
**21:49:03 → 21:49:26 UTC** (`msiexec` exit 0, service healthy, no reboot).

After the update:

- Installed **Parental OpControl 0.17.17**; Client, Helper and Luna
  `FileVersion 0.17.17.0`, `ProductVersion 0.17.17+4241f53…`.
- Service `OpControlParental` **Running / Auto**.
- One Luna in session 1; all three processes were restarted by the update, so no
  0.17.13 process remains.
- The installed Luna is the 0.17.17 build, which contains the Job 19i
  `MyOpControlWindow` in-place refresh (Messages diff, structural snapshot
  signature, live time-left update). The old build rebuilt the whole screen on
  every 2–3 s poll, which is the flicker; the slow Win10/HD 520 Yoga made it
  obvious, while the X1 already had the fix.

This cannot be visually confirmed over SSH. Version, process and update-log
evidence are conclusive that the Yoga is now on the build that contains the
19i fix. Ziur should still confirm the Home/Schedule/Rules/Activity/Messages
screens by eye on the Yoga.

### Regression test

`Job20BAboutTests.Unchanged_poll_does_not_rebuild_home_or_lose_the_live_countdown`
opens Home, changes only the runtime `UpdatedUtc` and
`DailyLimitRemainingSeconds`, refreshes, and asserts the render count does not
change while the live “time left” text does. That is the 2–3 s poll that used to
rebuild and flicker. The existing Job 19i tests (Messages diff, Enter, IME,
scroll, lock panel) still pass.

## 2. About screen in My OpControl

A new `Screen.About` in the Avalonia kid window, in night and gray themes, in
English and es-MX:

- **Sidebar:** a small **About** link at the bottom, under the device line
  (`summary.DeviceName`, or “This device” when no summary is cached).
- **Tray:** a new **About** item in the helper tray menu; it opens the same
  window at the About tab.
- **Version:** client version and build date from the assembly
  `AssemblyInformationalVersion` plus the build `AssemblyMetadata` “BuildDate”
  (the Windows build script now passes `-p:BuildDate=<yyyy-MM-dd>`), and the
  server version it is connected to. The service reads the server’s anonymous
  `/version` endpoint for the About request and caches it for 10 minutes; when
  the server is unreachable the screen says “Not available”. The new
  `about` pipe request is read-only and allowed for Luna in `LunaPipePolicy`.
- **License:** the PolyForm Noncommercial License 1.0.0 name and a plain
  summary; **Read the full license** opens the actual embedded repository
  `LICENSE` in a scrollable, selectable window (no invented terms).
- **Copyright:** “© 2026 Ziur / OpControl IT Solutions”, matching the repository
  license and the existing footer.
- **Credits:** the OpControl logo and “Created by Ziur · OpControl IT”, followed
  by the generated third-party list with versions and licenses.
- The Luna face next to the parents-supervised line is not used here.

### Generated credits

`tools/generate-third-party-credits.cs` runs as part of the Luna build
(`GenerateThirdPartyCredits` target) and reads the real
`project.assets.json`, the package `.nuspec`/license files and the bundled font
folders. It embeds `third-party-credits.json` in Luna; the About screen renders
that, so the list is not hand-written. The Luna build lists 36 components (32
NuGet packages + 4 fonts), including Avalonia 12.1.4, LiveMarkdown.Avalonia and
.Math 2.5.1, SkiaSharp, HarfBuzzSharp, Markdig, RaTeX, TextMateSharp, Fredoka,
Nunito, IBM Plex Mono and Manrope. Fonts are listed under SIL OFL 1.1.

The same tool is run again with the Client, Helper and Luna graphs for the
installer notices: **79 components** in this build. It writes
`legal/THIRD-PARTY-NOTICES.md`, and the MSI now installs:

- `Program Files\OpControlParental\legal\LICENSE`
- `Program Files\OpControlParental\legal\THIRD-PARTY-NOTICES.md` (list plus
  full license texts)
- `Program Files\OpControlParental\legal\fonts\OFL-*.txt`

The legal files are in the MSI component group `LegalComponents`. The release
workflow and the local XPS MSI both built successfully with them.

### Tests

New `Job20BAboutTests` (8 cases):

- the client version shown matches the Luna assembly version;
- the license text loads from the embedded `LICENSE` and contains the
  PolyForm name and Required Notice;
- the credits contain Avalonia, LiveMarkdown.Avalonia, LiveMarkdown.Avalonia.Math,
  Fredoka and Nunito, every package in the Luna build graph, and every component
  has a known license;
- About renders in night and gray;
- the sidebar link, tray item and en/es-MX keys are wired;
- the flicker regression above.

### Screenshots

In `/srv/opcontrol-parental/docs/job-20b-screens/` and repo
`docs/job-20b-screens/`:

- `about-night.png`, `about-gray.png` — English, both themes.
- `about-night-es.png`, `about-gray-es.png` — es-MX, both themes.
- `index.html` — all four at once.

They were captured on the XPS from the Debug preview with the generated build
date 2026-10-27, a 1040×700 window, the demo child “Ana” and device
“X1 laptop”; they read no real data and change no settings.

## 3. Release v0.17.17

- `Directory.Build.props` **0.17.17**; notes
  `.github/release-notes/v0.17.17.{en,es}.md`; annotated tag **v0.17.17**.
- Workflow **40587667497** success; published **21:39:56 UTC**.
- Web image:
  `ghcr.io/ziur-vela/parental-opcontrol@sha256:148434f9bc564afa790afc8b7ba5c637162dd0c934be69544a82163e7d0c85b2`
- Updater image:
  `ghcr.io/ziur-vela/parental-opcontrol-updater@sha256:8f52d15236bc62c777d70823b88cabf2a1dae563bd8a3037ee7bdfba8953d317`
- MSI `OpControlParental-0.17.17.msi` **143,929,344** bytes SHA-256
  `49b9f8b74adf2b4dfa294e84d64a36782dd6874f0e40fabff2db7e06dc6a2533`.
- Setup `OpControlParental-Setup-0.17.17.exe` **261,223,252** bytes SHA-256
  `2342bf6f0caebed85fc9bd7534669b54b6e5dabc1e173045cf19a025e15b9352`.
- Production catalog `data/releases` has both files with those exact hashes.

### Staging

- 0.17.16 → 0.17.17 success at **21:41:43 UTC**; updater backup
  `/opt/stacks/opcontrol-parental-staging/data/updater/backups/parental-pre-update-20261027T214102Z.sql`
  (12,103,396 bytes, “PostgreSQL database dump complete” = 1).
- `/version` **v0.17.17 · build 2026-10-27 · 4241f53**; `/health` **200**.
- `.env` pinned to the new digests; backup
  `.env.bak-job20b-20261027T214310Z`; compose backup
  `docker-compose.yml.bak-job20b-20261027T214310Z`; `docker compose config -q`
  OK.

### Production

- Manual pre-update backup:
  `/opt/stacks/opcontrol-parental/backups/parental-pre-job20b-20261027T214322Z.sql.gz`
  (**6,015,579** bytes; gzip OK; dump complete = 1), plus
  `parental-keys-pre-job20b-20261027T214322Z.tgz` (714 bytes),
  `docker-compose.yml.bak-job20b-20261027T214322Z` and
  `.env.bak-job20b-20261027T214322Z`.
- Updater backup
  `/opt/stacks/opcontrol-parental/data/updater/backups/parental-pre-update-20261027T214347Z.sql`
  (15,489,935 bytes; dump complete = 1).
- 0.17.16 → 0.17.17 success at **21:43:59 UTC**; `/version`
  **v0.17.17 · build 2026-10-27 · 4241f53**; `/health` **200**; `/luna`
  **302**; migrations **22** (no new migration).
- `.env` pinned to the new digests; backup
  `.env.bak-job20b-pin-20261027T214417Z`; `docker compose config -q` OK.
- Settings unchanged: categories `adult`, `ai`, `fakenews`, `gambling`, `social`
  all true; manual lock false; 2 Luna chats; 0 active remote sessions; no
  `job20b-%` users remain. The owner account was not touched or logged into.

## 4. Laptops

Both used the normal catalog **Update Now** through a temporary parent
(`job20b-admin@opcontrol.local`, Permissions 511, no 2FA), which was deleted
immediately after the two requests; 0 temp users remain.

| Device | Before | After | Update | Result |
|---|---|---|---|---|
| ZIUR-X1-LAPTOP | 0.17.15 | **0.17.17** | 21:48:56 → 21:49:20 UTC, `msiexec` 0 | healthy, service Running/Auto, Client/Helper/Luna 0.17.17.0, one Luna |
| EMMA-YOGALAPTOP | 0.17.13 | **0.17.17** | 21:49:03 → 21:49:26 UTC, `msiexec` 0 | healthy, service Running/Auto, Client/Helper/Luna 0.17.17.0, one Luna, no old process |

Both dashboard rows read **0.17.17 healthy** and the file versions are
`0.17.17.0` (`ProductVersion 0.17.17+4241f53…`). The Yoga’s flicker is fixed by
being on the 19i build; it could not be checked by eye remotely.

## 5. What Ziur should check by hand

1. Yoga: open **My OpControl** and leave Home, Schedule, Rules, Activity and
   Messages open for a minute each. Nothing should flash every 2–3 s.
2. Sidebar: the device name and **About** link at the bottom; the tray menu has
   **About**.
3. About in night and gray: client 0.17.17 · build 2026-10-27, server
   v0.17.17, license summary, **Read the full license** (scrolls), copyright,
   credits with the logo and the generated package list.
4. Switch the child language to es-MX and check the About labels.

## 6. Cleanup / notes

- Production temporary parent deleted; 0 `job20b-%` users; no temp family,
  device, chat or alert was created.
- XPS `C:\ocp-build\parental-20b`, `parental-20b.tgz`, `screens-20b`,
  `shots20b.ps1`, `credits-check` and the `Job20BShots` scheduled task were
  removed.
- The temporary read-only Yoga script `C:\Users\Public\yoga-check.ps1` was
  removed.
- Local transfer archives/scripts were removed.
- No owner or child login; no PIN, password, family, scaling, Windows or Luna
  setting was changed; OC’s git identity was not changed.

## Limits

- The flicker was not observed on the Yoga’s physical screen; the evidence is
  the version/update-log/process state plus the passing regression test.
- Screenshots are from the XPS Debug preview, not the installed laptops.
- The credits are generated from the actual NuGet graph and bundled fonts. The
  36-component Luna list is what the About screen shows; the installer’s
  79-component notices also cover the Client and Helper packages.
- The release MSI/setup are unsigned (unchanged from previous releases).
