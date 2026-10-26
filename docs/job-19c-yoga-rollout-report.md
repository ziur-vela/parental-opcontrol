# Job 19c — Yoga rollout to v0.17.9 (release hold ended)

**Status: complete.** Production is on **v0.17.9**, the **Yoga** is on **v0.17.9** and online/healthy, and the release hold has been removed. The X1 was already on v0.17.9 and was left alone. Luna and the Public AI chats category were **not changed by this job**.

- Production backup:
  `/opt/stacks/opcontrol-parental/backups/parental-pre-job19c-20261026T204000Z.sql.gz`
  (**5,914,471** bytes; `gzip -t` OK; `PostgreSQL database dump complete`).
- Compose backup:
  `/opt/stacks/opcontrol-parental/backups/docker-compose.yml.bak-job19c-20261026T204000Z`.
- No Windows settings, PIN, passwords, family settings or Luna/category settings
  were changed. No child or owner account was logged in.

## 1. Release hold removed

- Removed this block from production `docker-compose.yml`:
  `./data/release-assets-hold:/app/release-assets:ro` and its Job 18.3 comments.
- `docker compose config -q` passed; web recreated with
  `docker compose up -d --force-recreate web`.
- Server log confirmed the baked assets were published:
  - `Published baked client release OpControlParental-0.17.9.msi for server version v0.17.9`
  - `Published baked client release OpControlParental-Setup-0.17.9.exe for server version v0.17.9`
- Active `data/releases` now contains the **0.17.9** MSI and Setup (the current
  release, not the older held 0.17.0 files).
- `/health` **200**.
- `/api/releases` (authenticated check with a temporary parent, deleted after):
  - current **0.17.9**, SHA-256
    `2329ac2658771bac8a8ddce66a4921e3de51587b5855caedbe25ba31206443d3`,
    size **143,872,000**, published **2026-10-26T19:58:23Z**, previous
    **0.16.1**.
  - `/downloads` shows **V0.17.9**, the MSI hash above, and the Setup/MSI links.
- The `data/release-assets-hold` directory still exists but is no longer mounted.

## 2. Yoga update — normal product path

The Yoga was online and on **0.16.1**. I did **not** need the SSH/MSI fallback:

1. Dashboard → Devices showed **EMMA-YOGALAPTOP**, client 0.16.1, latest 0.17.9,
   **UPDATE AVAILABLE**.
2. Clicked **UPDATE NOW**. Database status went `requested` → `installing` →
   `0.17.9` / `healthy`. `LastUpdateCheckUtc` changed from 2026-10-26 01:52:53Z
   to 2026-10-26 20:38:17Z.
3. Yoga `update.log`:
   - `2026-10-26T13:38:19-07:00 starting update to ...OpControlParental-0.17.9.msi`
   - `2026-10-26T13:38:59-07:00 msiexec exited 0`
   - `2026-10-26T13:38:59-07:00 new service reported healthy`
   (20:38:19–20:38:59 UTC).
4. Dashboard after update: **EMMA-YOGALAPTOP · client 0.17.9 · latest 0.17.9 ·
   online · healthy · no UPDATE NOW button**.

## 3. Enrollment preserved

- Same single device row:
  **`d3b94f63-678b-492e-83d6-19f3036b89f8` / EMMA-YOGALAPTOP**, not revoked,
  child Emma.
- `C:\ProgramData\OpControlParental\config.json` last write is **2026-10-20**
  and `private\device.json` last write is **2026-10-25 18:46** — both unchanged
  by the 0.17.9 upgrade. No re-enrollment.
- Service `OpControlParental` is **Running / Automatic**.
- Client, Helper and Luna file versions are all **0.17.9.0**.
- Processes: Client (session 0, SYSTEM), Helper (session 1, Emma), one Luna
  (session 1, Emma), no second Luna/kid window.

## 4. Sleep size kept at 1.9

The installed Luna reads the sleep scale from the child user's local app data
(the legacy file next to the exe is absent):

`C:\Users\Emma\AppData\Local\OpControlParental\luna-sleep-scale.txt` = **1.9**

- ACL: `EMMA-YOGALAPTOP\Emma:(I)(F)` (readable by the Luna process), plus
  SYSTEM/Administrators.
- Restarted only the Luna process; the Helper supervisor relaunched it as PID
  **7604**. Luna log confirms:
  `sleep_scale=1.9 visible_dip=53.2x53.2 source=startup`.
- The 1.9 value matches both spike files:
  `C:\Temp\LunaSpike\sleep-scale.txt` and `C:\Temp\LunaDev\sleep-scale.txt`
  (both read as `1.9`; left untouched).

## 5. Verification

- Dashboard: **EMMA-YOGALAPTOP · 0.17.9 · online · healthy** (LastSeen
  2026-10-26 20:42:49 UTC; later heartbeats continued).
- Service running; client/helper/Luna 0.17.9.0; one Luna; enrollment/device row
  unchanged.
- **No lock triggered.** `LockStates` for Emma: `ManualLock=false`,
  `UnlockUntilUtc=2026-10-27T07:00:00Z` (pre-existing dashboard unlock); this job
  did not lock/unlock.
- **Luna unchanged:** enabled for Emma, protected key length **219**, same
  family settings; no key or toggle was written.
- **Public AI chats category unchanged:** already **on** for Emma, toggled by
  the owner `hendry.ruiz@opcontrol.net` at **2026-10-26 20:28:10Z** before the
  Yoga work. This job did not change it.
- 5-minute log watch (20:39–20:44 UTC): no product errors, crashes or WER
  entries. The only warnings are the known `meshagent` NUL-sanitizer warnings and
  the expected update-time Hub reconnect / DNS-forwarding blip at 20:38.
- Yoga had **0 active remote sessions** during cleanup. One owner-initiated
  Screen session on the X1 (created 20:41:10Z by `hendry.ruiz@opcontrol.net`)
  was present; it was not created by this job and was left untouched.

## 6. Cleanup

```
temp production parent job19c@... deleted: DELETE 1
remaining job19c% users: 0
Yoga active remote sessions: 0
```

- No temporary files were created on the Yoga. `C:\Temp\LunaSpike` and
  `C:\Temp\LunaDev` were left in place as instructed.
- Temporary local verification scripts/screenshots were removed.
- No temporary remote access remains from this job.

## 7. What Ziur should check by hand on the Yoga

The Yoga is 1366×768 at 100% scaling. No child account was logged in and no lock
was triggered, so the following are for a quick visual check:

1. **Luna window size:** Luna started with `screen=1366x768@0,0`,
   `work=1366x728`, `scaling=1`, chat window **683×600** at `0,0`; sleep sliver
   region **92×92**; sleep scale **1.9** → visible sleep dip **53.2×53.2**.
   Check that the moon/sleep size feels right on the physical screen.
2. **Kid view:** open My OpControl from the tray and check the 1366×768 layout,
   Home/Schedule/Rules/Activity/Messages tabs, and that the window fits.
3. **Lock screen:** lock and unlock once when convenient (PIN unchanged) and
   check the lock screen layout at 1366×768; this job did not open or change it.

## Notes / limits

- The Yoga updated through the normal dashboard per-device update; the SSH MSI
  fallback was not used.
- The release hold removal is live in production; future devices can now see
  0.17.9 and later releases in the active catalog. The old held directory is
  still present but unused.
- The 1.9 sleep value is written into Emma's local app data; it will survive
  normal app/service updates because that path is outside Program Files.
