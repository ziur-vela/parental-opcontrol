# Job 20h — one lock engine (v0.17.23)

**Status: in progress.** Branch `fix/job20h-lock-engine-0.17.23` from `main`
(v0.17.22 `eb6c6c8`; code tag `v0.17.22` on `d89ee6f`). Target device: X1 only.
No Windows settings, PIN, account or family setting will be changed.

## Step 0 — audit of the v0.17.22 behaviour (written before code changes)

### Where lock decisions are computed today

| Place | What it does |
|---|---|
| `Contracts/LockRules.Evaluate` | Server and client lock state: bonus first, then unlock, then manual lock, then active schedule, then daily limit. Returns `IsLocked`, reason, schedule name and an end time. |
| `Contracts/LockRules.NextLockUtc` | Client pill countdown. Bonus end → unlock end → next **schedule start**. It has no daily-limit input, which is the v0.17.22 bug (8:30 PM shown while 5 h of daily limit would lock first). |
| `Contracts/LockRules.NextLockCycle` | Dashboard/child/PIN “Unlock until next lock cycle”: active schedule end → upcoming schedule window end → daily-limit day end → local midnight. |
| `Contracts/LockRules.ActiveSchedule/CurrentScheduleEnd/NextScheduleStart` | Schedule window maths (including crossing midnight), used by the server, dashboard, client and policy checks. |
| `Server/Services/LockService.BuildDto` | Dashboard lock DTO from `Evaluate`; no next-lock or limit-left field. |
| `Server/Services/LockService.ApplyAsync` / `ApplyDeviceOverrideAsync` | Writes bonus/unlock/manual state; unlock uses `NextLockCycle`; bonus uses `now + minutes`, not the current bonus end. |
| `Server/Services/UsageService.BuildDailyLimitAsync` | Device-time-zone day bounds and used seconds from active usage intervals; no override exclusion. |
| `Server/Services/UsageLimitService` | 20 s background loop; pushes `DeviceStateDto`, raises the daily-limit alert/event. |
| `Server/Services/ScheduleTransitionService` | 30 s loop; records `schedule-start` / `schedule-end` events only (it does not decide locking). |
| `Server/Components/Pages/Home.razor` | Uses `Evaluate` for the state text, but separately computes `NextScheduleTime` with `ActiveSchedule` / `NextScheduleStart` and `RemainingText` from its own daily build. So the dashboard’s “next” ignores the daily limit. |
| `Server/Components/Pages/ScreenTime.razor` | Uses `UsageService` for used/remaining and `LockService` only for actions; no shared lock-time output. |
| `Client.Windows/LockRuntime` | Evaluates `Evaluate` + `NextLockUtc`; adjusts used seconds with local active seconds; writes `RuntimeLockStateDto` to `state.json` for the helper. |
| `Client.Windows.Helper/HelperContext` | Chooses the pill branch: bonus end, unlock end, next schedule start, otherwise daily-limit remaining. This is where “10h 54m schedule” was shown instead of the 5 h limit. |
| `Client.Windows.Helper/UsageSampler` | Counts active use only when the state is not locked, the session is unlocked and idle is below the threshold (default 180 s, clamped 30–3600). It queues active and inactive minute buckets; idle/locked time is inactive or not sampled. |
| `Client.Windows/DataSyncService` | Flushes queued usage to the server and only then adds it to local `LockRuntime`; offline queued usage is not in the local count until it is sent. |
| `Luna/MyOpControlWindow` | Kid Home “Time left today” uses `DailyLimitRemainingSeconds` (fallback summary used); “Next lock” uses bonus/unlock/`NextLockUtc` and has no daily-limit time. |

### Daily-limit counting

- Server: `UsageService.UsedTodaySecondsAsync` sums `UsageInterval.DurationSeconds` for intervals with `Active = true` whose `StartUtc` is inside the local day (device time zone), excluding always-allowed apps. Midnight bounds come from `LocalDayBounds(tz, now)`.
- Client: `UsageSampler` records 5-second samples into minute buckets. Active means: not locked, input desktop available (not the lock screen), and idle below the device threshold (default 180 s, range 30–3600). Idle buckets are stored with `Active = false` and are not counted. Locked time is not sampled.
- Server and client both count bonus/unlock usage today. The rules say it must not count.
- Day reset: server rebuilds from the local day bounds; client `LockRuntime.AdjustedStateLocked` shifts `DayStartUtc`/`DayEndUtc` forward when `now >= DayEndUtc` and resets used to 0, then adds local active seconds. The client local reset only advances one day at a time.

### Schedule windows

- `LockRules.ActiveSchedule` handles normal and overnight windows (`StartMinutes > EndMinutes`) with the previous day’s day mask for the after-midnight part.
- `CurrentScheduleEnd` returns the end of the active window; `NextScheduleStart` checks up to 8 days.
- The schedule transition loop only writes history events. It does not change lock state; the client/server evaluator decides.

### Bonus / Unlock / Lock now

- Bonus is stored as `BonusUntilUtc` (+ `BonusMinutes`) and, in the v0.17.22 server/client, also sets `ManualLock = true`. Evaluate gives bonus precedence while active; after it ends the manual lock relocks even if no schedule/limit applies. Adding a new bonus uses `now + minutes` rather than extending the current end.
- Unlock is stored as `UnlockUntilUtc`; server/client compute the end with `NextLockCycle`. v0.17.22 skips the upcoming schedule window.
- Neither bonus nor unlock stores a start time. `ManualLock` is the only “lock now” state; lock now clears bonus/unlock.
- Absolute UTC end times are stored server-side and sent to the client; ends are used offline. Starts are not stored.

### Offline

- The client keeps the last `DeviceStateDto` in `device-state.json` and the last `RuntimeLockStateDto` in `state.json`; `LockRuntime` evaluates offline with local usage.
- Overrides are applied locally first and queued in `overrides.queue`; usage/visits are queued and synced by `DataSyncService`.
- The server is the record for usage intervals. There is no explicit “larger used-minutes value wins” merge today; a server state that is behind can overwrite the client’s local used seconds until the queue syncs.
- Queued usage is added to local usage only after a successful POST, so offline daily-limit enforcement lags behind actual use.

### What the pill / kid view show today

- Pill: “Lock in `<time>`”. It chooses bonus end → unlock end → next schedule start → daily-limit remaining. The daily limit is only shown when there is no upcoming schedule, which is the reported bug. Tooltip is the normal countdown; no reason text.
- Kid Home “Time left today”: `state.DailyLimitRemainingSeconds` when present, else limit − summary used. “Next lock”: bonus active → bonus end; unlock active → unlock end; else `state.NextLockUtc` (schedule start); else “no locks”. It does not show a daily-limit lock.
- Dashboard Home: status from `Evaluate`; “Next schedule” from schedule helpers only; remaining time from its own daily build. It does not show the actual next lock when the daily limit comes first.
- Pill warnings (5/1 min) use the same branch as the pill, so they also miss the daily-limit case.

### Step 0 conclusion

There are three separate “next lock” calculations (pill/`NextLockUtc`, dashboard schedule helpers, kid `NextLockUtc`), plus two used-minute paths (server intervals and client local seconds). They do not share override starts, override usage exclusion, or daily-limit priority. This job replaces them with one pure engine and makes all surfaces render its output.

## What was removed and unified

- **One engine:** `Contracts/LockEngine.cs` is the only lock decision. It is a
  pure function of schedules, daily limit, absolute override start/end, manual
  lock, now and the device time zone. It returns `IsLocked`, reason, next-lock
  wall clock, time until lock, limit remaining, next schedule lock, active
  override and `HasPill`.
- **Server:** `LockService.BuildDto` now renders engine output. `GetStateAsync`
  (dashboard/API) and `EnrollmentService` use the same result. The duplicate
  `Evaluate`/`NextLockUtc` calculations are no longer used by server code.
- **Client:** `LockRuntime` calls `LockEngine.Evaluate` with the saved state and
  local active usage. `NextLockUtc`/`Evaluate` calculations were removed from
  the client runtime. The client stores absolute unlock start/end and bonus
  start/end locally and evaluates them offline.
- **Dashboard:** `Home.razor` reads `LimitRemainingSeconds`,
  `NextScheduleLockUtc` and `NextScheduleName` from the engine result instead of
  re-running `ActiveSchedule`/`NextScheduleStart`. `ScreenTime.razor` uses the
  same lock state for “time left today”.
- **Kid view:** Home “Time left today” uses `LimitRemainingSeconds`; “Next lock”
  uses `TimeUntilLockSeconds`/`TimeUntilLockKind` and shows the daily-limit
  countdown when that is the next lock.
- **Pill:** `HelperContext` uses `HasPill` + `TimeUntilLockSeconds` only, always
  labels the pill “Lock in”/“Bloqueo en”, and uses the engine kind/name for the
  tooltip. The 5/1-minute warnings use the same time. `LockTimerStatus` event
  messages now compare engine override fields, not the old reason strings.
- **Usage:** `UsageInterval.CountsTowardLimit` (default true for existing rows)
  records whether an interval counts against the daily limit. The sampler marks
  bonus/unlock usage `false`; the server excludes those intervals from
  `UsedTodaySeconds`. Bonus/unlock usage still appears in the actual usage
  report but does not consume the daily limit.
- **Bonus:** server and client extend an active bonus from its current end.
  Bonus no longer sets `ManualLock`. Lock now clears bonus/unlock and wins.
- **Offline merge:** the client applies queued usage locally before it can sync
  (in the 15 s sync loop), dedupes by interval id, and on every server state
  keeps `max(local used, server used)` so a behind server state cannot give back
  time. Absolute override starts/ends are persisted in `LockStates`
  (`UnlockStartUtc`, `BonusStartUtc`; migration `Job20hLockEngine`).
- **Migration:** `20261102190953_Job20hLockEngine` adds
  `LockStates.UnlockStartUtc`, `LockStates.BonusStartUtc` and
  `UsageIntervals.CountsTowardLimit` (existing rows default **true**).

## Test table

All rows were executed by the table-driven tests and passed
(**576/576 Linux** and **576/576 Dell XPS**).

| # | Scenario | Expected | Actual | Result |
|---|---|---|---|---|
| 1 | 4:33 PM, schedule 5:00 PM, limit left 28m | not locked, Schedule, 27m | not locked, Schedule, 1620s | PASS |
| 2 | same, limit left 20m | not locked, DailyLimit, 20m | not locked, DailyLimit, 1200s | PASS |
| 3 | locked at 5:00 by schedule, 28m limit, +30 bonus | free 30m, then Schedule; limit 28m | free Bonus 1800s; after bonus locked Schedule; limit 1680s | PASS |
| 4 | schedule ends 7:00 PM, 28m limit | 28m DailyLimit at 7:00; lock at 7:28 | 1680s DailyLimit; locked DailyLimit at 7:28 | PASS |
| 5 | limit out at 2:00, schedule 5:00, +30 | free 2:00–2:30, then DailyLimit | free Bonus 1800s; locked DailyLimit after; limit 0 | PASS |
| 6 | unlock until 12:00 AM at 8:25, 8:30 overnight schedule | no lock at 8:30; lock at 12:00 | free 12900s to midnight; locked Schedule at midnight | PASS |
| 7 | unlock while DailyLimit-locked | free until T, lock at T | free 3600s DailyLimit; locked at T | PASS |
| 8 | new bonus while bonus active | add to current end | start unchanged, end +30m | PASS |
| 9 | Lock now during bonus/unlock | locked LockNow, overrides cancelled | locked, LockNow, 0s | PASS |
| 10 | 9 PM–7 AM schedule, midnight reset | locked schedule, fresh limit at 7 AM | locked at 00:30; 7200s DailyLimit at 7:00 | PASS |
| 11 | 20 min idle with DailyLimit next | countdown unchanged | 3600s before and after | PASS |
| 12 | offline rules/limit + reconnect | rules and used local count enforced | 1800s then locked DailyLimit after use | PASS |
| 13 | Phoenix (no DST) + Los Angeles (DST) | local schedule wins | 3600s Schedule in both | PASS |
| 14 | no rules | never locked, no pill | not locked, no time, no pill | PASS |

Additional integration tests: server `GetStateAsync` returns DailyLimit when it
comes before the schedule; usage sync stores and excludes the override flag;
server bonus extends and lock-now cancels; kid Home shows “27m” and “27m” from
`LimitRemainingSeconds`/`TimeUntilLockSeconds`; source checks prove Home, kid,
client, helper and DataSync use the shared output.

## Release v0.17.23

- Branch `fix/job20h-lock-engine-0.17.23`; code commit **`b65ea79`**
  (*Job 20h: unify lock decisions in one shared engine*), fast-forwarded to
  `main`. Annotated tag **`v0.17.23`** is on `b65ea79`.
- Release workflow **41517948457** — test, Windows, images and release all
  succeeded.
- Tests: **576/576 Linux SDK 10.0.402** and **576/576 Dell XPS**.
- Windows local validation build on the XPS: MSI **144,052,224** B
  `D572AEB781551139DDB6928240755F6B296485089497CDD3E4BB3D00BDBCF2F9`;
  Setup **261,350,228** B
  `FF205D2ECC3F3E3FD89CA493794C9206C727F8742534713607C839E10C71BB16`.
- CI release assets (catalogue): MSI **144,011,264** B
  `6eccb0fe128fe25e7b028dd011fa57eb78193095fd3ddfeb468b4eb6d27af96e`;
  Setup **261,309,268** B
  `ed97323aa760b5392c4919efa323e6976446aee195bd9b53dedf75d83629af24`.
- Web image
  `ghcr.io/ziur-vela/parental-opcontrol@sha256:3d37a3aac391ad15044b8312a6b335dce556814089604ea6ffb282fe5b830514`;
  updater
  `ghcr.io/ziur-vela/parental-opcontrol-updater@sha256:9c036d4fb767709b036547b39ff6781c998aa6b1bcc4bb549ef759ee5ca70e22`.

### Staging

- Manual backup
  `/opt/stacks/opcontrol-parental-staging/backups/parental-pre-job20h-staging-20261102T201818Z.sql.gz`
  (**5,707,473** B; gzip OK; dump complete); `.env` and compose backups with
  the same timestamp.
- Updater backup `/data/backups/parental-pre-update-20261102T202705Z.sql`.
- 0.17.22 → 0.17.23 success. `/version`
  **v0.17.23 · build 2026-11-02 · b65ea79**; `/health` **200**;
  migrations **24** (`20261102190953_Job20hLockEngine`).
- Staging data after the temporary screenshot family was removed: 2 families,
  2 children, 11 devices, 2 users, 0 `job20h%` users.
- `.env` pinned to the final digests (`.env.bak-job20h-pin-20261102T202951Z`);
  `docker compose config -q` OK.

### Production

- Manual backup
  `/opt/stacks/opcontrol-parental/backups/parental-pre-job20h-prod-20261102T201818Z.sql.gz`
  (**6,746,457** B; gzip OK; dump complete).
- Keys `/opt/stacks/opcontrol-parental/backups/parental-keys-pre-job20h-20261102T201818Z.tgz`
  (714 B); `.env` and compose backups with the same timestamp.
- Updater backup `/data/backups/parental-pre-update-20261102T202900Z.sql`.
- 0.17.22 → 0.17.23 success. `/version`
  **v0.17.23 · build 2026-11-02 · b65ea79**; `/health` **200**;
  migrations **24**.
- Post-release state: 1 family, 1 child, 2 devices, 1 user (owner only), 0
  `job20h%` users.
- `.env` pinned to the final digests (`.env.bak-job20h-pin-20261102T202951Z`);
  `docker compose config -q` OK.
- Catalogue files inside `parental-web` and on the host match the manifest
  (MSI `6eccb0fe…`, Setup `ed97323a…`).

### X1 (only)

- X1 was on 0.17.22. The CI MSI was downloaded to `C:\Temp\Job20H`, SHA-256
  checked against `6eccb0fe…`, and installed with
  `msiexec /i … /qn /norestart`; exit **0**.
- Client, Helper and Luna are **0.17.23.0**; service **Running / Automatic**.
  Client PID 13696, Helper PID 17972 in session 1 as Emma, Luna PID 14772.
- Helper log: `2026-11-02T20:33:47Z helper-started session=1 user=Emma`, then
  `countdown-shown`.
- Read-only `state.json` after the restart:
  `TimeUntilLockSeconds=14573`, `TimeUntilLockKind=DailyLimit`,
  `LimitRemainingSeconds=14573`, `LimitMinutes=300`,
  `NextScheduleLockUtc=2026-11-03T03:30:00Z`, `HasPill=true`. This is the
  required proof: the 5 h limit (4h 2m 53s left at capture) wins over the
  8:30 PM schedule; the pill is showing the daily-limit time.
- The server reports `ZIUR-X1-LAPTOP` **0.17.23** (LastSeen
  20:33:54Z). The Yoga remains 0.17.19 and was not touched.
- `C:\Temp\Job20H` removed. No owner/child login, PIN change, Windows setting,
  display scaling or family change.

## Screenshots

`/srv/opcontrol-parental/docs/job-20h-screens/` and repo
`docs/job-20h-screens/`:

- `pill-daily-night.png`, `pill-daily-gray.png`, `pill-daily-night-es.png` —
  “Lock in 27m” / “Bloqueo en 27m” in both themes and languages;
- `kid-home-{night,gray}-en.png`, `kid-home-night-es.png` — Time left today
  **27m** and Next lock **27m** (screen time);
- `dashboard-home.png` — parent dashboard showing **27 min left today** and
  the 8:30 PM schedule detail;
- `index.html` — gallery.

The pill/kid shots come from the real paint/UI code on the XPS. The dashboard
shot is a real staging render with a temporary family (“Job20H Shot”, 300-minute
limit, 4h33 used, 20:30 schedule) that was deleted immediately afterwards; no
production account or child/owner login was used.

## Cleanup and limits

- XPS: `C:\ocp-build\p20h` and `p20h-final` removed with `cmd rmdir /s /q`;
  build/test scripts and tarballs removed.
- X1: `C:\Temp\Job20H` removed; runtime data, device credential, Luna key,
  config and PIN untouched.
- Staging: temporary screenshot family, child, user, device, schedule, limit
  and usage intervals deleted; 0 `job20h%` users remain.
- No production temporary users; no owner/child login; no Windows setting,
  display scaling, PIN or family change; no remote session left open.
- The X1 interactive screen was not captured (that would require logging in as
  Emma); the proof is the helper log, installed file versions, service state,
  the read-only `state.json` and the server-reported client version.
