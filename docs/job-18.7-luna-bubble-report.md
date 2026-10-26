# Job 18.7 — Luna bubble wrapping, inline math and fraction pictures (v0.17.6, X1 only)

Status: **complete and released.** The fix is **v0.17.6** on staging,
production and the **X1 only**. v0.17.5 was already used by Job 18.6, so this
job took the next free tag; nothing was retagged. The Yoga was not updated and
the release hold is unchanged. Luna stayed enabled with the same key. No
owner/child login, no family, PIN, password, display-scaling or Windows setting
change.

- Branch: **`fix/luna-0.17.6`** from `main` (`1e9a2a8`, v0.17.5).
- Commit: **`7be20e0`** — “Job 18.7: wrap Luna bubbles and draw complete
  fraction pictures”; `main` fast-forwarded to it.
- Tag: **v0.17.6**; release workflow **40349207666** success; GitHub release
  published 2026-10-26 15:32:52 UTC.
- Build/test hosts: Linux SDK **10.0.402** and the **Dell XPS**
  (`Ziur-Dell-XPS`, `100.98.112.35`), same SDK.
- Tests: **317/317 Linux** and **317/317 XPS**. Windows solution build on the
  XPS: **0 errors, 0 warnings**.

## Cause of each problem

### 1. Lines did not wrap and a bubble had an inner scrollbar

`LunaMarkdown.Block` returned:

```csharp
new ScrollViewer {
    Content = renderer,
    HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
    VerticalScrollBarVisibility   = ScrollBarVisibility.Disabled
}
```

A horizontal `ScrollViewer` measures its child with an **infinite width**.
LiveMarkdown’s `MarkdownTextBlock` then laid each paragraph out on one long
line (its default `TextWrapping` is `NoWrap`), and the ScrollViewer clipped it
and offered a horizontal scrollbar. Measured in a headless probe, the 356 px
wide bubble content had a **1428 px** text extent. This affected every long
line; the lines with inline math were simply the longest visible examples
(“Tell me what you get, and we can check it together. Ther…”,
“Here is 2/6. The bar has 6 equal pieces, and 2 of them are s…”).

**Fix:** there is no horizontal ScrollViewer around the bubble body anymore.
The renderer is measured at the bubble width, and a bubble-local style sets
`TextWrapping = Wrap` on every `MarkdownTextBlock` it creates. No scrollbar can
appear inside a bubble; only the chat list scrolls. Tables (which LiveMarkdown
also puts in a horizontal scroller and measures with infinite width) get every
cell capped at the bubble column width, so they wrap and grow downwards; their
inner viewer is disabled. A headless test renders a 3-column table at 300 px
and asserts the extent never exceeds the viewport.

### 2. Inline math was tiny; display math was not emphasised

LiveMarkdown creates its `FormulaControl` with the default 14 px size, and the
size is a **local value**, so an application style cannot change it. A stacked
`\frac` is drawn at roughly 70% of that, hence the hard-to-read fractions.

**Fix:** after each layout pass `LunaMarkdown.Tune` sets the formula font size:
inline math **19 px** (a stacked fraction is then about the body’s line height
and the size of the surrounding words) and display math **24 px**, with its
presenter centred. The number of formulas per bubble is small, and values are
only written when they differ, so the pass is cheap and does not loop.

### 3. `[[bar 2/6]]` was one shaded block, the caption was small and left

`LunaPictures.Layout` only emitted an empty bar plus one filled bar overlay:
there were **no piece boundaries at all**, and `[[pie]]` emitted only the
shaded slices, so unshaded pieces had no dividers. The picture caption was a
left-aligned row using LiveMarkdown math at the default size (15 px answer
fraction).

**Fix:**

- `[[bar a/b]]` and every bar of `[[bars a/b + c/b]]`, including the answer
  bar, now get `bar-divider` shapes at every internal boundary (1.5 px wide,
  full height, subject shadow colour), drawn above the fills.
- `[[pie a/b]]` emits **all b slices**; shaded slices use the subject colour,
  unshaded slices use the sunken colour, and every slice has the subject
  outline.
- The picture block is centred. The caption is built from the known fraction
  terms as real stacked fractions at **20 px**, with `+` between terms and `=`
  plus the success-coloured answer for `bars`. The caption is centred directly
  under the drawing.
- The 300 px drawing is inside a `Viewbox` with `StretchDirection=DownOnly`, so
  it shrinks rather than overflowing when the bubble is only 300 px wide.

## Tests

New `tests/OpControl.Parental.Tests/Job187BubbleTests.cs`:

- Renders the **exact three Luna replies** from the production
  “Multiplying Fractions Together” chat (read-only `SELECT` on
  `LunaMessages`, chat `4f98b405-1511-45a3-844c-552f1bb32345`, 2026-10-26
  03:51–03:53 UTC), at bubble widths **300, 420 and 560 DIP** and render
  scaling **100%, 125%, 150% and 200%**, in **both themes** (night/gray).
- For every combination it asserts: the bubble is not wider than the requested
  width, no descendant starts outside or overflows the bubble (horizontally or
  vertically), there is **no inner `ScrollViewer`**, the bubble height fits all
  content, and inline formulas are the intended size. The picture reply must
  contain the 5 divider lines for 6 pieces.
- A display-math test asserts the 24 px size, the centred presenter and the
  formula’s centre matching the bubble centre within 1 px.
- A table test at 300 px asserts wrapping, disabled inner scrollbars and
  `Extent ≤ Viewport`.
- Layout tests assert **5 dividers** for `bar 2/6`, **15** for
  `bars 1/6 + 2/6` (three bars), **6 slices** for `pie 2/6` (2 shaded,
  4 empty) and **12 slices** for `pie 1/12`.
- `Job184MarkdownTests` was updated from “there is a horizontal scroller” to
  “there is no scroller and the text blocks wrap”; `Job184MathTests` now
  expects the divider shape and all pie slices.
- Headless Avalonia tests from different classes are placed in a
  non-parallel xUnit collection (`HeadlessUiCollection`), because they share
  one in-process application.

## Screenshots

`/srv/opcontrol-parental/docs/job-18.7-screens/index.html` and the repo copy
`docs/job-18.7-screens/`:

- `before-chat-{night,gray}-150.png` — v0.17.5 at 150%, the exact chat with
  clipped lines, tiny math, a single-block bar and a small left caption;
- `after-chat-{night,gray}-150.png` — the fixed build, same chat and scales;
- `before-pictures-*` / `after-pictures-*` — `bar 2/6`, `bars 1/4 + 2/4` and
  `pie 2/6` in both themes at 150%.

They are rendered with the real Avalonia application (theme resources, bundled
fonts and the real `LunaRichContent`/`LunaPictureView`) at 150% render scaling.
As in Job 18.4, Windows DWM does not expose the transparent Luna window to
GDI/DWM capture, so the images are the real renderer in a headless Skia window
rather than a screenshot of the on-screen transparent window.

## Release v0.17.6 (X1 only)

- Version bump `7be20e0`; `Directory.Build.props` **0.17.6**; release notes
  `.github/release-notes/v0.17.6.{en,es}.md`; annotated tag **v0.17.6**.
- Workflow **40349207666** success; release published 15:32:52 UTC.
- Web image `ghcr.io/ziur-vela/parental-opcontrol@sha256:5a008e13d278c22cdd29ac965dc42a0f78533ce27bced1a84266b4c0cf9af639`
- Updater image `...-updater@sha256:75fc204eeb6a5ab11613d912652e75ad28b47f30e32b90b13eec5f0b9f3bb899`
- MSI `OpControlParental-0.17.6.msi` **143,831,040** bytes SHA-256
  `15ef27196cf31a83ecabaf32e4d4edb86342c3c6addc6b5806293b7f347adb85`.
- Setup `OpControlParental-Setup-0.17.6.exe` **261,120,850** bytes SHA-256
  `7698be9c24c462d8ff40e2cb9124a9ffd9afc6803ddd84cf8b2877a3c649e56e`.

### Staging

- Updater success 15:34:05 UTC; backup
  `/data/backups/parental-pre-update-20261026T153329Z.sql`.
- `/version`: `v0.17.6 · build 2026-10-26 · 7be20e0`; `/health` **200**;
  `/luna` **302**. Web and updater containers on the new digests.
- `.env` pinned to both new digests; backup
  `.env.bak-job187-20261026T154806Z`; `docker compose config -q` OK.
- No new migration; 20 migration rows.

### Production

- Manual pre-update backup:
  `/opt/stacks/opcontrol-parental/backups/parental-pre-job187-20261026T154819Z.sql.gz`
  (**5.7 MB**, `gzip -t` OK, contains `PostgreSQL database dump complete`,
  54 `CREATE TABLE` statements), plus
  `parental-keys-pre-job187-20261026T154819Z.tgz` and
  `docker-compose.yml.bak-job187-20261026T154819Z`.
- Updater success 15:48:41 UTC; its own backup
  `/data/backups/parental-pre-update-20261026T154830Z.sql`.
- `/version` public and local: **v0.17.6 · build 2026-10-26 · 7be20e0**;
  `/health` **200**; `/luna` **302**. Web/updater containers on the new
  digests; `.env` pinned (`.env.bak-job187-20261026T154911Z`);
  `docker compose config -q` OK.
- The Job 18.3 hold mount is still present:
  `./data/release-assets-hold:/app/release-assets:ro` in
  `/opt/stacks/opcontrol-parental/docker-compose.yml`.
- Active `data/releases` still contains **no 0.17.x** (grep count 0), so the
  device catalog cannot offer 0.17.6 and the Yoga cannot auto-update.
- Luna unchanged: `LunaFamilySettings` 1 row (Setup A, openrouter, protected
  key length **219**, last test OK); `ChildLunaSettings.Enabled = true`, theme
  `night`; `LunaChats` = 2. The key was not re-entered or written. Owner was
  not logged in and was not reset.

### X1 (only)

- Downloaded `OpControlParental-0.17.6.msi` to `C:\Temp\Job187`; SHA-256
  matched `15ef2719…`; `msiexec /i … /qn /norestart /l*v` exited **0** and the
  log says `Installation success or error status: 0`.
- `C:\ProgramData\OpControlParental\config.json` is unchanged
  (`https://parental.opcontrol.net`, OsAccount/MonitoredAccount Emma, same
  HelperPath).
- Service `OpControlParental`: **Running**, Automatic. Client, Helper and Luna
  file versions **0.17.6.0**. Session 1: Client, Helper `--monitored "Emma"`
  and **one** Luna moon (`…\Luna\OpControl.Parental.Luna.exe`, no
  `--kid-window`).
- Helper log after the install: `helper-started session=1 user=Emma` and
  `countdown-shown`; `luna.log` is a healthy sleeping moon. No new `.NET
  Runtime`/Application Error/WER entry in the last 20 minutes.
- Dashboard: **ZIUR-X1-LAPTOP · 0.17.6 · online** (LastSeen 15:53:07 UTC).
  Device `89f9db76-3e1c-458a-8053-44cf26df451e` not revoked. No lock was
  triggered by this job.
- `C:\Temp\Job187` was removed after verification.

### Yoga — not touched

The Yoga (**EMMA-YOGALAPTOP**) is still **0.16.1**, `LastSeenUtc`
2026-10-26 03:22:00 UTC and `LastUpdateCheckUtc` 2026-10-26 01:52:53 UTC — both before
this job started. The active release catalog has no 0.17.x and the production
hold mount is still in place. **No Job 18.7 binary, installer or setting was
sent to the Yoga.**

## Limits / notes

- The tag is **v0.17.6**, not v0.17.5 as written in the job prompt, because
  Job 18.6 already released v0.17.5; reusing a tag would not be possible.
- The screenshots are headless Skia renders of the real app renderer, not
  DWM captures of the transparent window (the Job 18.4 limitation).
- 125% and 200% were exercised in the headless layout tests, not physically on
  the X1; the X1 is 150%.
