# Job 20c — About polish, sidebar button and AI credits (v0.17.18)

**Status: complete and released.** v0.17.18 is on staging, production, the X1
and the Yoga.

- Branch: **`fix/job20c-about-polish-0.17.18`** from `main` (`ae79fb1`).
- Feature commit: **`827a24b`** — “Job 20c: polish About and credit AI
  assistance”. `main` fast-forwarded to the same commit; tag **v0.17.18**.
- Release workflow: **40596367892**, success; release published
  **2026-10-27 22:46:52 UTC**.
- Tests: **429/429 Linux SDK 10.0.402** and **429/429 Dell XPS**.
- Build host: **Dell XPS** (`Ziur-Dell-XPS`, `100.98.112.35`), SDK 10.0.402.
- No database migration.

## 1. Kid view polish

### Message my parents button

The button is now horizontally centered in the sidebar (it no longer stretches
the full width) and has a 16 px bottom margin. With the sidebar stack's existing
8 px spacing that is 24 px between the button and the device name — 16 px more
than before — with the About link below the device line. The change is layout
only, so it is identical in night and gray and at every preview/window scale.

### About header card

The Parental OpControl shield and “My OpControl / Created by Ziur · OpControl
IT” stay on the left. The right side of the same card now has the **OpControl IT
lockup**, vertically centered, 40 px tall and 183.3 px wide (the asset's exact
440×96 ratio).

The asset was copied byte-for-byte from the parent dashboard's footer logo
(`src/OpControl.Parental.Server/wwwroot/brand/opcontrol-logo.png`, SHA-256
`b6403566541856849546afb86da75f20381a10339ef6f0a86b20723c655b1bb0`) to
`src/OpControl.Parental.Luna/Assets/opcontrol-logo.png` and embedded in Luna.
It is not redrawn or invented. The 1×1 headless `Bitmap.PixelSize` stub is
avoided by reading the PNG IHDR dimensions, so the aspect ratio is correct in
the real renderer and in tests.

### Server version `vv` bug

`AboutInfoService` now strips a leading `v` from the server `/version` value, and
the About render also normalises before adding its own `v`. About shows
**v0.17.18**, not `vv0.17.18`.

The dashboard was checked with a temporary parent session on production:
`/` and `/downloads` show a single `v0.17.18`. The only `vv` matches are random
base64 inside inline scripts, not version text. The dashboard version pills use
`Build.DisplayVersion` / `Build.Version` (already single-v) and Downloads adds
`v` to the release filename version (no prefix), so no dashboard change was
needed.

### Sidebar language refresh

While checking the Spanish screenshots, the sidebar/title bar stayed in English
when the snapshot language changed, because they were built before
`Loc.SetLanguage`. `LoadAsync` now rebuilds the root once when the language
changes, so the sidebar (Home/Inicio, About/Acerca de, etc.) matches the About
page in es-MX.

## 2. Built with AI credits

### About page

A **Built with AI** section now sits in the credits card, above the third-party
list, in both languages. The product names are bold:

- English: “Parental OpControl was designed, directed and tested by Ziur, and
  built with AI assistance: design, planning and code review with **Claude**
  (by Anthropic), and code written by **OC**, an AI coding agent built on
  **OpenCode**, running **DeepSeek** models. Every change was reviewed and
  tested by a person before release.”
- Spanish: “Parental OpControl fue diseñado, dirigido y probado por Ziur, y
  construido con ayuda de IA: diseño, planeación y revisión de código con
  **Claude** (de Anthropic), y código escrito por **OC**, un agente de
  programación con IA basado en **OpenCode**, usando modelos de **DeepSeek**.
  Cada cambio fue revisado y probado por una persona antes de publicarse.”

No AI product logos were added.

### Dashboard footer

`MainLayout.razor` now has a small line under the existing “Created by
OpControl” footer:

- EN: “Built with AI assistance: Claude, OpenCode, DeepSeek”
- ES: “Construido con ayuda de IA: Claude, OpenCode, DeepSeek”

Verified live on production as a temporary parent: the line is present in both
languages on `/`.

### README

`README.md` has a new **Credits** section and `README.es.md` a **Créditos**
section, both immediately before License/Licencia. They carry the same AI
paragraph and the Created by / third-party pointer.

Job 20a's README changes were not merged when this job ran (`main` had no
Credits section). The section was added to the current READMEs; if Job 20a
rewrites the same area, its branch should keep this section or merge it.

### Tests

New `Job20CAboutPolishTests` (7 cases):

- About shows `v0.17.18` once with a server value that already starts with `v`;
- the AI credits are present in English and es-MX, including the bold product
  names, and the sidebar labels are localized;
- the AI section is above the third-party list in the source/order;
- the Message my parents button is centered and has at least a 16 px bottom
  margin;
- the header OpControl logo loads, is 40 px tall and has the source 440×96
  aspect ratio;
- the dashboard footer AI line exists in both resx languages.

The XPS full run initially hit the pre-existing `Job14RemoteTests` race
(150 ms watchdog timeout vs the test's 100 ms delay). The test now uses a 5 s
heartbeat for the Stop test and keeps 150 ms only for the heartbeat test;
the suite then passed 429/429.

## 3. Screenshots

`/srv/opcontrol-parental/docs/job-20c-screens/` and repo
`docs/job-20c-screens/`:

- `home-night.png`, `home-gray.png` — sidebar with the centered/raised
  Message my parents button, device name and About link.
- `about-night.png`, `about-gray.png` — About with the header lockup and the
  Built with AI section.
- `about-night-es.png`, `about-gray-es.png` — the same in es-MX.
- `index.html`.

Captured on the XPS Debug preview, 1040×700, demo child Ana / X1 laptop, build
date 2026-10-27. No real data.

## 4. Release v0.17.18

- `Directory.Build.props` **0.17.18**; notes
  `.github/release-notes/v0.17.18.{en,es}.md`; annotated tag **v0.17.18**.
- Workflow **40596367892** success; published **22:46:52 UTC**.
- Web image:
  `ghcr.io/ziur-vela/parental-opcontrol@sha256:2c2d3f21a8dcab303b1b738eda5deb44081c1a1d1d27e044b9c33e9798fffc42`
- Updater image:
  `ghcr.io/ziur-vela/parental-opcontrol-updater@sha256:67ad1145efcb6309af5ebf4cae70c0e23536a1dada03c36b4f220a91770e1eb2`
- MSI `OpControlParental-0.17.18.msi` **143,917,056** bytes SHA-256
  `9c52978fee133013cf4b639d84420a70e41fbb627ce67537c7d92bbbb5abde59`.
- Setup `OpControlParental-Setup-0.17.18.exe` **261,210,964** bytes SHA-256
  `ef423b957077d71c490acd6737ca94b3cf3df4566dab39f9419e52290df20295`.
- Production catalog `data/releases` has both files with those exact hashes.

### Staging

- 0.17.17 → 0.17.18 success at **22:48:31 UTC**; updater backup
  `/opt/stacks/opcontrol-parental-staging/data/updater/backups/parental-pre-update-20261027T224753Z.sql`
  (12,313,184 bytes, dump complete = 1).
- `/version` **v0.17.18 · build 2026-10-27 · 827a24b**; `/health` **200**.
- `.env` pinned to the new digests (`.env.bak-job20c-pin-20261027T224902Z`);
  compose `.bak-job20c-20261027T224752Z`; `docker compose config -q` OK.

### Production

- Manual pre-update backup:
  `/opt/stacks/opcontrol-parental/backups/parental-pre-job20c-20261027T224908Z.sql.gz`
  (**6,116,674** bytes; gzip OK; dump complete = 1), plus
  `parental-keys-pre-job20c-20261027T224908Z.tgz` (714 bytes),
  `docker-compose.yml.bak-job20c-20261027T224908Z` and
  `.env.bak-job20c-20261027T224908Z`.
- Updater backup
  `/opt/stacks/opcontrol-parental/data/updater/backups/parental-pre-update-20261027T224916Z.sql`
  (15,759,633 bytes; dump complete = 1).
- 0.17.17 → 0.17.18 success at **22:49:27 UTC**; `/version`
  **v0.17.18 · build 2026-10-27 · 827a24b**; `/health` **200**; `/luna`
  **302**; migrations **22** (no new migration).
- `.env` pinned to the new digests
  (`.env.bak-job20c-pin-20261027T224953Z`); `docker compose config -q` OK.
- Settings unchanged: categories `adult`, `ai`, `fakenews`, `gambling`, `social`
  all true; manual lock false; 0 active remote sessions; 1 user (the owner,
  untouched). Luna chats are 4 (2 more than Job 20b; this job created none).

## 5. Laptops

Both used the normal catalog **Update Now** through a temporary parent
(`job20c-admin@opcontrol.local`, Permissions 511, no 2FA), deleted immediately
after the requests; 0 `job20c-%` users remain.

| Device | Before | After | Update | Result |
|---|---|---|---|---|
| ZIUR-X1-LAPTOP | 0.17.17 | **0.17.18** | 22:52:05 → 22:52:29 UTC, `msiexec` 0 | healthy, service Running/Auto, Client/Helper/Luna 0.17.18.0, one Luna |
| EMMA-YOGALAPTOP | 0.17.17 | **0.17.18** | 22:52:11 → 22:52:27 UTC, `msiexec` 0 | healthy, service Running/Auto, Client/Helper/Luna 0.17.18.0, one Luna |

Both dashboard rows read **0.17.18 healthy**. The installed legal folder from
Job 20b is still present on both laptops (`legal/LICENSE`,
`legal/THIRD-PARTY-NOTICES.md`, `legal/fonts/OFL-*.txt`).

## 6. What Ziur should check by hand

1. Sidebar: Message my parents is centered and a little higher; the device name
   and About are under it, with no clipping at 1366×768 / 150%.
2. About in night and gray: the OpControl lockup is on the right of the top card,
   the server line reads v0.17.18 (one v), and Built with AI is above the
   third-party list.
3. es-MX: the sidebar, About and Built with AI section are in Spanish.
4. Dashboard: the footer has “Built with AI assistance: Claude, OpenCode,
   DeepSeek” (or the Spanish line).

## 7. Cleanup / notes

- Production temporary parent deleted; 0 `job20c-%` users; no temp family,
  device, chat, rule or alert was created.
- XPS `C:\ocp-build\parental-20c`, `parental-20c.tgz`, `screens-20c`,
  `shots20c.ps1`, `job20c-test.log` and the `Job20CShots` scheduled task were
  removed.
- Local transfer archives/scripts were removed.
- No owner or child login; no PIN, password, family, scaling, Windows or Luna
  setting was changed; OC’s git identity was not changed.

## Limits

- The dashboard footer was verified from the live production HTML as a
  temporary parent; it was not screenshotted in a browser.
- Screenshots are from the XPS Debug preview, not the installed laptops.
- The release MSI/setup are unsigned (unchanged from previous releases).
