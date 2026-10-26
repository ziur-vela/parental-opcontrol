# Job 18.8 — Luna chat auto-scroll, wider window and wider bubbles (v0.17.7, X1 only)

Status: **complete and released.** v0.17.7 is on staging, production and the
**X1 only**. The Yoga was not updated and the release hold is unchanged. Luna
stayed enabled with the same key. The lock screen was not touched. No
owner/child login, no family, PIN, password, display-scaling or Windows setting
change.

- Branch: **`fix/luna-0.17.7`** from `main` (`9ffe04a`, v0.17.6).
- Commit: **`3d1e4b4`** — “Job 18.8: widen Luna chat and fix reply scrolling”;
  `main` fast-forwarded to it.
- Tag: **v0.17.7**; release workflow **40364912306** success; GitHub release
  published 2026-10-26 17:11:23 UTC.
- Design file: `/srv/oc-launcher/prompts/luna-design.md` section 1 updated
  (window size line and the 88% bubble rule); a copy is in
  `/srv/opcontrol-parental/docs/luna-design.md`.
- Tests: **338/338 Linux SDK 10.0.402** and **338/338 Dell XPS**; Windows
  solution build: **0 errors, 0 warnings**.

## Cause and fix — auto-scroll

The old reply path rebuilt the entire chat with `ShowChat()` three seconds after
the answer (the refresh in `SendFromInputAsync`). That created a **new
`ScrollViewer`** and immediately called `ScrollToEnd()` at `DispatcherPriority.Background`.
At that point the new viewer had no measured extent, so the scroll was applied
against an empty/old extent — the rebuilt conversation opened at the top. The
per-token `ScrollToEnd()` calls only worked while the *old* viewer existed, and
the code had no notion of “was the child at the bottom?” or of a reply taller
than the view. That is why the jump happened after the reply rather than while
the words were arriving.

The new policy lives in `LunaAppView`:

- **Follow the tail** (`_pinToBottom`) is restored on opening a chat and on
  send, and is saved across `ShowChat()` rebuilds. A rebuild at the bottom
  returns to the bottom; a rebuild while the child has scrolled up restores the
  old offset.
- **Scrolling waits for layout**: `ScrollToEndSoon()` marks a pending end,
  scrolls after `LayoutUpdated`, and retries at Background if the extent grew in
  the same frame. The old one-shot early scroll is gone.
- **Sending** sets the tail flag, hides the pill, and scrolls after the child
  bubble and the thinking animation are added.
- **A new reply** is followed according to its measured height: if it is taller
  than the viewport, its first line is placed at the top (8 px margin) and the
  child can read down; once the child moves the scroll, the app stops touching
  it. A short reply stays at the bottom while it reveals.
- **The child scrolling up** stops auto-scrolling. A new reply then shows a
  small **New message ↓** pill (localized EN/ES-MX, named `NewMessagePill`)
  above the composer; clicking it returns to the latest message.
- The streaming reveal was extracted into `AdvanceStreaming()` so the same code
  can be driven by the real `DispatcherTimer` and by the headless tests.

## Window size and persistence

New `LunaChatSize` helper:

- default width **50%** of the working area in DIP, default height **80%**;
- minimum **520 × 600** where the screen has room;
- maximum **60%** width and **90%** height, also capped to the working area
  minus 12 DIP;
- the old 630 × 900 cap is removed.

`LunaWindow.InitializeChatSize()` computes this in `OnOpened` using
`Screen.WorkingArea / RenderScaling`. The resize grip clamps to the same rules.
The last size is saved as `WxH` in
`%LOCALAPPDATA%\OpControlParental\luna-chat-size.txt` on every resize and read
again on the next start (clamped to the current screen). `MorphRect` is
unchanged in shape; it now receives the larger target size, and tests assert
the start (moon), the 55% stage and the final rect at 960 × 832, 683 × 600 and
the 100/150/200% defaults.

A note on the absolute minimum: on a hypothetical 1366 × 768 screen at **200%**
the working area is only about 683 × 364 DIP, so a 520 × 600 minimum cannot fit.
The implementation uses the maximum that fits instead of overflowing; the
normal 1920 × 1080 cases at 100/150/200% and 1366 × 768 at 100% meet the stated
minimums.

## Bubbles and contrast

- `BubbleColumns()` gives Luna’s bubbles and the child’s bubbles a star column
  of **88%** of the chat column (Luna’s face column is outside it). Short
  messages still size to their content. Pictures and display math stay centred
  and keep their 300 px/120 px limits; tables use the full bubble width.
- Child bubbles now use the subject’s design `Ink` (Night `#15103a`,
  Gray `#23273a`) on the subject `Tile`. `LunaColor.ContrastRatio` implements
  WCAG contrast and the tests check **all six subject colours in both themes**;
  the lowest is Night math at 5.69:1, above 4.5:1. The flagged bubble keeps its
  own dark background/light text.

## Tests

`tests/OpControl.Parental.Tests/Job188ChatTests.cs` (21 cases):

1. default size for 1920 × 1040, 1280 × 693, 960 × 520, 1366 × 728,
   911 × 485 and 683 × 364 DIP work areas, including the 100/150/200% scaling
   of the same 1920-class screen;
2. remembered size clamping and `WxH` parse/format round-trip;
3. `LunaWindow` integration at the headless 1920 × 1280 screen → 960 × 1024;
4. morph start/55%/final at the new sizes;
5. every subject colour in both themes ≥ 4.5:1 and the `style.Ink` source
   check;
6. bubbles wider than the old caps and ≤ 88% of the chat column;
7. opening starts at the latest message; sending keeps the child message and
   thinking visible at the bottom;
8. a short reply stays at the bottom;
9. a reply taller than the view keeps its first line within 0–40 px of the top
   and is not scrolled to the bottom;
10. a child who scrolled up keeps the same offset, gets the pill, and the pill
    click returns to the bottom.

Headless tests are in the non-parallel `HeadlessUiCollection`.

## Screenshots

`/srv/opcontrol-parental/docs/job-18.8-screens/index.html` and the repo copy
`docs/job-18.8-screens/`:

- `before-1920x1080-{night,gray}.png` — v0.17.6 at 520 × 600 on a 1920 × 1080
  screen (1040 DIP working height);
- `after-1920x1080-{night,gray}.png` — v0.17.7 at 960 × 832 (50% × 80%);
- `before-1366x768-{night,gray}.png` — v0.17.6 at 520 × 600;
- `after-1366x768-{night,gray}.png` — v0.17.7 at 683 × 600.

They use the real Avalonia app, theme resources and fonts with the exact
production Math chat, rendered through headless Skia at 100%. As before, the
transparent Luna window cannot be captured by DWM/GDI; the screenshots are the
real renderer, not an on-screen capture. The scroll behaviour and pill are
covered by the headless tests for the same reason.

## Release v0.17.7 (X1 only)

- Version bump `3d1e4b4`; `Directory.Build.props` **0.17.7**; release notes
  `.github/release-notes/v0.17.7.{en,es}.md`; annotated tag **v0.17.7**.
- Workflow **40364912306** success; release published 17:11:23 UTC.
- Web image `ghcr.io/ziur-vela/parental-opcontrol@sha256:77c74d2a850d13aa7bd9206b9d67e0f28b9934e14e254adda525e3940e013cb2`
- Updater image `...-updater@sha256:e00ea9f5592e561d486dad871db1ebd581288f432f14bfbb18b726671f251166`
- MSI `OpControlParental-0.17.7.msi` **143,884,288** bytes SHA-256
  `dcc9e95c3fd6684f375067e1e31146e2dbb69a7256f17806fe75c5b025197096`.
- Setup `OpControlParental-Setup-0.17.7.exe` **261,174,098** bytes SHA-256
  `11cd4f5eb074b9cbd7de1d67d753275f0387cde95a0cd25e3839c1f60848e717`.

### Staging

- Updater success 17:12:04 UTC; backup
  `/data/backups/parental-pre-update-20261026T171204Z.sql`.
- `/version`: `v0.17.7 · build 2026-10-26 · 3d1e4b4`; `/health` **200**;
  `/luna` **302**. Web/updater containers on the new digests.
- `.env` pinned; backup `.env.bak-job188-20261026T171257Z`;
  `docker compose config -q` OK.

### Production

- Manual pre-update backup:
  `/opt/stacks/opcontrol-parental/backups/parental-pre-job188-20261026T171305Z.sql.gz`
  (**5.7 MB**, `gzip -t` OK, contains `PostgreSQL database dump complete`,
  54 `CREATE TABLE` statements), plus
  `parental-keys-pre-job188-20261026T171305Z.tgz` and
  `docker-compose.yml.bak-job188-20261026T171305Z`.
- Updater success 17:13:13 UTC; its own backup
  `/data/backups/parental-pre-update-20261026T171313Z.sql`.
- `/version` public and local: **v0.17.7 · build 2026-10-26 · 3d1e4b4**;
  `/health` **200**; `/luna` **302**. Web/updater on the new digests;
  `.env` pinned (`.env.bak-job188-20261026T171343Z`); `docker compose config -q`
  OK.
- The Job 18.3 hold mount is still present:
  `./data/release-assets-hold:/app/release-assets:ro`; active
  `data/releases` still has **no 0.17.x** (count 0), so the Yoga cannot
  auto-update.
- Luna unchanged: `LunaFamilySettings` 1 row (Setup A, openrouter, protected
  key length **219**, last test OK); `ChildLunaSettings.Enabled = true`, theme
  `night`; `LunaChats` = 2. The key was not re-entered or written. Owner was
  not logged in and was not reset.

### X1 (only)

- Downloaded `OpControlParental-0.17.7.msi` to `C:\Temp\Job188`; SHA-256
  matched `dcc9e95c…`; `msiexec /i … /qn /norestart /l*v` exited **0** and the
  log says `Installation success or error status: 0`.
- `C:\ProgramData\OpControlParental\config.json` is unchanged.
- Service `OpControlParental`: **Running**, Automatic. Client, Helper and Luna
  file versions **0.17.7.0**. Session 1: Client, Helper `--monitored "Emma"`
  and **one** Luna moon (PID 17428, no `--kid-window`).
- No new `.NET Runtime`/Application Error/WER entry in the last 10 minutes.
- Dashboard: **ZIUR-X1-LAPTOP · 0.17.7 · online** (LastSeen 17:17:05 UTC);
  device `89f9db76-3e1c-458a-8053-44cf26df451e` not revoked. No lock was
  triggered by this job.
- `C:\Temp\Job188` was removed after verification.

### Yoga — not touched

The Yoga (**EMMA-YOGALAPTOP**) is still **0.16.1**, `LastSeenUtc`
2026-10-26 03:22:00 UTC and `LastUpdateCheckUtc` 01:52:53 UTC — both before this
job started. No Job 18.8 binary, installer or setting was sent to the Yoga.

## Limits / notes

- The headless `DispatcherTimer` does not tick in the test harness, so the
  reveal tests drive the extracted `AdvanceStreaming()` method directly; the
  production `DispatcherTimer` calls the same method every 28 ms.
- Screenshots are headless Skia renders of the real app because DWM/GDI cannot
  capture the transparent Luna window (the Job 18.4 limitation).
- The 1366 × 768 @ 200% case is smaller than the 520 × 600 minimum by
  definition (its working area is about 683 × 364 DIP); the window uses the
  maximum that fits instead of overflowing. The 1920-class at 100/150/200% and
  1366 × 768 at 100% meet the minimums.
