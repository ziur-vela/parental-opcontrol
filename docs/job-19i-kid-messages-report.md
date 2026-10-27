# Job 19i — My OpControl Messages flicker / Enter, and Luna's sleep hit area (v0.17.14)

**Status: complete and released.** v0.17.14 is on staging, production and **X1**.
**Yoga was offline for the whole job** (Tailscale and TRMM both down, last seen
2026-10-27 05:19 UTC on 0.17.13); it will take 0.17.14 from the normal catalog
when it is next online/restarted. No Windows setting, family setting, Luna
setting or category setting was changed.

- Branch: **`fix/job19i-messages-moon-0.17.14`** from `main` (`99d8f6d`, v0.17.13).
- Commit: **`40a8d9b`** — “Job 19i: stop kid message flicker and shrink Luna's
  sleep hit area”; `main` fast-forwarded to the same commit; tag **v0.17.14**.
- Release workflow: **40531037422**, success; release published
  **2026-10-27 15:34:12 UTC**.
- Tests: **392/392 Linux SDK 10.0.402** and **392/392 Dell XPS**; full Windows
  solution build **0 errors / 0 warnings**.
- Build host: **Dell XPS** (`Ziur-Dell-XPS`, `100.98.112.35`), SDK 10.0.402.
- No database migration.

## 1. Cause of the flicker

Both messaging screens rebuilt their whole message list on every poll:

- **My OpControl (Avalonia)** — `MyOpControlWindow` had a 3 s `DispatcherTimer`
  that called `LoadAsync()`. `LoadAsync()` always called `RenderCurrent()`, which
  did `_content.Children.Clear()` and rebuilt the screen. On Messages that meant
  every bubble was destroyed and recreated every 3 s, which is the quick redraw
  Ziur saw. The same unconditional rebuild hit Home, Schedule, Rules and
  Activity. It also explained why a typed message could be lost when the editor
  was not focused.
- **Lock screen panel (WinForms)** — `LockMessagesForm` had a **2 s** poll. Every
  tick called `RebuildThread()`, which cleared `TableLayoutPanel`, its row styles
  and all rows, then re-added every bubble. That is the ~2 s flicker in the lock
  messaging panel.

## 2. Flicker / Enter fix

### Shared thread diff

New `src/OpControl.Parental.Contracts/MessageThreadDiff.cs`:

- normalizes the thread (drops deleted, orders by `CreatedUtc` then `Id`);
- compares two reads and returns `Added`, `Updated`, `Removed`, `OrderChanged`
  and `Changed`;
- an identical poll returns `Changed = false`, so neither screen touches the UI.

Both UIs now use it. A status-only change is `Updated` (in place), not a rebuild.

### My OpControl Messages

- `MyOpControlWindow.RenderCurrent()` is no longer called by the timer on
  Messages. `LoadAsync()` calls `UpdateMessagesInPlace()` instead.
- `UpdateMessagesInPlace()`:
  - unchanged poll → returns without touching controls (typing, focus and scroll
    are untouched);
  - new message → `AddMessageRow()` adds **one** row, once;
  - status/ack change → `UpdateMessageRow()` rewrites that row's body/meta only;
  - removal/reorder → rebuilds the list (rare) but still keeps the editor;
  - keeps the scroll offset when the child has scrolled up; follows the tail
    only when they were already at the bottom;
  - `SendAsync` scrolls to the end and refocuses the composer after sending;
  - `MarkSeenAsync` is sent once per message id (`_seenRequested`), not on every
    rebuild.
- Home/Schedule/Rules/Activity: `LoadAsync()` computes a structural snapshot
  signature that **excludes** the per-poll runtime `UpdatedUtc` and
  `DailyLimitRemainingSeconds`. When the signature is unchanged the screen is not
  redrawn; the live “time left” text is updated in place instead. Summary
  refreshes (every 5 min / on push) still rebuild the screen once.

### Enter / Shift+Enter / IME

- The composer's `KeyDown` is now handled with
  `AddHandler(KeyDownEvent, …, RoutingStrategies.Tunnel)`. Previously the
  TextBox's own `AcceptsReturn` handling consumed Enter before the bubbling
  handler, so nothing was sent.
- Enter (no Shift) sends; Shift+Enter inserts a newline
  (`InsertComposerNewline`). Enter text-input is suppressed so a newline cannot
  sneak in after sending.
- `Key.ImeProcessed` is ignored (IME candidate commit never sends), and dead
  keys/accents arrive as normal text input.
- Empty/whitespace-only text is rejected; the 500-character `MaxLength` and the
  `> 500` guard remain.
- The same rule applies in `LockMessagesForm`; its WinForms `KeyDown` also
  ignores `Keys.ProcessKey` (IME) before handling Enter.
- The lock panel now applies the same incremental diff and scroll behaviour, so
  its 2 s poll no longer clears and rebuilds the thread.

### Tests

New `tests/OpControl.Parental.Tests/Job19IMessageFlickerTests.cs` (16 cases):

- diff: unchanged poll has no change; new message appends once; status is an
  in-place update; deleted message is removed;
- real `MyOpControlWindow` with a controllable data source: unchanged poll does
  not rebuild or add rows; typed text and focus survive; new message appends
  once; second poll does not append again; status updates without a rebuild;
- Enter sends, Shift+Enter adds `\n`, IME/`á`/`ñ` input never sends,
  whitespace-only and >500 chars are rejected;
- source checks that both the kid window and the lock panel use the shared diff
  and the IME guard.

## 3. Luna sleeping moon — cause and fix

The **hover/wake area** was still the old square `[-4k, 44k] × [-4k, 44k]`
(`IsCursorOverMoon`), not the visible quarter circle. The **OS window region**
in Sleep was a 48k × 48k square (`UpdateRegion`). So:

- moving over the browser Back button (inside the square but outside the yellow
  sliver) started the 0.4 s hover and woke Luna;
- the square kept Luna's window over that corner, and the waking 200×200 region
  then covered the click.

The Z's were why the square existed: they float past the yellow sliver and the
old square region was used to keep them painted.

Fix:

- New `src/OpControl.Parental.Luna/LunaSleepGeometry.cs` describes the visible
  sliver: circle centre `(-20k, -20k)`, radius `48k`, and its ellipse bounds.
- `IsCursorOverMoon` (Sleep) now uses **only** `IsInsideSliver`; the 0.4 s hover
  delay is unchanged.
- `IsPointOverSleepMoon` (WM_NCHITTEST) uses the same helper.
- `UpdateRegion` (Sleep) creates an **elliptic region** with
  `CreateEllipticRgn` from the sliver bounds, not a rectangle. Everything
  outside the yellow shape is no longer part of Luna's window, so the OS passes
  mouse moves and clicks straight to the window underneath. The region is
  clipped to the window's top-left quarter, which is the visible yellow sliver.
- `LunaSleepGeometry.EllipseBounds` is exercised at k = 1.0/1.6/2.2/3.0 and
  100/125/150/200% scaling.

### Real Windows proof (Dell XPS, default k = 1.6, 100%)

The Release Luna window was started in the interactive desktop through a
temporary scheduled task (deleted afterwards) and queried with
`GetWindowRgn`/`PtInRegion`, `WM_NCHITTEST` and `WindowFromPoint`:

```
window rect  = 0,0,960,826
region box   = -109,-109,44,44          (ellipse bounds, not a square)
PtInRegion   (13,13)  = true            (inside yellow)
PtInRegion   (1,1)    = true
PtInRegion   (24,24)  = false
PtInRegion   (44,44)  = false           (old square corner)
PtInRegion   (44,4)   = false
PtInRegion   (64,64)  = false
PtInRegion   (0,70)   = false
WM_NCHITTEST (13,13)  = 1  (HTCLIENT)
WM_NCHITTEST (44,44)  = -1 (HTTRANSPARENT)
WM_NCHITTEST (64,64)  = -1
WindowFromPoint (13,13) = Luna window
WindowFromPoint (44,44) = other window (desktop)
WindowFromPoint (64,64) = other window (desktop)
```

This is the actual OS region, not a headless approximation.

**Visual consequence:** because the window region is now only the yellow
sliver, the decorative Z's that float outside it are clipped; only the part of
the Z animation that overlaps the sliver is painted. They can no longer be
clicked or wake Luna. Keeping the Z's fully visible *and* click-through would
need a separate click-through/layered overlay; that was not added in this job.

## 4. Release v0.17.14

- `Directory.Build.props` **0.17.14**; notes
  `.github/release-notes/v0.17.14.{en,es}.md`; annotated tag **v0.17.14**.
- Workflow **40531037422** success; published 15:34:12 UTC.
- Web image:
  `ghcr.io/ziur-vela/parental-opcontrol@sha256:4160c03ebbbee425d94bc7f269490d3a0ba97a3cd5c46c34ae144f55ada26f8a`
- Updater image:
  `ghcr.io/ziur-vela/parental-opcontrol-updater@sha256:e53b2874ddf4c6768c0fbcf170d9ea06116dbc5c64f0b73e5d34dc7fce539f13`
- MSI `OpControlParental-0.17.14.msi` **143,867,904** bytes SHA-256
  `e93a5968fa0176672a635657472046dbd0d5d6cca3aa36c87580915cfcd6c41c`.
- Setup `OpControlParental-Setup-0.17.14.exe` **261,157,716** bytes SHA-256
  `1f87b703be4f8d6ebcd9d4bfa1287fac297070d9cae789fcf9d20e46f1c42e41`.
- Production catalog `data/releases` has both 0.17.14 files with the same
  SHA-256; 0.17.13 remains as the previous release.

### Staging

- 0.17.13 → 0.17.14 success; updater backup
  `/opt/stacks/opcontrol-parental-staging/data/updater/backups/parental-pre-update-20261027T153457Z.sql`
  (12,101,674 bytes).
- `/version` **v0.17.14 · build 2026-10-27 · 40a8d9b**; `/health` **200**.
- Web `4160c03ebbbe` healthy; updater `e53b2874ddf4`.
- `.env` pinned; backup
  `/opt/stacks/opcontrol-parental-staging/.env.bak-job19i-20261027T152704Z.full`;
  `docker compose config -q` OK.

### Production

- Manual pre-update backup:
  `/opt/stacks/opcontrol-parental/backups/parental-pre-job19i-20261027T152704Z.sql.gz`
  (**5,987,776** bytes; gzip OK; “PostgreSQL database dump complete” = 1), plus
  `docker-compose.yml.bak-job19i-20261027T152704Z`,
  `.env.bak-job19i-20261027T152704Z`, and
  `parental-keys-pre-job19i-20261027T152704Z.tgz` (contains
  `keys/key-b957b495-….xml`).
- Updater backup
  `/opt/stacks/opcontrol-parental/data/updater/backups/parental-pre-update-20261027T153819Z.sql`
  (15,315,885 bytes).
- `/version` **v0.17.14 · build 2026-10-27 · 40a8d9b**; `/health` **200**;
  `/luna` **302**; web container healthy.
- `.env` pinned to the new digests; `docker compose config -q` OK.
- Data unchanged: Luna enabled, night, daily limit 60; categories `adult`, `ai`,
  `fakenews`, `gambling`, `social` all true; 2 Luna chats / 23 messages; manual
  lock false; 0 active remote sessions.

### Laptops (normal catalog)

- **ZIUR-X1-LAPTOP** (device `89f9db76-3e1c-458a-8053-44cf26df451e`): update
  15:40:20 → 15:40:53 UTC, `msiexec` exit 0, service healthy. Client/Helper/Luna
  **0.17.14.0**, service Running/Automatic, one Luna in session 1 in `Sleep`
  state, dashboard **0.17.14 healthy**. Kid theme `night`; no sleep-scale file
  so default 1.6. No helper/config errors in the tail of `luna.log`.
  A redundant dashboard Update Now was requested after the device had already
  updated; its temporary parent was deleted and the transient “requested”
  status/event was normalized to healthy (0 temp users remain).
- **EMMA-YOGALAPTOP** (`d3b94f63-678b-492e-83d6-19f3036b89f8`): **offline**
  (last seen 05:19 UTC on 0.17.13; Tailscale and TRMM both offline). A magic
  packet was sent to both its Wi-Fi and Ethernet MACs from the LAN; it did not
  wake. It will update from the catalog when it is next online or its service is
  restarted. Its sleep scale 1.9 and settings are untouched.

## 5. What Ziur should check by hand

On X1 (v0.17.14, 150%):

1. Open **My OpControl → Messages** and leave it open for a minute. It must not
   flash/redraw; typing must survive.
2. Type a message and press **Enter** → it sends. **Shift+Enter** → new line.
   Try `á`, `ñ` and an IME candidate; they must not send. Empty/space-only must
   not send.
3. Scroll up in a long thread; a poll must not jump the scroll to the bottom.
4. Open the lock screen's **Message my parents** panel and repeat Enter /
   Shift+Enter; it must not flicker every 2 s.
5. Check Home/Schedule/Rules/Activity do not flash while open.
6. Move the mouse over the browser **Back** button next to the sleeping moon:
   Luna must **not** wake after 0.4 s; click it and the browser must get the
   click. Move over the yellow sliver: Luna must wake after 0.4 s.
7. Repeat the corner check on Yoga at its 1.9 sleep size after it updates.
   Note: the Z's outside the yellow sliver are clipped in this build.

## 6. Cleanup / notes

- XPS Job 19h leftovers finished: `C:\ocp-build\parental-19h` (1.36 GB) and
  `C:\ocp-build\parental-19h.zip` (41,322,091 bytes) removed. The Job 19i build
  copy `C:\ocp-build\parental-19i` and its tgz were also removed after the
  tests. The XPS was reachable for the whole job.
- XPS verification scripts and the temporary scheduled task were removed.
- Local transfer archives/scripts removed; temporary production parent deleted
  (0 `job19i-%` users).
- No owner/child login, no PIN/password/family/date-of-birth change, no Windows
  scaling/setting change, no OC git identity change, no Tailscale change.

## Limits

- Yoga could not be updated or verified (offline); its 0.17.14 install is the
  only open item.
- The real OS-region proof was done at the default k = 1.6 / 100%; other sleep
  sizes and 100–200% scaling are covered by the geometry tests, not by a real
  mouse on each combination.
- The Z's outside the yellow sliver are clipped by the OS region (see §3).
- The Windows build was the full solution + 392/392 tests on XPS; installers
  were produced by the release workflow (as in previous jobs).
