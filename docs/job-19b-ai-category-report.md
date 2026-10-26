# Job 19b — “Public AI chats” website category (v0.17.9, X1 only)

**Status: complete and released.** v0.17.9 is on staging, production and the
**X1 only**. The Yoga was not updated and the hold is still in place. Luna
stayed enabled with the same key. No production owner or child login and no
production family/PIN/password/display-scaling/Windows setting change. The
staging verification used a temporary parent and temporary Luna test settings;
both were cleaned up.

- Branch: **`fix/ai-category-0.17.9`** from `main` (`5db2496`, v0.17.8).
- Feature commit: **`f7d51b5`** — “Job 19b: add the public AI chats category”;
  `main` fast-forwarded to it.
- Release commit: **`9ec3dbd`** — “Job 19b: release v0.17.9”; tag **v0.17.9**.
- Release workflow: **40390729543**, success; release published
  **2026-10-26 20:01:08 UTC**.
- Build host: **Dell XPS** (`Ziur-Dell-XPS`, `100.98.112.35`), SDK 10.0.402.
- Linux SDK 10.0.402: **355/355 tests pass**; full solution build **0 warnings,
  0 errors**.
- XPS: **355/355 tests pass**; full solution build **0 warnings, 0 errors**.
- No database migration was needed.

## What was built

### 1. One repo-owned list

`src/OpControl.Parental.Server/Resources/ai-category.json` is the single source
for the category. It is embedded in the server assembly
(`LogicalName=ai-category.json`) and has per-entry `name`, `group`, `blockable`,
`domains`, `sources` and `checked` fields, plus a top-level `version` (currently
**1**) and `checked` date.

- Included entries: **51**
- Unique domains: **92** (107 entry/domain rows before dedupe)
- `src/OpControl.Parental.Server/Services/AiCategoryCatalog.cs` loads the file,
  validates every domain, enforces the guard list, skips `unverified`/
  `not-by-domain` entries, dedupes, and applies the list to the existing
  `Categories` / `CategoryDomains` tables.
- `CategoryRefreshService` now runs the AI refresh on the same 24-hour cadence
  as the other categories. The DB version is bumped when the domain set changes
  or the file `version` is raised; a re-added domain reactivates its old row
  instead of creating a duplicate.

| Group | Included entries | Domain rows |
|---|---:|---:|
| Standalone AI chat | 19 | 53 |
| Companion/character | 13 | 26 |
| Search-engine dedicated hosts | 4 | 7 |
| Browser dedicated hosts | 3 | 9 |
| Windows dedicated host | 1 | 1 |
| Wrapper/copy sites | 11 | 11 |
| **Total** | **51** | **107 → 92 unique** |

### 2. Category in the dashboard

- Key `ai`, name **Public AI chats** / **Chats públicos de IA**, version 1,
  **off by default** for every child.
- Shown in the Websites Categories list with the short line:
  “Blocks AI chat and AI companion sites so Luna is the AI your child uses.”
  (“Bloquea sitios de chat de IA y de compañeros de IA para que Luna sea la IA
  que usan tus hijos.”)
- Collapsible **“What this does and doesn’t block”** / **“Qué bloquea y qué no”**
  with the requested Blocks / Doesn’t block / “new sites appear all the time”
  text.
- A small **Report a site** / **Reportar un sitio** link. There is no existing
  feedback/message endpoint for this, so the link uses a **mailto** to the
  server admin address from `ServerSettings.FromAddress` (currently
  `hendry.ruiz@opcontrol.net`) with subject `Report an AI site`. If that address
  is empty, the page falls back to the first `IsServerAdmin` user email; if
  neither exists, the link is hidden.
- en and es-MX, dark and light, desktop and 390 px. The category row has
  `id="category-ai"` and `scroll-margin-top`; the target gets a dashed outline.

### 3. Luna integration restored

- Removed the Job 18.4 `PublicAiBlockAvailable=false` switch and the disabled
  “Coming soon” button.
- The welcome popup button is **Open Websites** / **Abrir Sitios web** again and
  links to `/websites#category-ai`.
- The child page tip now reads the real `ai` category state for that child:
  `LunaUi.ShowPublicAiBlockTip(lunaOn, publicAiChatsBlocked)` is true only when
  Luna is on/valid and the `ai` category is off. It disappears when the parent
  turns the category on.
- Updated the welcome recommendation text so it no longer claims to block AI
  built into search engines/browsers.

## What is in the list

The importer includes `blockable` `full` or `partial` entries in groups
`standalone`, `companion`, `wrapper`, `search`, `browser` and `windows`.
Everything else is excluded.

Included examples: ChatGPT, Gemini, Copilot (web/Edge service), Claude, Grok,
Meta AI, Perplexity, DeepSeek, Mistral Vibe, Poe, HuggingChat
(**whole `huggingface.co`**, as requested), Pi, Qwen, Kimi, You.com, Phind,
ChatGLM, Doubao, ERNIE; Character.AI, Talkie, Chai, Replika, Janitor, Nomi,
Kindroid, PolyBuzz, Candy AI, Crushon, Muah, SpicyChat, DreamGF; the dedicated
search hosts `duck.ai`, `alice.yandex.ru`, `yiyan.baidu.com`,
`ernie.baidu.com`, `aistudio.baidu.com`; `copilot.microsoft.com`,
`edgeservices.bing.com`, `gemini.google.com`; and the known wrapper domains.

### Guard list (38 domains)

These are enforced in code (`AiCategoryCatalog.GuardDomains`). The importer
rejects an entry if the domain is a guard, or if it is a parent domain of a
guard. A dedicated subdomain such as `gemini.google.com` is allowed.

```
google.com, bing.com, duckduckgo.com, yahoo.com, yandex.ru, baidu.com,
youtube.com, x.com, instagram.com, cdninstagram.com, facebook.com,
messenger.com, whatsapp.com, web.whatsapp.com, snapchat.com,
web.snapchat.com, discord.com, discordapp.com, discord.gg, telegram.org,
web.telegram.org, github.com, microsoft.com, apple.com, amazon.com,
khanmigo.ai, khanacademy.org, magicschool.ai, schoolai.com, briskteaching.com,
diffit.me, web.diffit.me, ello.com, duolingo.com, canva.com, quizizz.com,
classroom.google.com, teams.microsoft.com
```

### Excluded and why (34 draft entries not imported)

| Reason | Count | Entries |
|---|---:|---|
| `blockable=unverified` | 2 | Sakura AI, Windows Notepad AI |
| `blockable=not-by-domain` | 11 | Google AI Mode/AI Overviews, Brave Search Answer with AI, Yahoo AI, Opera Aria, Arc/Dia, Meta AI in Instagram, WhatsApp, Messenger and Facebook, Snapchat My AI, Discord AI |
| Social group (not in category) | 2 | Grok on X, Telegram AI bots (standalone Grok domains are included separately) |
| No safe domains | 7 | Brave Leo, Copilot key, Windows Recall, Paint AI, Click to Do, Settings agent, gpt4free (github.com guarded) |
| Kid-safe `allow` entries | 12 | Khanmigo, Khan Academy, MagicSchool, SchoolAI, Brisk Teaching, Diffit, Ello, Duolingo, Canva for Education, Quizizz, Google Classroom, Microsoft Teams for Education |

Shared domains were dropped from mixed entries, for example `duckduckgo.com`
kept `duck.ai`; `bing.com` kept `copilot.microsoft.com` and
`edgeservices.bing.com`; `yandex.ru` kept `alice.yandex.ru`; `baidu.com` kept
the ERNIE hosts; `x.com` kept `grok.com`/`x.ai` from the standalone entry.

## Tests

New `tests/OpControl.Parental.Tests/Job19BAiCategoryTests.cs`:

1. embedded catalog has 51 entries / 92 domains, contains `huggingface.co`, and
   contains no guard or parent-of-guard domain;
2. unverified, not-by-domain, social and empty entries are skipped; duplicate
   domains are deduped;
3. guard list rejects `google.com`, `bing.com`, `khanacademy.org`,
   `duckduckgo.com`, `x.com`; dedicated `gemini.google.com` is allowed;
4. invalid domains are rejected;
5. category toggle is **off by default**, per child, and toggles independently;
6. an always-allow rule beats the AI category for `chatgpt.com` and
   `chat.openai.com`;
7. sample DNS-policy assertions: `chatgpt.com`, `chat.openai.com`,
   `character.ai`, `duck.ai` and `gemini.google.com` get `category:ai`, while
   `google.com`, `duckduckgo.com`, `bing.com`, `x.com`, `khanacademy.org` and
   `classroom.google.com` stay unblocked;
8. versioning bumps only on change/manifest bump and reactivates removed rows;
9. tip visibility: only Luna-on + AI-off;
10. welcome button links to `#category-ai`; the child page uses the real state;
11. all new en/es-MX strings exist.

Existing tests still pass. `Job184WebsiteTipTests.cs` was replaced by the new
Job 19b coverage because its “hidden until Job 19” assertions no longer apply.

## Screenshots

`/srv/opcontrol-parental/docs/job-19b-screens/index.html`:

- `websites-{en,es}-{dark,light}.png` — category and explanation open at
  1440 px;
- `websites-{en,es}-phone.png` — 390 px width;
- `popup-{en-dark,es-light}.png` — welcome popup with the restored
  **Open Websites** button;
- `popup-*-opened.png` — destination after the click;
- `child-{en-dark,es-light}-tip-{on,off}.png` — tip visible while the category is
  off and gone when it is on.

They are real headless Chromium captures of the staging dashboard. The staging
Luna settings were temporarily made test-ready to exercise the tip, then
restored. The temporary parent shown in the captures was deleted.

## Release v0.17.9

- `Directory.Build.props` **0.17.9**; notes
  `.github/release-notes/v0.17.9.{en,es}.md`; annotated tag **v0.17.9**.
- Workflow **40390729543** success; release published 20:01:08 UTC.
- Web image:
  `ghcr.io/ziur-vela/parental-opcontrol@sha256:a7fefc287c6d440dec029c9d50c1c64984d6b00084ead5f98d005dbfe266a4ca`
- Updater image:
  `ghcr.io/ziur-vela/parental-opcontrol-updater@sha256:12a3c8453a874a64c69282ba89dca56fe54f32009a3ae630edfe0584733407c4`
- MSI `OpControlParental-0.17.9.msi` **143,872,000** bytes SHA-256
  `2329ac2658771bac8a8ddce66a4921e3de51587b5855caedbe25ba31206443d3`.
- Setup `OpControlParental-Setup-0.17.9.exe` **261,161,810** bytes SHA-256
  `643712c2e2bb0018c3822d0117fc268e8da989253bd96c5cb43a9dfe42561685`.

### Staging

- Update 0.17.8 → 0.17.9 success; backup
  `/data/backups/parental-pre-update-20261026T200139Z.sql`
  (12,080,668 bytes, contains `PostgreSQL database dump complete`).
- `/version`: `v0.17.9 · build 2026-10-26 · 9ec3dbd`; `/health` **200**.
- `Categories` has `ai` / **Public AI chats** / v**1**, source
  `repo:src/OpControl.Parental.Server/Resources/ai-category.json`, **92 active
  domains**.
- **Staging Child**: `ai` on (turned on for the verification). **Beta Kid**: no
  `ai` row (off).
- `.env` pinned to the new web/updater digests; backup
  `.env.bak-job19b-20261026T200500Z`; `docker compose config -q` OK.

### Production

- Manual pre-update backup:
  `/opt/stacks/opcontrol-parental/backups/parental-pre-job19b-20261026T201000Z.sql.gz`
  (**5,908,742** bytes; `gzip -t` OK; `PostgreSQL database dump complete`),
  `parental-keys-pre-job19b-20261026T201000Z.tgz` (**714** bytes), and
  `docker-compose.yml.bak-job19b-20261026T201000Z`.
- Updater success; its own backup
  `/data/backups/parental-pre-update-20261026T200726Z.sql`
  (14,845,567 bytes, dump complete).
- `/version`: **v0.17.9 · build 2026-10-26 · 9ec3dbd**; `/health` **200**.
- `ai` category v1 with **92 active domains**.
- **Category off for every child:** `SELECT ... FROM "ChildCategories" WHERE
  "CategoryKey"='ai'` returns **0 rows**. Emma’s existing adult/fakenews/
  gambling/social toggles were not touched.
- Luna unchanged: `LunaFamilySettings` 1 row, `LastTestOk=1`, protected key
  length **219**; `ChildLunaSettings` 1 row, enabled; `LunaChats` **2**. No key
  or setting was written.
- `.env` pinned to the new digests; backup
  `.env.bak-job19b-20261026T201100Z`; `docker compose config -q` OK.
- The Job 18.3 hold mount is still present:
  `./data/release-assets-hold:/app/release-assets:ro`. Active `data/releases`
  contains **no 0.17.x** (only up to 0.16.1), so the Yoga cannot auto-update.

### X1 (only)

- Downloaded `OpControlParental-0.17.9.msi` to `C:\Temp\Job19B`; SHA-256 matched
  `2329ac26…`; `msiexec /i … /qn /norestart /l*v` exited **0**, log says
  `Installation success or error status: 0`.
- `C:\ProgramData\OpControlParental\config.json` is unchanged (same server,
  device name `ZIUR-X1-LAPTOP`, monitored account `Emma`, helper path).
- Service `OpControlParental`: **Running**, Automatic. Client, Helper and Luna
  file versions **0.17.9.0**. Session 0: Client; session 1: Helper
  `--monitored "Emma"` and **one** Luna moon, no `--kid-window`.
- Dashboard: **ZIUR-X1-LAPTOP · 0.17.9 · online** (LastSeen
  2026-10-26 20:12:00 UTC). Same device
  `89f9db76-3e1c-458a-8053-44cf26df451e`, not revoked. No lock triggered, no
  active remote session.
- Log watch: no new product crash/error; `luna.log` shows the moon sleeping and
  healthy; service log shows normal 200s. The only `error` lines are the
  pre-existing `remote-capture-error` entries from before this install.
- `C:\Temp\Job19B` was removed after verification.

### Yoga — not touched

`EMMA-YOGALAPTOP` is still **0.16.1**; `LastSeenUtc`
2026-10-26 03:22:00 UTC and `LastUpdateCheckUtc` 2026-10-26 01:52:53 UTC are
both from before this job. No Job 19b binary, installer or setting was sent to
the Yoga. The active release catalog still has no 0.17.x.

## Cleanup

- Staging temporary parent `job19b@opcontrol.local` deleted.
- Staging Luna settings restored: chat key/model/test values cleared, child
  Luna disabled; the staging `ai` toggle remains on for Staging Child as the
  verification state.
- X1 `C:\Temp\Job19B` removed; XPS build copy
  `C:\ocp-build\parental-19b` removed.
- No production owner/child login, no password/PIN/family change, no Windows
  settings or display scaling changed, Tailscale untouched, 0 active remote
  sessions.
- Production `ai` category remains **off** for Emma; Ziur can turn it on.

## Limits / notes

- This is still a **DNS-only** category. It blocks dedicated AI hostnames, not
  AI answers embedded in Google/Brave search, social apps, browsers or Windows.
  Those are listed in the dashboard explanation and remain for the later
  browser/Windows policy job.
- `huggingface.co` is blocked as a whole site by design; that also blocks
  Hugging Face models/datasets.
- The Report a site link uses a mailto to the server admin because no existing
  feedback path was found. If `ServerSettings.FromAddress` is empty, the page
  falls back to the first server-admin email; otherwise the link is hidden.
- Screenshots are headless Chromium, not a physical phone; 390 px is viewport
  emulation.
- Tests use an in-memory policy evaluation for “DNS block / still resolve”; the
  live staging run confirms the category data and toggle, not a physical child
  PC DNS lookup.
