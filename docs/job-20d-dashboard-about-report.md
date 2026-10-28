# Job 20d — dashboard About page and clearer AI credits (v0.17.19)

**Status: complete and released.** v0.17.19 is on staging, production and the X1.
The Yoga was offline before the update request; it is queued to update when it
comes back online.

- Branch: **`main`** (started from `main` at `9552308`, the v0.17.18 docs
  commit).
- Commits:
  - `7042796` — “Job 20d: add the dashboard About page and clarify AI
    credits” (feature).
  - `5db5ef9` — “Job 20d: fix the credits generator on Windows and the
    last-seen spacing”.
  - `e7589ab` — “Job 20d: isolate the credits generator build for parallel
    projects”. **Tag `v0.17.19`.**
- Release workflow: **40637346253**, success; published
  **2026-10-28 05:26:58 UTC**.
- Tests: **442/442 Linux SDK 10.0.402** and **442/442 Dell XPS**.
- Build host for Windows: **Dell XPS** (`Ziur-Dell-XPS`,
  `100.98.112.35`), SDK 10.0.402.
- No database migration (22 migrations before and after).

## 1. Dashboard `/about`

- New page `src/OpControl.Parental.Server/Components/Pages/About.razor` with
  `@page "/about"`, `[Authorize]` (every authenticated parent role) and the
  interactive server render mode. The visible content is
  `Components/Shared/AboutPanel.razor`, which is also what the render tests
  exercise.
- **Header card:** the Parental OpControl shield (`/brand/icon-color.svg`) and
  the product name on the left, the OpControl IT lockup
  (`/brand/opcontrol-logo.png`, the same asset as the footer, exact 440×96
  ratio) on the right.
- **Author line, only here:** “Created by **[the real name]** · OpControl IT Solutions” /
  “Creado por **[the real name]** · OpControl IT Solutions”. It is
  hardcoded through `AboutPage.AuthorLine` in both resx files, not a setting,
  and it is the only place in the product where that name is shown.
- **Version card:** server `Build.DisplayVersion` (version, build date and
  short commit), a **Release notes on GitHub** link to the matching tag, and
  every non-revoked device in the family with its reported client version and
  last-seen date.
- **License card:** PolyForm Noncommercial License 1.0.0, the short summary,
  the exact English/Spanish copyright sentence requested, and a
  **Read the full license** disclosure that shows the repository `LICENSE`
  text inline (same reader as `/license`).
- **Credits card:** the new **Built with AI** wording (bold product names) and
  the generated third-party list in a scrollable list, above the full license
  details.
- **Sidebar footer:** the version pill, **Version details** and the short
  **© 2026 Ziur · License** line now open `/about`. The old product tagline,
  copyright sentence and license name were removed from the footer and moved
  to `/about`. `/license` and `/third-party-notices` still work.
- Both themes and phone width use the existing dashboard variables and layout;
  new styles are at the end of `parental.css` with a 620 px breakpoint.

## 2. Generated third-party components

- `tools/generate-third-party-credits.cs` now:
  - accepts optional extra dependency graphs (the first is required);
  - reads `frameworkReferences` and adds **ASP.NET Core** and **.NET Runtime**
    as framework components;
  - scans the Luna fonts as well as the helper/setup fonts, including the
    dashboard webfonts, so IBM Plex Mono, Manrope, Fredoka and Nunito appear.
- `OpControl.Parental.Server.csproj` generates a combined list from the server
  graph plus the kid view graph (when the Luna assets file exists) and embeds
  it as `OpControl.Parental.Server.ThirdPartyCredits.json`.
- `AboutCredits` reads that generated resource and appends the PostgreSQL
  server (17.6, PostgreSQL license) because it is deployed with the stack but
  is not a NuGet package. Web Push is implemented in-house
  (`WebPushCrypto`/`PushService`), so there is no third-party Web Push library
  to list.
- The dashboard list contains 66 components, including ASP.NET Core, .NET
  Runtime, EF Core, Npgsql, QRCoder, Avalonia, LiveMarkdown.Avalonia,
  PostgreSQL (server) and the four bundled font families. No component has an
  unknown license.
- The Dockerfile now copies `tools/` and restores the Luna project so the
  published image has the combined list. The generator runs in a per-project
  data/temp home, so the Luna and Server targets no longer collide when
  MSBuild builds projects in parallel.

## 3. Kid view, footer and READMEs

- `Client.Kid.About.BuiltWithAi` (en.json and es-MX.json) and the dashboard
  footer (`Layout.BuiltWithAi`) use the new wording, with **Claude**,
  **OpenCode** and **DeepSeek V4.1 Flash** in bold in the UI.
- `README.md` and `README.es.md` have the same paragraph with **Ziur** (not
  the real name).
- The dashboard footer renders the bold markers properly; it is no longer
  plain `**Claude**` text.
- The obsolete “OC” wording was removed. The public product strings, current
  READMEs and release notes no longer contain the internal agent name. The
  v0.17.18 release notes were reworded for the same reason.

## 4. Tests

New `Job20DAboutPageTests` (13 cases):

- `/about` renders for owner and co-parent roles, with full and minimal
  permissions, in English and es-MX, and shows the header, author line,
  version, license and credits.
- the real name appears **exactly once** on the rendered page, and a source
  scan proves it only exists in the two `AboutPage.AuthorLine` resx values
  (the README and every other page/resource are clean);
- no public text contains the internal name “OC” as a standalone token;
- the generated credits include ASP.NET Core, .NET Runtime, EF Core, Npgsql,
  Avalonia, LiveMarkdown, PostgreSQL (server) and all four fonts, with known
  licenses;
- the sidebar version/license links open `/about` and the old tagline is gone;
- the page is authorized and has the responsive About/credit styles;
- the new AI wording is present in both resx files, both READMEs and both
  localization JSON files.

Updated `Job20CAboutPolishTests` for the new footer wording and
`LocalizationTests` for the two intentionally identical About labels.

Full runs:

- Linux: `Passed! Failed 0, Passed 442, Skipped 0` (Release).
- A clean `git clone --branch v0.17.19` of the tag also passed 442/442 on Linux,
  which caught the original parallel-generator collision.
- Dell XPS, clean `bin`/`obj`: `Passed! Failed 0, Passed 442, Skipped 0`
  (Release).

### First release attempt

The first v0.17.19 workflow (**40636996436**) failed in the Linux test job
because the Luna and Server credit targets ran `dotnet run --file` in parallel
and collided on the same generated script build. `e7589ab` gives each project
its own data/temp home; the tag was moved to that commit and the final workflow
passed. No image or release was published from the failed run.

## 5. Release v0.17.19

- `Directory.Build.props` **0.17.19**; release notes
  `.github/release-notes/v0.17.19.{en,es}.md`; annotated tag **v0.17.19** on
  `e7589ab`; `main` pushed.
- Web image:
  `ghcr.io/ziur-vela/parental-opcontrol@sha256:f2a063483c7bd3137e9b5024b051c74114a8d23f2f584c60671daab86e5c1456`
- Updater image:
  `ghcr.io/ziur-vela/parental-opcontrol-updater@sha256:c5f919d5c0be6009d4b752a948f8f7cbdcfe449c40255cfc81850b425ecc3193`
- MSI `OpControlParental-0.17.19.msi` **143,953,920** bytes SHA-256
  `9ce81b7db205728fb3b3f42e23f406db935912438096c69924f307ce41c2c427`.
- Setup `OpControlParental-Setup-0.17.19.exe` **261,247,828** bytes SHA-256
  `5b799ac2eb7edc628b15c0a5f5e7193088deff69197fb34854b960bc70b43eb4`.
- Production catalog `data/releases` has both files.

### Staging

- Manual pre-update backup:
  `/opt/stacks/opcontrol-parental-staging/backups/parental-pre-job20d-20261028T051639Z.sql.gz`
  (5,518,876 bytes; gzip OK; dump complete = 1).
- Updater backup:
  `/opt/stacks/opcontrol-parental-staging/data/updater/backups/parental-pre-update-20261028T052929Z.sql`
  (12,313,547 bytes).
- 0.17.18 → 0.17.19 success at **05:30:15 UTC**; `/version`
  **v0.17.19 · build 2026-10-28 · e7589ab**; `/health` **200**.
- `.env` pinned to the new digests (backup `.env.bak-job20d-20261028T051639Z`);
  `docker compose config -q` OK.
- Verified with temporary parents on the staging family:
  - a limited co-parent (Permissions 16) and a full parent (511);
  - English and es-MX;
  - the page showed the real name exactly once, the license, the generated
    components (EF Core, ASP.NET Core, SIL OFL 1.1) and the localized footer.
  Both temporary users were deleted (0 `job20d-%` users remain).

### Production

- Manual pre-update backups:
  - `/opt/stacks/opcontrol-parental/backups/parental-pre-job20d-20261028T051639Z.sql.gz`
    (6,165,303 bytes; gzip OK; dump complete = 1);
  - `parental-keys-pre-job20d-20261028T051639Z.tgz` (714 bytes);
  - `docker-compose.yml.bak-job20d-20261028T051639Z` and
    `.env.bak-job20d-20261028T051639Z`.
- Updater backup:
  `/opt/stacks/opcontrol-parental/data/updater/backups/parental-pre-update-20261028T053155Z.sql`
  (16,056,072 bytes).
- 0.17.18 → 0.17.19 success at **05:32:06 UTC**; `/version`
  **v0.17.19 · build 2026-10-28 · e7589ab**; `/health` **200**; `/luna`
  **302**; migrations **22** (no new migration).
- `.env` pinned to the new digests (backup `.env.bak-job20d-20261028T051639Z`);
  `docker compose config -q` OK.
- Production `/about` verified with a temporary full-permission parent:
  version v0.17.19, the author line exactly once, the license and copyright,
  the new AI wording, EF Core, ASP.NET Core and the font licenses. The
  temporary parent was deleted (1 user remains, the owner, untouched).
- Settings unchanged: manual lock false; 0 active remote sessions; 1 family,
  1 child, 2 devices; Luna chats 5 (none created by this job).

## 6. Laptops

| Device | Before | After | Result |
|---|---|---|---|
| ZIUR-X1-LAPTOP | 0.17.18 | **0.17.19** | requested 05:34 UTC, healthy 05:36 UTC; Client/Helper/Luna 0.17.19.0; service Running/Automatic; legal folder intact |
| EMMA-YOGALAPTOP | 0.17.18 | **0.17.19 (queued)** | offline since 05:31 UTC (Tailscale and TRMM agent); update requested at 05:34 UTC, still queued at the end of the job |

Yoga detail: it was already offline before the request (last seen
2026-10-28 05:31:28 UTC in TRMM and 05:31:59 UTC in the dashboard). The normal
catalog request is stored (`UpdateStatus=requested`), so it will install
0.17.19 the next time the laptop is online. The legal folder from Job 20b is
still present on the X1; the Yoga could not be checked.

## 7. Screenshots

`/srv/opcontrol-parental/docs/job-20d-screens/` and repo
`docs/job-20d-screens/`:

- `about-desktop-dark-en.png`, `about-desktop-dark-es.png`
- `about-desktop-light-en.png`, `about-desktop-light-es.png`
- `about-phone-dark-en.png`, `about-phone-dark-es.png`
- `about-phone-light-en.png`, `about-phone-light-es.png`
- `index.html`

Captured from the local Release preview at 1440 px and 390 px, build
`e7589ab`, demo family (Emma, X1 and Yoga), no real family data. The full-page
images show the sidebar footer (version/Version details/License) and the
dashboard footer AI credit.

## 8. What Ziur should check by hand

1. On the dashboard, click the version in the sidebar and **Version details**;
   both open `/about`.
2. `/about` in dark and light: shield and OpControl lockup, the author line,
   version/build, device versions, the license disclosure and the credits.
3. es-MX: the page, sidebar footer and license paragraph are in Spanish.
4. The real name appears only on `/about`; the kid view, README and footers
   still say Ziur.
5. Desktop and phone widths in both themes (screenshots above).

## 9. Cleanup / notes

- Staging and production temporary parents deleted; 0 `job20d-%` users remain.
- No owner or child login; no PIN, password, family, scaling, Windows or Luna
  setting was changed; OC’s git identity was not changed.
- Local preview stack and database will be removed after the report; the XPS
  build folder `C:\ocp-build\parental-20d` remains until the next job unless
  removed.
- Screenshots are from the local preview, not from the installed laptops.

## Limits

- The Yoga update is queued, not installed: the laptop was offline.
- The X1 screenshots are not included because the local preview was used to
  avoid real family data.
- The release MSI/setup are unsigned (unchanged from previous releases).
