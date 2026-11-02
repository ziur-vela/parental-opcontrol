# Job 20g — lock timer fixes (v0.17.22)

**Status: complete and released.** v0.17.22 is on staging, production and the
**X1 only**. The Yoga was not targeted, tested or deployed and remains at
0.17.19. No Windows setting, PIN, account, display scaling or family setting
was changed.

- Commit: **`d89ee6f`** — *Job 20g: fix unlock override and polish the lock
  timer* (on `main`, from v0.17.21 `abc48ae`).
- Annotated tag **`v0.17.22`** is on `d89ee6f`.
- Release workflow **41477949309** — `test`, `windows`, `images` and `release`
  all succeeded.
- Tests: **557/557 Linux SDK 10.0.402** and **557/557 Dell XPS**
  (`Ziur-Dell-XPS`, SDK 10.0.402).
- No database migration. Production and staging stay at **23 migrations**.

## 1. Unlock override root cause and fix

### Root cause

The dashboard **Unlock** action, the child-PIN *Unlock until next lock cycle*
action and the client's local override all called
`LockRules.NextLockCycleUtc` and stored the result in `UnlockUntilUtc`.
With no active schedule, `NextLockCycleUtc` returned the **next schedule
start** (8:30 PM). The override was therefore valid only *until* 8:30 PM. At
8:30 PM the override expired and the schedule locked the PC. The pill had
shown the new end time, but it was the lock *start*, not the end of the lock
window.

### Fix

`LockRules.NextLockCycle` now returns the end of the upcoming schedule window,
not its start (`ScheduleWindowEndUtc`). It still considers, in order: the
active schedule end, the upcoming schedule window end, an earlier daily-limit
day end, then local midnight. The client (`LockRuntime.ApplyLocalOverride`),
dashboard (`LockService.ApplyAsync`) and PIN path
(`LockService.ApplyDeviceOverrideAsync`) all use the same rule, so the override
no longer expires at the schedule start. `UnlockScheduleName` also recognises
the upcoming window end.

For Ziur's case (unlock at 8:25 PM, schedule 8:30 PM–late night, daily day end
12:00 AM) the override now runs to **12:00 AM**: the PC does not lock at
8:30 PM, and the normal schedule/daily-limit state applies again at the window
end.

### Other time controls checked

| Control | Result |
|---|---|
| **Unlock** (dashboard and child PIN) | Fixed: skips the upcoming window until its end. |
| **Unlock while already locked** | Uses the active schedule window end (unchanged by design). |
| **Unlock with nothing scheduled** | Uses the daily-limit day end or local midnight (unchanged). |
| **Add time / bonus** | Already wins over an upcoming schedule for the bonus duration, then relocks by the manual bonus state; not affected by the start/end bug. |
| **Lock now** | Sets the manual lock and clears any override immediately; not affected. |
| **Lift lock** | Is the same `Unlock` path, so it gets the fix. |

Tests cover: unlock 5 minutes before a schedule → no lock at the schedule
start, lock at the daily window end; unlock while locked; unlock with nothing
scheduled; override expiry; next-day schedule; and the contract-level
`NextLockCycle` boundaries.

## 2. Old warning popups removed

- Removed the old on-screen `NotificationForm` warning for the 5- and 1-minute
  **lock** heads-up in `CheckLockWarnings` (`ShowNotification` call deleted).
- Removed the 5- and 1-minute **daily screen-time** popups too; the existing
  **15-minute** daily-limit warning is kept.
- Kept voice warnings, `lock-warning-*` / `daily-warning-*` log entries, and
  the server notification path (`CheckNotification`) that the parent side uses.
- The pill is now the warning: under 5 minutes it slides out even when tucked
  and stays out until the lock. When the child hides the timer, the pill still
  appears from the 5-minute mark until the lock.
- Timer tab note changed to: *“Even with the timer hidden, it will pop up
  5 minutes before the computer locks.”* / *“Aunque ocultes el temporizador,
  aparecerá 5 minutos antes de que se bloquee la computadora.”*

## 3. Time format

`LockTimerFormat` now uses `h`/`m`/`s` units and keeps the colon only for the
last minute, which always shows seconds (`0:42s`).

| Time left | Seconds hidden | Seconds shown |
|---|---|---|
| ≥ 1 day (days setting) | `1d 8h 45m` / `1 d 8 h 45 min` | `1d 8h 45m 52s` / `1 d 8 h 45 min 52 s` |
| ≥ 1 day (hours setting) | `32h 45m` / `32 h 45 min` | `32h 45m 52s` / `32 h 45 min 52 s` |
| 1 h to < 1 day | `3h 21m` / `3 h 21 min` | `3h 21m 05s` / `3 h 21 min 05 s` |
| 1 min to < 1 h | `21m` / `21 min` | `21m 05s` / `21 min 05 s` |
| Last minute | `0:42s` | `0:42s` |

The Timer tab's “More than a day left” hours button is now `32h 45m` /
`32 h 45 min` (was `32:45`). Formatter tests cover exactly 1 day, 23h 59m,
1 h, 59m, 1 min, 59 s and 0 in both languages with seconds hidden/shown, plus
the hours setting and last-minute forcing.

## 4. Status message, then morph into the countdown

`LockTimerStatus.Detect` compares the previous and current runtime state and
the pill now shows a status message for about **4 seconds** before morphing
into the normal countdown:

- Unlocked: *Unlocked until 12:00 AM* / *Desbloqueado hasta las 12:00 a. m.*
- Time added: *+30 min added* / *+30 min agregados*
- Schedule/lock time changed: *Now locks at 9:00 PM* / *Ahora se bloquea a las 9:00 p. m.*
- Lock lifted with nothing scheduled: *No lock scheduled* / *Sin bloqueo programado*,
  then the pill hides.

The text cross-fades/slides while the pill width animates. If the pill is
tucked it slides out for the message and tucks again after the usual delay. If
the child hid the timer, only changes that make the lock **earlier** are shown.
Hovering shows the current status as the tooltip, e.g. *“Unlocked until
12:00 AM. Click to open My OpControl”*. Luna stays calm during the message
except under 5 minutes, where the warning face wins. `--timer-preview message`
was added.

## 5. Grimace clipping

The grimace's glance travel was reduced (from 16 to 6 units) and the eyes and
mouth are now clipped to the face circle for both the grimace and the
last-minute face. The features stay inside the moon for the whole glance at
100%, 125% and 150% output scaling.

## 6. Release v0.17.22

### CI assets

- Web image: `ghcr.io/ziur-vela/parental-opcontrol@sha256:4fb450e862e6e820e66440f6b8deb988339cf3e684f2c6b52bf7ade3037ff1df`
- Updater image: `ghcr.io/ziur-vela/parental-opcontrol-updater@sha256:15d070aeb87fe46c389ccd616fd9598360b14830b6a46080941d19ec07d461ed`
- MSI `OpControlParental-0.17.22.msi` **144,003,072** B SHA-256
  `3d1803baf2b888d127630ad7e118e3eb822dabc5f9c28a4cd55da74d755fb14c`
- Setup `OpControlParental-Setup-0.17.22.exe` **261,301,076** B SHA-256
  `310bf0445aa5cd42092d2801089b12608db5a194edbdfedb51d6c2fa45a77e4f`

XPS local validation build (not the published catalogue): MSI **144,048,128** B
`CCF155D81C6C883FF4585F33CD7B1A6EFFE39C2CA5CBEA20627EC8ABB442D7AB`;
Setup **261,346,132** B
`ACBA6BF0C1C13E3D93FB31C919B925E6B528A41C0CA9DD0EA3317A51B7DF877E`.

### Staging

- Manual backup
  `/opt/stacks/opcontrol-parental-staging/backups/parental-staging-pre-job20g-20261102T160539Z.sql.gz`
  (5,707,482 B; gzip OK; dump complete); `.env` and compose backups with the
  same timestamp.
- Updater backup `/data/backups/parental-pre-update-20261102T161413Z.sql`.
- 0.17.21 → 0.17.22 success at 16:14:52Z. `/version`
  **v0.17.22 · build 2026-11-02 · d89ee6f**; `/health` **200**; migrations 23;
  staging data unchanged (2 families, 2 children, 11 devices, 2 users,
  0 `job20g%` users).
- `.env` pinned to the final digests (`.env.bak-job20g-pin-20261102T162534Z`);
  `docker compose config -q` OK.

### Production

- Manual backup
  `/opt/stacks/opcontrol-parental/backups/parental-pre-job20g-20261102T160520Z.sql.gz`
  (**6,729,084** B; gzip OK; dump complete).
- Keys `/opt/stacks/opcontrol-parental/backups/parental-keys-pre-job20g-20261102T160520Z.tgz`
  (714 B); `.env` and compose backups with the same timestamp.
- Updater backup `/data/backups/parental-pre-update-20261102T161953Z.sql`.
- 0.17.21 → 0.17.22 success. `/version`
  **v0.17.22 · build 2026-11-02 · d89ee6f**; `/health` **200**; migrations 23.
- Post-release state: 1 family, 1 child, 2 devices, 1 user (owner only), 0
  `job20g%` users.
- `.env` pinned to the final digests (`.env.bak-job20g-pin-20261102T162534Z`);
  `docker compose config -q` OK.
- Catalogue files inside `parental-web` and on the host match the release
  manifest (MSI `3d1803ba…`, Setup `310bf044…`).

### X1 (only)

- X1 was on 0.17.21. The CI MSI was downloaded to `C:\Temp\Job20G`, SHA-256
  checked against `3d1803ba…` and installed with `msiexec /i … /qn /norestart`;
  exit **0**.
- Client, Helper and Luna are **0.17.22.0**; service **Running / Automatic**.
  New client PID 5080; helper PID 18660 in session 1 as Emma; Luna PID 19856.
- Helper log: `2026-11-02T16:24:18Z helper-started session=1 user=Emma`, then
  `countdown-shown`.
- The server reports `ZIUR-X1-LAPTOP` **0.17.22**, LastSeen
  **2026-11-02T16:24:55Z**. The Yoga remains 0.17.19 and was not touched.
- `C:\Temp\Job20G` removed. No owner/child login, PIN change, Windows setting,
  display scaling or family change.

## 7. Screenshots

`/srv/opcontrol-parental/docs/job-20g-screens/` and repo
`docs/job-20g-screens/`:

- `pill-format-*` — every format boundary in English and es-MX, seconds hidden
  and shown, days and hours settings;
- `pill-message-*` and `pill-message-morph.gif` — message plus the 4 s
  message-to-countdown morph;
- `pill-grimace-{100,125,150}.png` and `pill-grimace-glance.gif` — grimace at
  the three output scales;
- `pill-show/tuck/warn/last-night.png` — normal pill states;
- `index.html` — gallery.

The images are painted by the real `PillArt` code in a 900×250 scene from the
Dell XPS build host.

## 8. Cleanup and limits

- XPS: `C:\ocp-build\p20g` removed (short-path build). No build tasks left.
- X1: `C:\Temp\Job20G` removed; runtime data, device credential, Luna key,
  config and PIN untouched.
- No temporary production parent/user was created.
- No Yoga action; no owner or child login; no Windows/display/PIN/family
  change; no remote session left open.
- The X1's interactive screen was not captured (that would require logging in
  as Emma); the child-side proof is the helper log, the installed file
  versions, the service state and the server's reported client version.
- The 100%/125%/150% grimace images are vector paint rendered at those output
  scales; the X1's own 150% screen was not photographed.
