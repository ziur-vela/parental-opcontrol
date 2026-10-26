# Job 19f — parent "Delete all chats" and the Luna app (v0.17.12)

**Status: complete and released.** The server was already deleting correctly; the
Luna app was showing an in-memory list it never refreshed. v0.17.12 fixes the
refresh and the removed-chat path, and the dashboard now confirms the delete
counts. Both laptops are on 0.17.12.

- Branch: **`fix/luna-delete-chats-0.17.12`** from `main` (`7438cee`, v0.17.11).
- Feature commit: **`465b652`** — “Job 19f: refresh Luna chats and remove deleted
  chats cleanly”; `main` fast-forwarded.
- Release commit: **`648eb47`** — “Job 19f: release v0.17.12”; tag **v0.17.12**.
- Release workflow: **40415261006**, success; release published
  **2026-10-26 22:56:12 UTC**.
- Build host: **Dell XPS** (`Ziur-Dell-XPS`, `100.98.112.35`), SDK 10.0.402.
- Tests: **375/375 Linux SDK 10.0.402** and **375/375 Dell XPS**.
- No database migration.

## 1. What was wrong (in plain words)

**The server was right.** A read-only SELECT on production for Emma
(`1bf0d329-3ea4-4391-95c5-2819c3278226`) returned **0 Luna chats and 0 Luna
messages**. There were no soft-delete or hidden rows to find: the parent delete
performed hard deletes of the chat rows (which also carry the title and summary)
and the message rows. The review page's "All" filter uses the same chat table, so
it also had nothing to show. The Luna chat-list endpoint
(`GET /api/luna/chats`) reads the same `LunaChats` table and cannot return a chat
that is not there.

**The client was stale.** The Luna app has no on-disk chat cache. The chat list
lived only in the long-running `LunaAppView` process (`_chats`), and it was only
loaded once in `InitAsync` when the Luna process started. Closing the Luna window
returns to the moon **without stopping the process**, and reopening it did not
refresh the list, so the three deleted chats stayed on screen. A full process
restart would have cleared them, which is why the server looked correct.

The three chats were already gone from production; they did not need to be
deleted again.

## 2. Fix

### Dashboard / server

`LunaParentService.DeleteChatAsync` and `ClearAsync` still hard-delete the chat
(including title and summary) and its messages, and they now return
`(Chats, Messages)` counts. The learner profile is deliberately untouched.

The two parent API endpoints (`/api/children/{childId}/luna/chats/{chatId}/delete`
and `/api/children/{childId}/luna/clear`) return `chats` and `messages`, and the
Review page shows the result as:

- EN: `Chats deleted: 3 · Messages deleted: 12`
- ES: `Chats eliminados: 3 · Mensajes eliminados: 12`

### Luna app

- **Refresh on open:** `LunaWindow.OpenChat` now calls
  `LunaAppView.RefreshForOpenAsync()`. Every time Luna is opened from the moon it
  fetches the current list (including hidden chats), rebuilds the home/chats
  screen when it changed, and handles a selected chat that no longer exists.
- **Refresh when Home is shown:** `ShowHome()` now refreshes in the background
  and re-renders only when the list fingerprint changed, so back/close/theme
  changes do not loop or flicker.
- **Removed chat path:** if loading a chat's messages returns
  `400 Bad Request` or `404 Not Found` (the server's "chat not found"), the app
  removes that chat from both local lists, returns to Home and shows
  **"This chat was removed"** / **"Este chat fue eliminado"**.
- **Local cache:** there is no disk cache to clear. The in-memory `_chats` and
  `_hiddenChats` lists are updated on every refresh and the deleted id is removed
  from both on the removed-chat path.
- To make the client testable without a child token, the view now talks to a
  small `ILunaApiClient` interface; `LunaApiClient` implements it unchanged.

## 3. Tests

New `tests/OpControl.Parental.Tests/Job19FDeleteChatsTests.cs` plus two server
tests in `LunaTests.cs` (5 new tests total):

1. delete one chat removes that chat and all its messages, leaves the other chat
   and the learner profile, and the list endpoint returns only the remaining chat;
2. delete all chats removes every chat and message, keeps the learner profile,
   and the chat-list endpoint (normal and `includeHidden`) returns nothing;
3. showing Home refreshes the list and drops a chat deleted on the server;
4. reopening Luna refreshes the list;
5. opening a deleted chat returns to Home with the removed-chat note and removes
   it from the local list.

Existing tests still pass: **375/375** on Linux and **375/375** on the XPS.

## 4. Release v0.17.12

- `Directory.Build.props` **0.17.12**; notes
  `.github/release-notes/v0.17.12.{en,es}.md`; annotated tag **v0.17.12**.
- Workflow **40415261006** success; release published 22:56:12 UTC.
- Web image:
  `ghcr.io/ziur-vela/parental-opcontrol@sha256:92ca52d4db5aca612be410a7a130050af0c001ce5470a259188d5daedf05297e`
- Updater image:
  `ghcr.io/ziur-vela/parental-opcontrol-updater@sha256:995891d2512957936bf6b818cf8fd71775e8c45f0f29b32f3349c4d16df64477`
- MSI `OpControlParental-0.17.12.msi` **143,843,328** bytes SHA-256
  `9d33d1fc0446ee1bc0578fc651f526369b36e171873a4594d621e5f8f5418ae9`.
- Setup `OpControlParental-Setup-0.17.12.exe` **261,133,140** bytes SHA-256
  `a42d28a89d473c3b4f396001a139af0ec2b38cb6bf35e24dc91a27bc32dcd6fe`.

### Staging

- Update 0.17.11 → 0.17.12 success; backup
  `/data/backups/parental-pre-update-20261026T225707Z.sql`.
- `/version`: `v0.17.12 · build 2026-10-26 · 648eb47`; `/health` **200**.
- Web container `92ca52d4db5a` healthy; updater `995891d25129`.
- `.env` pinned to the new digests; backup
  `.env.bak-job19f-20261026T225759Z`; `docker compose config -q` OK.

### Production

- Manual pre-update backup:
  `/opt/stacks/opcontrol-parental/backups/parental-pre-job19f-20261026T225804Z.sql.gz`
  (**5,927,429** bytes; `gzip -t` OK; `PostgreSQL database dump complete`),
  plus `docker-compose.yml.bak-job19f-20261026T225804Z` and
  `.env.bak-job19f-20261026T225804Z`.
- Updater backup:
  `/data/backups/parental-pre-update-20261026T225811Z.sql`.
- `/version`: **v0.17.12 · build 2026-10-26 · 648eb47**; `/health` **200**.
- `data/releases` has 0.17.12 and keeps 0.17.11 as the previous release; both
  MSI and Setup SHA-256 values match the release assets above.
- `.env` pinned to the new digests; `docker compose config -q` OK.
- **No direct INSERT/UPDATE/DELETE was run on production data.** Only read-only
  SELECTs and the `pg_dump` backup were used.

### Both laptops (normal catalog)

Because this job prohibited direct production DB writes, the normal dashboard
"Update Now" request (which needs a temporary parent row) was not used. Instead
the **OpControlParental service was restarted** on each laptop; on start the
existing client update service performs its normal catalog check
(`/api/device/update`), downloads the 0.17.12 MSI from the catalog, verifies its
SHA-256 and installs it through the same update runner used by "Update Now".

- **ZIUR-X1-LAPTOP**: update log 23:00:02 → 23:00:34 UTC, `msiexec` exit 0, new
  service healthy.
- **EMMA-YOGALAPTOP**: update log 23:01:33 → 23:01:59 UTC, `msiexec` exit 0, new
  service healthy.
- Both: client/helper/Luna **0.17.12.0**, service **Running/Automatic**, one Luna
  each in session 1, dashboard **online** on 0.17.12; same device rows:
  `89f9db76-3e1c-458a-8053-44cf26df451e` (X1) and
  `d3b94f63-678b-492e-83d6-19f3036b89f8` (Yoga), not revoked.
- X1 Luna started with `sleep_scale=1.6` (default); Yoga kept **1.9**
  (`C:\Users\Emma\AppData\Local\OpControlParental\luna-sleep-scale.txt`).
- No lock was triggered: Emma `ManualLock=false`; the pre-existing
  `UnlockUntilUtc` was not changed. 0 active remote sessions.
- Luna settings unchanged: protected key length **219**, Setup A / OpenRouter /
  `anthropic/claude-haiku-5.5`, `LastTestOk=true`, child Luna enabled, theme
  **night**, daily limit **60**; 0 chats and 0 messages remain.
- Category settings unchanged: `adult`, `ai` (Public AI chats), `fakenews`,
  `gambling`, `social` — all still enabled.

## 5. What Ziur should do next

- **Nothing for the old three chats.** The production server already has 0 chats
  and 0 messages, and both laptops now run 0.17.12 (their Luna processes were
  restarted during the update), so the stale list is gone.
- In future, after pressing **Delete all chats** or **Delete chat**, the notice
  shows the exact counts. Open Luna (or return to its Home screen); the list
  refreshes automatically. If a chat disappears between the list load and the
  click, Luna shows "This chat was removed" instead of an empty chat.
- If another device still shows old chats, make sure that device has received
  v0.17.12; a Luna restart on the old build is only a temporary workaround.

## 6. Cleanup

- XPS copy `C:\ocp-build\parental-19f` and the transfer zip removed.
- Local TRMM/Playwright helper scripts removed.
- No production temp parent, no production INSERT/UPDATE/DELETE, no temp database.
- No owner or child account was logged in; no PIN/password/family/Windows setting
  was changed; Tailscale untouched.

## Limits / notes

- The client refresh is triggered by opening Luna and by showing Home, not by a
  push message while the Luna window is already open on a chat. The selected chat
  is checked on the next Luna open.
- The removed-chat note stays on Home until the child starts or opens another
  chat; it is a short status banner, not a modal.
- The screenshots from Job 19e are unaffected; this job changed no visual theme.
