# Job 19h — Luna chat back button only clicks on its own top-left corner (v0.17.13)

**Status: complete and released.** v0.17.13 is on staging, production and **both**
laptops (X1 and Yoga). The back button and the other top-left controls now work
across their whole visible area. Luna and the Public AI chats category settings
were not changed.

- Branch: **`fix/luna-back-hit-area-0.17.13`** from `main` (`731b5ce`, v0.17.12).
- Feature commits: **`59141a6`** (fix), **`14d98b2`** and **`547b4cf`** (test
  coverage); `main` fast-forwarded to **`1a4a0a4`** — “Job 19h: release
  v0.17.13”; tag **v0.17.13**.
- Release workflow: **40427942903**, success; release published
  **2026-10-27 00:48:26 UTC**.
- Build host: **Dell XPS** (`Ziur-Dell-XPS`, `100.98.112.35`), SDK 10.0.402.
- Tests: **376/376 Linux SDK 10.0.402** and **376/376 Dell XPS**.
- No database migration.

## 1. The real cause

The button itself is not transparent or undersized: `IconButton` is a 44×44 DIP
Button with an opaque rounded background, and its hit box matches its bounds
(the transparent rounded corner is the only part that is not part of the drawn
button). There is no scaling transform on it.

The dead area was an invisible element above it. In `LunaWindow.axaml` the
`SleepLayer` (the moon canvas and the Z's) is declared **after** `Blob`/`ChatRoot`,
so it sits above the chat. During chat the moon is hidden by setting only
`_moonSlide.Opacity = 0`, and **opacity 0 does not remove a visual from hit
testing**. The invisible moon's ellipses stayed over the top-left of the open
chat. At 150% the back button sits under that invisible moon; only the small
top-left part of the 44×44 button, outside the moon ellipse, remained clickable.
That is exactly "only the top-left corner of the button works".

A headless hit test reproduced it before the fix: sampling the centre/edges of
`ChatBack` returned an `Ellipse` from the moon layer instead of the Button. The
same invisible layer had been over the home tiles at the top-left.

## 2. Fix

In `LunaWindow.UpdateVisuals` the sleep layer is now disabled for hit testing
whenever the moon is hidden (`MorphOpen`, `Chat`, `MorphClose`):

```csharp
_sleepLayer.IsHitTestVisible = !moonHidden;
_zLayer.IsHitTestVisible = false;   // decorative Z's
```

`ResetToSleep` restores `_sleepLayer.IsHitTestVisible = true`. The moon remains
clickable in Sleep/Waking/Awake/Retract because the window-level pointer handler
and the visible moon layer are unchanged. The decorative greeting bubble was
already `IsHitTestVisible=false`.

No button layout, DPI or window-region change was needed: the chat window region
is the full rounded chat rectangle, and the title bar is well inside it at all
four scalings.

## 3. Tests

New `tests/OpControl.Parental.Tests/Job19HHitTargetTests.cs`:

- Opens the real `LunaWindow`, puts the real `LunaAppView` on a chat (with a child
  bubble and avatar), and morphs the window into Chat.
- For **both themes** and **100%, 125%, 150% and 200%** render scaling, checks
  nine points per control (four inside-corner points, four edge midpoints and the
  centre) with `InputHitTest`:
  - `ChatBack`, `ChatTheme`, `ChatMenu`, `ChatClose` (also asserts each is at
    least 44×44 DIP);
  - `ResizeGrip`, `ChildAvatar`;
  - all six home screen subject tiles.
- Asserts that the hidden sleep layer is **not** hit-testable while the chat is
  open, so no invisible moon or Z visual can overlap the title bar.

Before the fix the test failed at the first back-button point with
`hit Ellipse` from the moon layer; after the fix all points hit the intended
controls. Existing tests still pass: **376/376** on Linux and XPS.

## 4. Release v0.17.13

- `Directory.Build.props` **0.17.13**; notes
  `.github/release-notes/v0.17.13.{en,es}.md`; annotated tag **v0.17.13**.
- Workflow **40427942903** success; release published 00:48:26 UTC.
- Web image:
  `ghcr.io/ziur-vela/parental-opcontrol@sha256:259ac3cb7d833941afae89901c17e8cf39900aeb0f7232d94f6a09ce3990f465`
- Updater image:
  `ghcr.io/ziur-vela/parental-opcontrol-updater@sha256:bd5d29daaabda013b6f5b047042994b5b99495f80b635a03694c9a325115ed53`
- MSI `OpControlParental-0.17.13.msi` **143,794,176** bytes SHA-256
  `0891c12eb91b74f177b133a7f46cfab6961b403360ed5f3a674f339e0599a47d`.
- Setup `OpControlParental-Setup-0.17.13.exe` **261,083,988** bytes SHA-256
  `f98ef8d8dddba6fcfa91c5114878cb0452ebf481c3b8eae15a078b5597f76d29`.

### Staging

- Update 0.17.12 → 0.17.13 success; backup
  `/data/backups/parental-pre-update-20261027T004924Z.sql`.
- `/version`: `v0.17.13 · build 2026-10-27 · 1a4a0a4`; `/health` **200**.
- Web container `259ac3cb7d83` healthy; updater `bd5d29daaabd`.
- `.env` pinned to the new digests; backup
  `.env.bak-job19h-20261027T005017Z`; `docker compose config -q` OK.

### Production

- Manual pre-update backup:
  `/opt/stacks/opcontrol-parental/backups/parental-pre-job19h-20261027T005023Z.sql.gz`
  (**5,941,531** bytes; `gzip -t` OK; `PostgreSQL database dump complete`),
  plus `docker-compose.yml.bak-job19h-20261027T005023Z` and
  `.env.bak-job19h-20261027T005023Z`.
- Updater backup:
  `/data/backups/parental-pre-update-20261027T005030Z.sql`.
- `/version`: **v0.17.13 · build 2026-10-27 · 1a4a0a4**; `/health` **200**.
- Release catalog has 0.17.13 and keeps 0.17.12 as the previous release; MSI and
  Setup SHA-256 values match the release assets above.
- `.env` pinned to the new digests; `docker compose config -q` OK.

### Both laptops (normal catalog)

- **ZIUR-X1-LAPTOP**: update log 00:51:51 → 00:52:26 UTC, `msiexec` exit 0, new
  service healthy.
- **EMMA-YOGALAPTOP**: update log 01:30:04 → 01:30:29 UTC, `msiexec` exit 0, new
  service healthy. Yoga had gone offline at 23:54 UTC; it came back online at
  01:25 UTC after being woken, then updated through the normal catalog check.
- Both: client/helper/Luna **0.17.13.0**, service **Running/Automatic**, one Luna
  each in session 1, dashboard **online** on 0.17.13; same device rows:
  `89f9db76-3e1c-458a-8053-44cf26df451e` (X1) and
  `d3b94f63-678b-492e-83d6-19f3036b89f8` (Yoga), not revoked.
- X1 Luna started with `sleep_scale=1.6` (default); Yoga kept **1.9**.
- No lock was triggered: Emma `ManualLock=false`; 0 active remote sessions.
- Luna settings unchanged: protected key length **219**, `LastTestOk=true`, child
  Luna enabled, theme **night**, daily limit **60**.
- Category settings unchanged: `adult`, `ai` (Public AI chats), `fakenews`,
  `gambling`, `social` — all still enabled.

## 5. What Ziur should check by hand

On the X1 (150% scaling is the case that failed):

1. Open a Luna chat and click the **whole** round back button: top-middle,
   left-middle, right-middle, centre and the lower half — not only the top-left
   corner. It should return to Home every time.
2. In the same chat, click the theme button, the "..."/options menu and Close
   across their full area.
3. On Home, click the top-left subject tile near its left and top edges.
4. Drag the resize grip at the bottom-right and confirm the window resizes.
5. Repeat a quick check on the Yoga laptop (its Luna size is tuned to 1.9).

If any spot is still dead, note the exact control and screen position; that would
be a separate hit-area issue, not the invisible moon layer fixed here.

## 6. Cleanup / notes

- Yoga was offline at the first final check; it was woken and updated. Its last
  seen is now 01:30 UTC on 0.17.13.
- XPS went offline before the build-copy cleanup could run, so
  `C:\ocp-build\parental-19h` and `C:\ocp-build\parental-19h.zip` remain on the
  XPS until it is next reachable; they are build copies only.
- Local transfer zip and TRMM helper scripts were removed.
- No production owner/child login, no PIN/password/family/Windows setting change,
  no date of birth added, Tailscale untouched.

## Limits

- The headless test covers the Avalonia hit tree; the invisible-layer root cause
  is platform-independent. It does not drive a real mouse, so Ziur's hand check
  at 150% remains the final confirmation.
- The rounded transparent corners of the 44×44 icon buttons are not part of the
  drawn button; the visible button and a comfortable 44×44 hit box around it are
  covered.
