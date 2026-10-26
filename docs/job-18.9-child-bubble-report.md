# Job 18.9 — child chat bubble colour and avatar (v0.17.8, X1 only)

Status: **complete and released.** v0.17.8 is on staging, production and the
**X1 only**. The wider window, wider bubbles, auto-scroll and lock screen from
Jobs 18.7/18.8 were left as they were. The Yoga was not updated and the hold is
still in place. Luna stayed enabled with the same key. No owner/child login, no
family, PIN, password, display-scaling or Windows setting change.

- Branch: **`fix/luna-0.17.8`** from `main` (`e3e17aa`, v0.17.7).
- Commit: **`414d90e`** — “Job 18.9: mint child bubbles and vector avatars”;
  `main` fast-forwarded to it.
- Tag: **v0.17.8**; release workflow **40375612978** success; GitHub release
  published 2026-10-26 18:21:22 UTC.
- Tests: **342/342 Linux SDK 10.0.402** and **342/342 Dell XPS**; Windows
  solution build: **0 errors, 0 warnings**.
- Design file: `/srv/oc-launcher/prompts/luna-design.md` section 1 updated
  (“child’s bubbles right in the subject color” replaced by the mint rule and
  the avatar paragraph); a copy is in
  `/srv/opcontrol-parental/docs/luna-design.md`.

## Child bubble colour

The child’s own Luna bubble no longer uses the subject tile:

| Theme | Background | Text | Border | Contrast |
|---|---|---|---|---|
| Night sky | `#3ddc97` | `#15103a` | `#22a46d` | **10.18:1** |
| Moon gray | `#14845a` | `#ffffff` | `#0f6b49` | **4.69:1** |

- New palette fields `ChildChatBg`, `ChildChatInk` and `ChildChatBorder` are
  used only by the Luna chat; the kid view’s existing `ChildBubble` palette
  fields were not touched.
- The bubble keeps its rounded shape with the small bottom-right corner
  (`CornerRadius 14,14,4,14`).
- The text is 15 px **Bold (700)** in a fully opaque `SolidColorBrush`; no
  opacity or light weight remains. The old faint look came from the subject
  blue/purple and the lighter 14.5 weight rather than a transparent brush; both
  are now gone.
- The subject colour still drives the header icon, chat-list dots and every
  drawn math picture.

## “You · time · status” line

The v0.17.7 Luna chat had no metadata line, so the requested line was added
inside the child bubble at 9.5 px Bold in the same fully opaque ink:

`You · 10:38 AM · Sent`

- “You” is localized (`Client.Messages.SentByYou`, “Tú” in es-MX); the time is
  the message’s local `CreatedUtc` using the current culture.
- The status uses the existing localized messages: `Sending…` while the request
  is in flight, `Sent` after the server accepts it, and `Failed` if the send
  throws. Loaded history shows `Sent`.
- The line is inside the bubble so it stays readable on the new mint/dark-green
  colours; the normal bubble border and corner are unchanged.

## Child avatar

A flat vector avatar now sits on the **right** of every child bubble, top
aligned like Luna’s face and the same **34 px** chat size. Luna’s chat face was
moved from 28 px to 34 px so both sides match. The child bubble and the avatar
are laid out with a `0.12* / 0.88* / Auto` star grid; the avatar column is
outside the bubble’s 88% column.

- `LunaAvatars` draws eight choices with vector shapes only (no emoji font):
  **face** (default warm `#f2c48d` face, dark eyes, smile, no hair), **cat**,
  **dog**, **fox**, **rocket**, **star**, **planet**, **robot**.
- Every avatar has the 2 px ring in the child-bubble colour (`#3ddc97` on
  Night, `#14845a` on Gray) and uses the same flat style as Luna.
- The picker opens from **tapping the avatar** or **⋯ → Change avatar** and
  shows a 4 × 2 grid of previews with plain names. English: Face, Cat, Dog, Fox,
  Rocket, Star, Planet, Robot. es-MX: Cara, Gato, Perro, Zorro, Cohete,
  Estrella, Planeta, Robot. Each button has the name as its tooltip and
  accessible name.
- The choice is stored per child in
  `%LOCALAPPDATA%\OpControlParental\kid-avatar.txt`, next to the theme file,
  and read at Luna startup. Selecting an option rebuilds the chat (preserving
  the scroll policy and the New-message pill).
- Only the Luna chat child bubbles use the avatar; the kid view, dashboard and
  parent pages were not changed.

## Tests

New `tests/OpControl.Parental.Tests/Job189AvatarTests.cs` (4 tests):

1. `KidAvatarStore` round-trip using a temporary path: missing file → face,
   `cat` saved and reloaded, invalid value → face, all eight keys valid;
2. all eight avatars in a headless layout at **100%, 125%, 150% and 200%**
   render scaling, each 34 DIP and with its vector children;
3. the child bubble integration in both themes: exact `#3ddc97`/`#14845a`
   background, `#15103a`/white ink, **Bold** text, the `You · …` line, the
   `Sent` status and a 34 px `ChildAvatar` containing the selected face;
4. source wiring checks for the eight-key picker, the store read/write and the
   34 px avatar size.

The existing `Job188ChatTests` contrast test was updated to the new child
bubble colours and still checks every subject tile in both themes. Its child
bubble/source test now expects `ChildBubbleColumns`, `ChildChatBg/Ink` and the
avatar store instead of the old subject `style.Ink`.

## Screenshots

`/srv/opcontrol-parental/docs/job-18.9-screens/index.html` and the repo copy
`docs/job-18.9-screens/`:

- `before-night.png`, `before-gray.png` — v0.17.7 child bubbles in the subject
  blue with no avatar;
- `after-night.png`, `after-gray.png` — v0.17.8 mint/dark-green bubbles, the
  `You · time · status` line and the face avatar;
- `avatars-night.png`, `avatars-gray.png` — all eight vector avatars in both
  themes.

They use the real Avalonia app, theme resources and fonts with the production
Math chat, rendered through headless Skia at 960 px chat width. The transparent
Luna window cannot be captured by DWM/GDI, so these are real-renderer images,
not an on-screen capture.

## Release v0.17.8 (X1 only)

- Version bump `414d90e`; `Directory.Build.props` **0.17.8**; release notes
  `.github/release-notes/v0.17.8.{en,es}.md`; annotated tag **v0.17.8**.
- Workflow **40375612978** success; release published 18:21:22 UTC.
- Web image `ghcr.io/ziur-vela/parental-opcontrol@sha256:69f3c1999292b3408ef21070be815ff85ba8895e0fa0ba1c1ad50a3fbad90d5b`
- Updater image `...-updater@sha256:b083e2e0463c903894c04114424f760e50432936b0040c2f97d1159344348803`
- MSI `OpControlParental-0.17.8.msi` **143,892,480** bytes SHA-256
  `c8b6ad09a2dbbb611110b0e58e29143b8a465b006b9874945e482f72dffbc1f1`.
- Setup `OpControlParental-Setup-0.17.8.exe` **261,182,290** bytes SHA-256
  `e08a4e2e8474e5fa0c4c02dcd043e95bf78f6a34f8d63da19e8b97bbbd86e188`.

### Staging

- Updater success 18:22:02 UTC; backup
  `/data/backups/parental-pre-update-20261026T182202Z.sql`.
- `/version`: `v0.17.8 · build 2026-10-26 · 414d90e`; `/health` **200**;
  `/luna` **302**. Web/updater on the new digests.
- `.env` pinned; backup `.env.bak-job189-20261026T182255Z`;
  `docker compose config -q` OK.

### Production

- Manual pre-update backup:
  `/opt/stacks/opcontrol-parental/backups/parental-pre-job189-20261026T182303Z.sql.gz`
  (**5.7 MB**, `gzip -t` OK, contains `PostgreSQL database dump complete`,
  54 `CREATE TABLE` statements), plus
  `parental-keys-pre-job189-20261026T182303Z.tgz` and
  `docker-compose.yml.bak-job189-20261026T182303Z`.
- Updater success 18:23:10 UTC; its own backup
  `/data/backups/parental-pre-update-20261026T182310Z.sql`.
- `/version` public and local: **v0.17.8 · build 2026-10-26 · 414d90e**;
  `/health` **200**; `/luna` **302**. Web/updater on the new digests;
  `.env` pinned (`.env.bak-job189-20261026T182342Z`); `docker compose config -q`
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

- Downloaded `OpControlParental-0.17.8.msi` to `C:\Temp\Job189`; SHA-256
  matched `c8b6ad09…`; `msiexec /i … /qn /norestart /l*v` exited **0** and the
  log says `Installation success or error status: 0`.
- `C:\ProgramData\OpControlParental\config.json` is unchanged.
- Service `OpControlParental`: **Running**, Automatic. Client, Helper and Luna
  file versions **0.17.8.0**. Session 1: Client (PID 13232), Helper
  `--monitored "Emma"` (PID 4328) and **one** Luna moon (PID 14672, no
  `--kid-window`).
- No new `.NET Runtime`/Application Error/WER entry in the last 10 minutes;
  `luna.log` shows a healthy sleeping moon.
- Dashboard: **ZIUR-X1-LAPTOP · 0.17.8 · online** (LastSeen 18:27:21 UTC);
  device `89f9db76-3e1c-458a-8053-44cf26df451e` not revoked. No lock was
  triggered by this job.
- `C:\Temp\Job189` was removed after verification.

### Yoga — not touched

The Yoga (**EMMA-YOGALAPTOP**) is still **0.16.1**, `LastSeenUtc`
2026-10-26 03:22:00 UTC and `LastUpdateCheckUtc` 01:52:53 UTC — both before this
job started. No Job 18.9 binary, installer or setting was sent to the Yoga.

## Limits / notes

- The before/after images are real-renderer headless Skia captures, not DWM
  captures of the transparent Luna window (the Job 18.4 limitation).
- The child bubble’s status line reports the client send state
  (`Sending…`/`Sent`/`Failed`); it is not a read receipt. Loaded history is
  shown as `Sent`.
- The avatar picker is a flyout and is exercised through the store, layout and
  bubble integration tests; the transparent chat window itself was not
  interacted with on the X1.
