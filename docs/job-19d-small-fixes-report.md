# Job 19d — small fixes from Ziur's safety test (v0.17.10)

**Status: complete and released.** v0.17.10 is on staging, production and **both**
laptops (X1 and Yoga). The release uses the normal catalog now that the Job 19c
hold is gone. Luna and the Public AI chats category were **not changed** by this
job; Emma's date of birth was **not added or changed**.

- Branch: **`fix/small-fixes-0.17.10`** from `main` (`01084ed`, v0.17.9).
- Feature commit: **`e6766b3`** — “Job 19d: fix Luna notice, dashboard times and
  child profile text”; `main` fast-forwarded to it.
- Release commit: **`1bb42c6`** — “Job 19d: release v0.17.10”; tag **v0.17.10**.
- Release workflow: **40405832440**, success; release published
  **2026-10-26 21:43:45 UTC**.
- Build host: **Dell XPS** (`Ziur-Dell-XPS`, `100.98.112.35`), SDK 10.0.402.
- Tests: **364/364 Linux SDK 10.0.402** and **364/364 Dell XPS**; full solution
  build **0 warnings, 0 errors** on both.
- No database migration.

## 1. Duplicate notice above the Luna input — fixed

The server already puts the fixed reply into the chat as the Luna message, but
the blocked response also carried the same text in `Notice`, so the Luna app
painted it again above the input.

- Added `LunaAppView.NoticeAfterSend(response)`: when the response has a Luna
  bubble, the above-input notice is dropped; when there is no Luna bubble (real
  error/limit), the notice is kept.
- `SendFromInputAsync` now uses that helper and still clears `_notice` at the
  start of every send, so a real-error notice disappears on the next send.
- The duplicate is gone for blocked and reply-blocked results. The chat fixed
  reply stays. Real errors/limit notices still work.
- Test: `Job19DSmallFixTests` covers blocked/reply-blocked → null, real
  error/limit → notice, and the send wiring.
- Before/after: `luna-blocked-notice-{before,after}.png` (real Avalonia headless
  Skia render at 683×600). The before image shows the chat reply plus the yellow
  box; the after image shows only the chat reply.

## 2. Dashboard times in UTC — fixed

- Added `DashboardTime` (`Convert`, `Format`, `FamilyZoneAsync`). The dashboard
  has no per-parent time zone, so it uses the family's existing device time zone
  (the same source the rest of the dashboard already uses).
- Luna pages: `LunaChild`, `LunaReview` and `LunaSetup` now use the family zone
  for Needs review, the review page, profile “Updated by”, setup “tested” and
  every other date/time on those pages. The “tested today” check also compares
  local dates now.
- Other dashboard pages fixed from server-local/UTC to the family zone:
  `Team` invitation expiry, `Devices` Last seen (per device zone), `Home` device
  Last seen, `Downloads` published time, `ServerAdmin` family/activity/audit
  times and `ServerUpdates` last-check/attempt times.
- No `.ToLocalTime()` remains in the dashboard/server pages.
- Example verified on staging: a message at **20:43 UTC** now shows
  **26 Oct, 13:43** (Pacific daylight, matching Hermosillo in October) instead
  of **26 Oct, 20:43**.
- Before/after: `before-luna-child.png` / `after-luna-child.png`,
  `before-luna-review.png` / `after-luna-review.png`,
  `before-luna-setup.png` / `after-luna-setup.png`.

## 3. “Messages today” tile — fixed

The tile now uses a localized label instead of a bare number:

- EN: **55 left today**
- es-MX: **Quedan 55 hoy**

New keys `Luna.Child.MessagesLeftToday` in both resx files. Tested in
`Job19DSmallFixTests` and shown in `after-luna-child.png` /
`after-luna-child-es.png`.

## 4. Missing date of birth — fixed

- The Luna page now says:
  “Add {child}'s date of birth in Children so Luna can match answers to
  {child}'s age.” / “Agrega la fecha de nacimiento de {child} en Niños para que
  Luna adapte las respuestas a la edad de {child}.”
- It links to a new **Children** page at `/children#child-{id}`.
- New `Children.razor` page lists the family's children, shows a date-of-birth
  field and a Save button, validates invalid/future dates, and clears the value
  if emptied. It is available to parents with the Luna permission; a **Children**
  item was added to the sidebar.
- No date of birth was added or changed by this job. The page is for Ziur/parents.
- Before/after: `before-luna-child.png` (old “This child has no date of birth”),
  `after-luna-child.png` (helpful text + OPEN CHILDREN), `after-children.png`
  (new page), `after-children-es.png` (es-MX / light).

## Tests

New `tests/OpControl.Parental.Tests/Job19DSmallFixTests.cs` (9 tests):

1. blocked/reply-blocked results do not keep the above-input notice;
2. real error/limit notices still show;
3. `DashboardTime` converts 20:43 UTC to 13:43 in the family zone;
4. `FamilyZoneAsync` uses the first device time zone;
5. Luna pages no longer format with the UTC constant;
6. other dashboard pages use the helper and have no `.ToLocalTime()`;
7. Messages tile strings in EN/es-MX (including “55 left today” / “Quedan 55 hoy”);
8. missing-DOB text/link + Children page wiring and Spanish name;
9. Luna send uses the notice guard and clears it on the next send.

Existing tests still pass: **364/364** on Linux and XPS.

## Release v0.17.10

- `Directory.Build.props` **0.17.10**; notes
  `.github/release-notes/v0.17.10.{en,es}.md`; annotated tag **v0.17.10**.
- Workflow **40405832440** success; release published 21:43:45 UTC.
- Web image:
  `ghcr.io/ziur-vela/parental-opcontrol@sha256:4459cedcedc88ed81eca01cf5f694a8a390008c77273f1a26773ad05ec16db5e`
- Updater image:
  `ghcr.io/ziur-vela/parental-opcontrol-updater@sha256:80ee4608dedae54ba9cdd844b9a2d5b126dd110a021eb9f6bd88b7da036ca607`
- MSI `OpControlParental-0.17.10.msi` **143,904,768** bytes SHA-256
  `da6d1be4e87f4e90c1b3bb7f417c6edb79999b0e7800c3d5bb833bf1229e2459`.
- Setup `OpControlParental-Setup-0.17.10.exe` **261,194,580** bytes SHA-256
  `342ee98b3155e17c930b71ee382d513f4d3cb6aaea141fd54fd5ccca399a7491`.

### Staging

- Update 0.17.9 → 0.17.10 success; backup
  `/data/backups/parental-pre-update-20261026T214414Z.sql` (12,089,089 bytes,
  `PostgreSQL database dump complete`).
- `/version`: `v0.17.10 · build 2026-10-26 · 1bb42c6`; `/health` **200**.
- `.env` pinned to the new digests; backup
  `.env.bak-job19d-20261026T214500Z`; `docker compose config -q` OK.
- Temporary verification data (one chat, two messages, usage 5/60, profile,
  temporary parent) was removed after the screenshots; Luna settings were
  restored (key/model/test cleared, child disabled) and the child's DOB was
  never set.

### Production

- Manual pre-update backup:
  `/opt/stacks/opcontrol-parental/backups/parental-pre-job19d-20261026T215500Z.sql.gz`
  (**5,923,911** bytes; `gzip -t` OK; `PostgreSQL database dump complete`),
  plus `docker-compose.yml.bak-job19d-20261026T215500Z`.
- Updater backup:
  `/data/backups/parental-pre-update-20261026T214610Z.sql` (14,925,160 bytes,
  dump complete).
- `/version`: **v0.17.10 · build 2026-10-26 · 1bb42c6**; `/health` **200**.
- `/api/releases`: current **0.17.10**, SHA
  `da6d1be4…`, 143,904,768 bytes, previous **0.17.9**; `/downloads` shows
  V0.17.10 and the Setup/MSI links.
- `.env` pinned to the new digests; backup
  `.env.bak-job19d-20261026T214700Z`; `docker compose config -q` OK.

### Both laptops (normal catalog)

- Updated through the dashboard per-device **Update Now** path (normal product
  catalog, no manual MSI):
  - **ZIUR-X1-LAPTOP**: update log 21:48:45 → 21:49:20 UTC, msiexec exit 0,
    new service healthy.
  - **EMMA-YOGALAPTOP**: update log 21:49:05 → 21:49:39 UTC, msiexec exit 0,
    new service healthy.
- Both: client/helper/Luna **0.17.10.0**, service Running/Automatic, one Luna
  each, dashboard **online · healthy**, same device rows:
  `89f9db76-3e1c-458a-8053-44cf26df451e` (X1) and
  `d3b94f63-678b-492e-83d6-19f3036b89f8` (Yoga), not revoked.
- Yoga sleep size stayed **1.9** after the update
  (`C:\Users\Emma\AppData\Local\OpControlParental\luna-sleep-scale.txt`); Luna
  log for the new process shows `sleep_scale=1.9 visible_dip=53.2x53.2`.
- No lock was triggered. `LockStates` for Emma: `ManualLock=false`,
  `UnlockUntilUtc=2026-10-27T07:00:00Z` (pre-existing). 0 active remote
  sessions.
- Luna unchanged: protected key length **219**, `LastTestOk=true`, child Luna
  enabled. Public AI chats category unchanged and still **on** for Emma (owner
  set it before Job 19c; this job did not touch it).
- Short log check after both updates: no product errors; Luna sleeping.

## Screenshots

`/srv/opcontrol-parental/docs/job-19d-screens/index.html`:

- `luna-blocked-notice-{before,after}.png`
- `before-luna-child.png` / `after-luna-child.png`
- `before-luna-review.png` / `after-luna-review.png`
- `before-luna-setup.png` / `after-luna-setup.png`
- `after-luna-child-es.png`
- `after-children.png` / `after-children-es.png`

The Luna-app images are real Avalonia headless Skia renders. The dashboard images
are headless Chromium captures of staging. The temporary parent/chat/usage/profile
were deleted after capture.

## Cleanup

- Temporary production parent `job19d-admin@opcontrol.local` deleted; remaining
  `job19d%` users **0**; active remote sessions **0**.
- Temporary staging parent, chat, messages, usage/profile test data removed;
  Luna setup/key/test state and child Luna enabled state restored; DOB still null.
- XPS copy `C:\ocp-build\parental-19d` removed; local temporary harness/scripts
  removed.
- No production owner/child login, no PIN/password/family/Windows setting change,
  no date of birth added, Tailscale untouched.

## Limits / notes

- The dashboard has no per-parent time zone setting; it uses the family's device
  time zone, which is the same source the other dashboard pages already used.
- The Luna-app before/after images are renderer captures, not on-screen photos.
- The new Children page is intentionally minimal: date of birth only, with
  invalid/future validation. It does not change any other child data.
- Server-admin pages use the admin’s own family device time zone; there is no
  browser-zone fallback because every current family has a device.
