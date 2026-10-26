# Job 19e — Luna's hover greeting as a comic speech bubble (v0.17.11)

**Status: complete and released.** v0.17.11 is on staging, production and **both**
laptops (X1 and Yoga). Luna and the Public AI chats category settings were **not
changed** by this job. No owner or child account was logged in.

- Branch: **`feature/luna-hover-bubble-0.17.11`** from `main` (`cd6afa2`, v0.17.10).
- Feature commit: **`9e0e280`** — “Job 19e: show Luna's hover greeting as a comic
  speech bubble”; `main` fast-forwarded.
- Release commit: **`b0b72c1`** — “Job 19e: release v0.17.11”; tag **v0.17.11**.
- Release workflow: **40410992359**, success; release published
  **2026-10-26 22:23:44 UTC**.
- Build host: **Dell XPS** (`Ziur-Dell-XPS`, `100.98.112.35`), SDK 10.0.402.
- Tests: **370/370 Linux SDK 10.0.402** and **370/370 Dell XPS**; full solution
  build succeeded with **0 errors** on both.
- No database migration.

## 1. The bubble

The fixed dark tooltip (`LUNA / Hi there! / Need help with homework? Click me.`)
is replaced by a self-sizing comic speech bubble drawn by a new
`LunaGreetingBubble` control (`src/OpControl.Parental.Luna/LunaGreetingBubble.cs`).

- **Placement:** to the right of the awake moon, vertically centred on Luna's eye
  line, with the tail pointing at her. The tail tip is 6 px past the moon edge.
- **Tail flip:** the placement helper measures the bubble and the screen work
  area. If the right side does not fit, it flips the bubble to the left with the
  tail pointing right; if neither side fits, it keeps the side with more room and
  clamps inside the screen. Vertical clamping also keeps it on screen.
- **Shape:** one continuous path for the rounded box and tail, so there is no seam
  line where the tail meets the box. 18 px corners, 4 px outline and a solid
  5 px-down shadow.
- **Colours:** follow the child's theme.
  - Night sky: fill `#fffdf3`, outline `#15103a`, shadow `#e8dcc6`, ink `#15103a`.
  - Moon gray: fill `#ffffff`, outline `#23273a`, shadow `#e2bd52`, ink `#23273a`.
- **Text:** first line Fredoka 700 24 px; second and continuation lines Nunito
  800 15 px. Padding 14 px top/bottom, 20 px right, 18 px left. The box width and
  height are measured from the real `FormattedText` metrics, so text never
  overflows. The second line breaks after the question mark:
  - EN: **"Hi, I'm Luna!"** / **"Got a question?"** / **"I'm all ears!"**
  - ES: **"¡Hola, soy Luna!"** / **"¿Tienes una pregunta?"** / **"¡Te escucho!"**
- **Animation:** pops in when Luna wakes (scale 0.3 → 1.08 → 0.97 → 1 with fade,
  300 ms, origin at the tail tip). It shrinks and fades away when Luna sleeps or
  the chat opens. When Windows reports reduced motion it uses a simple fade.
- **Input:** the window region now unions the real bubble bounds (the old fixed
  134..326 × 54..144 rectangle is gone). Clicking the bubble follows the same
  path as clicking Luna and opens the chat; hovering it keeps Luna awake.
- **Theme/language:** the bubble re-configures on the `ThemeChanged` event from
  the chat view and also checks the shared local theme file once a second, so it
  follows the theme picker, the tray theme toggle and the 1 s theme poll. Text
  uses the existing `Luna.Greeting.Line1/Line2` strings in both languages.

New localisation keys (both `en.json` and `es-MX.json`):
`Luna.Greeting.Line1`, `Luna.Greeting.Line2`.

## 2. Tests

New `tests/OpControl.Parental.Tests/Job19EGreetingTests.cs` (6 tests):

1. text fits at 100/125/150/200% scaling in both themes and both languages
   (line widths and heights stay inside the measured padding, and the control
   bounds match its computed size);
2. the second line breaks after `?` in English and Spanish;
3. the tail starts on the left of a roomy screen, flips to the right near the
   screen edge, stays inside a too-narrow screen, and clamps vertically;
4. Night sky and Moon gray colours match the approved values, including ink;
5. the reduced-motion switch returns the expected value;
6. the real `LunaWindow` shows the bubble after waking (opacity 1, scale 1,
   three lines), clicking the bubble enters `MorphOpen`, the bubble shrinks away,
   and the chat finishes opening.

Existing tests still pass: **370/370** on Linux and XPS. The first XPS run hit a
pre-existing timing flake in `Job14RemoteTests` (socket-close test); its isolated
rerun passed and the full rerun passed 370/370. Nothing in that test was changed.

## 3. Screenshots

`/srv/opcontrol-parental/docs/job-19e-screens/index.html` (also
`docs/job-19e-screens/` in the repo):

- `greeting-night-en-100.png` / `-200.png`
- `greeting-night-es-100.png` / `-200.png`
- `greeting-gray-en-100.png` / `-200.png`
- `greeting-gray-es-100.png` / `-200.png`

They are real headless Avalonia Skia renders of the actual `LunaWindow` after
waking with the chat closed, at 100% and 200% render scaling, with a matching
theme backdrop so the paper fill and hard shadow are visible.

## 4. Release v0.17.11

- `Directory.Build.props` **0.17.11**; notes
  `.github/release-notes/v0.17.11.{en,es}.md`; annotated tag **v0.17.11**.
- Workflow **40410992359** success; release published 22:23:44 UTC.
- Web image:
  `ghcr.io/ziur-vela/parental-opcontrol@sha256:7c2d2fd9d18918f412bdbdbfed6dbcf0a66b2918e95aeeea79756e355c797e6e`
- Updater image:
  `ghcr.io/ziur-vela/parental-opcontrol-updater@sha256:200f2b20a73ae329d5198c82f450bb412f6ba1b383488ee3d069753d2883b05a`
- MSI `OpControlParental-0.17.11.msi` **143,888,384** bytes SHA-256
  `21802216cf2c1544f542c8461f2374e4866cc13750cd84e88ec8674e99a01379`.
- Setup `OpControlParental-Setup-0.17.11.exe` **261,178,196** bytes SHA-256
  `ac16d8a9c5a8b8a79a13cbb906b52eee303f3dc6b43418d7da82667cff405d50`.

### Staging

- Update 0.17.10 → 0.17.11 success; backup
  `/data/backups/parental-pre-update-20261026T222507Z.sql`.
- `/version`: `v0.17.11 · build 2026-10-26 · b0b72c1`; `/health` **200**.
- Web container runs `7c2d2fd9d189` healthy; updater runs `200f2b20a73a`.
- `.env` pinned to the new digests; backup
  `.env.bak-job19e-20261026T222625Z`; `docker compose config -q` OK.

### Production

- Manual pre-update backup:
  `/opt/stacks/opcontrol-parental/backups/parental-pre-job19e-20261026T222638Z.sql.gz`
  (**5,923,750** bytes; `gzip -t` OK; `PostgreSQL database dump complete`),
  plus `docker-compose.yml.bak-job19e-20261026T222638Z` and
  `.env.bak-job19e-20261026T222638Z`.
- Updater backup:
  `/data/backups/parental-pre-update-20261026T222650Z.sql`.
- `/version`: **v0.17.11 · build 2026-10-26 · b0b72c1**; `/health` **200**.
- Release catalog has `OpControlParental-0.17.11.msi` and
  `OpControlParental-Setup-0.17.11.exe`; both SHA-256 values match the release
  assets above. 0.17.10 remains the previous release.
- `.env` pinned to the new digests; `docker compose config -q` OK.

### Both laptops (normal catalog)

- Requested through the dashboard per-device **Update Now** path (normal product
  catalog, no manual MSI):
  - **ZIUR-X1-LAPTOP** (Emma session): update log 22:32:07 → 22:32:43 UTC,
    `msiexec` exit 0, new service healthy.
  - **EMMA-YOGALAPTOP**: update log 22:32:16 → 22:32:46 UTC, `msiexec` exit 0,
    new service healthy.
- Both: client/helper/Luna **0.17.11.0**, service **Running/Automatic**, one Luna
  each, session 1, dashboard **online** on 0.17.11; same device rows:
  `89f9db76-3e1c-458a-8053-44cf26df451e` (X1) and
  `d3b94f63-678b-492e-83d6-19f3036b89f8` (Yoga), not revoked.
- X1 Luna started with `sleep_scale=1.6` (the default) and sleeps normally.
  Yoga kept sleep size **1.9** after the update
  (`C:\Users\Emma\AppData\Local\OpControlParental\luna-sleep-scale.txt`); its new
  Luna process shows the same 1.9 sleep scale.
- No lock was triggered. Emma `ManualLock=false`; `UnlockUntilUtc` remains
  `2026-10-27T07:00:00Z` (pre-existing). 0 active remote sessions.
- Luna settings unchanged: protected key length **219**, Setup A / OpenRouter /
  `anthropic/claude-haiku-5.5`, `LastTestOk=true`, child Luna enabled, theme
  **night**, daily limit **60**. No chats were created or changed.
- Category settings unchanged: the pre-update production backup and the live
  database both show the same rows for Emma — `adult`, `ai` (Public AI chats),
  `fakenews`, `gambling`, `social`, all enabled.

## 5. Cleanup

- Temporary production parent `job19e-admin@opcontrol.local` deleted; remaining
  `job19e%` users **0**; active remote sessions **0**.
- Temporary verification database `job19e_verify` created from the pre-update
  dump, compared and dropped.
- Local Playwright/TRMM read-only helper scripts and credentials removed.
- XPS copy `C:\ocp-build\parental-19e` and the transfer zip removed.
- No production owner/child login, no PIN/password/family/Windows setting change,
  no date of birth added, Tailscale untouched.

## Limits / notes

- The screenshots are renderer captures, not on-screen photos of the laptops.
- The bubble is only shown while Luna is awake; the sleeping sliver is unchanged.
- The window click region for the bubble uses its bounding rectangle, so the few
  transparent pixels at the rounded corners are also part of the clickable area.
  This matches the prior tooltip behaviour and does not affect the desktop outside
  the bubble.
