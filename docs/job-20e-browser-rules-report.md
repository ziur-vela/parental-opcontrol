# Job 20e — Browser rules and device-online alerts (v0.17.20)

**Status: complete and released.** v0.17.20 is on staging, production and the
**X1 only**. The Yoga was not targeted, tested or deployed and is unchanged at
0.17.19. The Shorts toggle is **off** in production; Ziur turns it on when he
wants.

- Branch: **`fix/job20e-browser-rules-0.17.20`** from `main` (`29d46b1`,
  v0.17.19).
- Feature commits:
  - **`6755fe8`** — Job 20e: add browser rules and device-online alerts.
  - **`b85fd07`** — Job 20e: write URLBlocklist as a numbered registry subkey
    (correction found during X1 verification). **Final tag `v0.17.20` is on
    this commit.**
- Release workflows: **41345462064** (first build, success) and
  **41348120135** (final build after the registry fix, success). The tag and
  release assets were moved to `b85fd07`; the GitHub release was updated with
  the final images and installers.
- Tests: **469/469 Linux SDK 10.0.402** and **469/469 Dell XPS**
  (`Ziur-Dell-XPS`, `100.98.112.35`, SDK 10.0.402).
- Windows build host: **Dell XPS**. The XPS built and validated the installers
  (MSI `178BFE26…` / 144,011,264 bytes; Setup `93EBD09E…` / 261,305,172 bytes
  for the final commit). The release assets are built by the release workflow;
  the catalog uses those assets (hashes below).
- Migration: **`20261101193533_Job20eBrowserRulesOnline`** (23 migrations after
  the update, 22 before).

## 1. Browser rules

### What was built

- **Per child, per Windows user.** `ChildBrowserSetting` (the Shorts switch)
  and `BrowserPageRule` (host + optional path) are stored per family and child.
  The client writes only to the **monitored child's own user hive**
  (`HKEY_USERS\<child SID>\Software\Policies\...`). It never writes HKLM and
  never touches the parent's or another user's hive.
- **Exact policy encoding.** Chromium list policies are a **subkey named
  `URLBlocklist` with numbered `REG_SZ` values `1`, `2`, …**, not a single
  multi-string value. The first v0.17.20 build used a multi-string value and
  Brave correctly ignored it; `b85fd07` fixes this and removes the legacy value
  when it appears. The manager also remembers and preserves any pre-existing
  numbered entries.
- **Targets written:** Chrome (`Software\Policies\Google\Chrome`), Edge
  (`Software\Policies\Microsoft\Edge`) and Brave
  (`Software\Policies\BraveSoftware\Brave`). Only the `URLBlocklist` list is
  managed; the existing DoH values (DnsOverHttpsMode/DNSOverHTTPS) are untouched.
- **Apply timing.** The client applies the policy while the monitored account is
  signed in, immediately when the server state changes, and when the child signs
  in. It verifies the policy every 15 seconds and re-applies it if it was
  removed or edited.
- **Tamper alert.** A re-application queues the client tamper kind
  `browser-policy-removed`. The dashboard shows a dedicated localized tamper
  alert (`Browser rules removed: <device>` / `Se quitaron las reglas del
  navegador: <device>`) and the Activity page shows
  “Browser rules were removed and restored”.
- **Clean removal.** Turning a rule off writes the baseline back (or deletes
  the `URLBlocklist` subkey when the baseline is empty). Uninstall calls
  `--prepare-uninstall`/service shutdown cleanup, loads the child's hive briefly
  with `reg load` if the child is signed out, restores the baseline and removes
  our entries.
- **Dashboard.** New **Browser rules** card on `/websites`, per child, en/es-MX:
  Shorts toggle, custom page rules with validation and remove buttons, the
  supported-browsers note, the “block other browsers in Apps” link, and the
  one-click **Block other browsers** helper (nothing happens until a parent
  clicks). Text says browsers pick up changes within a minute or after a restart.
- **Shorts patterns.** Exactly:
  `youtube.com/shorts`, `www.youtube.com/shorts`, `m.youtube.com/shorts`.
  The tests assert exactly these three. Regular YouTube, search and channels are
  not touched.
- **Custom rules.** A host with an optional path, with or without a scheme
  (`x.com/i/grok`, `https://X.com/Path`). Hosts are validated; paths reject
  spaces, control characters, `?`, `#`, `@`, `\`, `*` and dot segments.
  Duplicates, invalid hosts and invalid paths are rejected with localized
  errors.
- **Block other browsers helper.** Matches installed apps against common
  unsupported/portable browsers: Firefox, LibreWolf, Waterfox, Tor Browser,
  Opera/Opera GX, Vivaldi, Chromium/Ungoogled Chromium, Thorium, Catsxp, Yandex,
  Maxthon, Pale Moon, SeaMonkey, Falkon, Midori, Slimjet, SRWare Iron, Comodo
  Dragon, Epic Privacy Browser, Avast/AVG Secure Browser, 360/QQ/UC Browser,
  DuckDuckGo Browser, Zen, Floorp, Mullvad, K-Meleon, Otter, Basilisk and
  GNU IceCat. It skips Chrome, Edge and Brave. Off by default.

### Which browsers honor user-level policy (X1 evidence)

| Browser | User-level policy | Live check on the X1 |
|---|---|---|
| **Brave 156.1.98.63** | **Honored** | `brave://policy` lists **URLBlocklist** with source **Platform** (`x1-brave-policy.png`). With the temporary rule `example.com/ocp-policy-test`, Brave returned `net::ERR_BLOCKED_BY_ADMINISTRATOR` for that path, while `https://example.com/` loaded normally (HTTP 200). Removing the rule restored the URL. |
| **Chrome** | Supported by Chromium on Windows (HKCU), same policy schema | Chrome is **not installed** on the X1. The Chrome key and numbered subkey were written and verified; there was no browser to launch. |
| **Edge** | Supported by Chromium/Edge on Windows (HKCU), same policy schema | No Edge desktop browser on the X1 (only the WebView2 runtime is installed). The Edge key and numbered subkey were written and verified; there was no browser to launch. |
| **Firefox** | **Not honored at user level.** Firefox on Windows reads its enterprise policy from the machine (HKLM / distributions), not the user hive. | Not installed on the X1. The product deliberately **does not write HKLM** and makes no Firefox change; the card states this in both languages. |

We did not fall back to HKLM for any browser, so parents and other Windows
users are unaffected.

### X1 registry verification

- Emma SID: `S-1-5-21-2257060886-3514852105-3305933139-1002`.
- While the test rule was active, all three browser keys under
  `HKEY_USERS\<Emma SID>\Software\Policies\...` had the numbered
  `URLBlocklist` subkey with `1 = example.com/ocp-policy-test`.
- `HKLM\Software\Policies\...` and `HKEY_USERS\<Ziur SID>\Software\Policies\...`
  had **no** `URLBlocklist` value or subkey.
- Deleting the Brave subkey was repaired within 30 seconds; the legacy
  multi-string value was removed; the server recorded
  `ClientTamperEvents.Kind = browser-policy-removed` at 20:54:33 UTC and the
  alert `Browser rules removed: ZIUR-X1-LAPTOP` at 20:54:49 UTC.
- Removing the test rule through the dashboard produced an empty policy in all
  three keys and Brave loaded the URL again.

## 2. Device came online

- New per-device `Device.NotifyOnline`, **default false** (migration
  `AddColumn defaultValue: false`).
- Devices page: **Notify me when this device comes online** /
  **Avisarme cuando este dispositivo se conecte** next to the offline switch.
  The production device card screenshot shows it off.
- `DevicePresenceService.ShouldNotifyOnline` requires the switch, a non-revoked
  device, a non-null last-seen and **at least five minutes** since the device
  was last seen. A quick SignalR reconnect is ignored. The check runs before
  LastSeen is refreshed.
- Alert type `device-online`, text `Device online: <name>` / `Dispositivo
  conectado: <name>`. It uses the existing **offline/connectivity** push
  preference and the existing offline mute preference, so push and email follow
  the parent's notification settings. The Settings/Push labels now say
  “connectivity” / “offline or online”.
- Tests cover default off, a 4-minute reconnect (ignored), a 10-minute offline
  (notified), revoked devices and a missing last-seen. No live five-minute
  outage was performed on the X1 to avoid taking the child's device offline.

## 3. Tests and build

New `Job20EBrowserRulesTests` (27 cases including the theories):
Shorts produces exactly three patterns; validation and normalization; writes
only under the child SID (never HKLM or another SID); add/remove per rule;
baseline entries are preserved and restored; removed policy is detected and
repaired; the list policy uses the numbered subkey; per-child Shorts toggle;
duplicate/invalid rules; DTO carries the policy; device-online gating;
unsupported-browser matching; en/es-MX strings.

Full runs:

- Linux SDK 10.0.402: `Passed! Failed 0, Passed 469, Skipped 0`.
- Dell XPS, clean extraction of the final commit:
  `Passed! Failed 0, Passed 469, Skipped 0`.
- The XPS built the client, helper, Luna, MSI and setup from `b85fd07` with WiX
  5.0.2 (`msiexec` not needed for the build). The final build artifacts' hashes
  are in the header.

## 4. Release v0.17.20 (final)

- `Directory.Build.props` **0.17.20**; release notes
  `.github/release-notes/v0.17.20.{en,es}.md`; annotated tag **v0.17.20** on
  **`b85fd07`** (tag moved from the first build after the registry fix).
- Web image:
  `ghcr.io/ziur-vela/parental-opcontrol@sha256:904804f7a916d16f6b1d6a4d68cd2e9dd4ef5e9a84acd46a61fc447dc837db6a`
- Updater image:
  `ghcr.io/ziur-vela/parental-opcontrol-updater@sha256:aca34e0f3459d4fa7d001eae8240848c1edc2a25a9bd97c1243273b7d188a78b`
- MSI `OpControlParental-0.17.20.msi` **143,937,536** bytes SHA-256
  `376f3199334107dff80b2fa8aec450201d0e7ee99db4b6ad3162b1b541260f6b`.
- Setup `OpControlParental-Setup-0.17.20.exe` **261,231,444** bytes SHA-256
  `c78dcb47154a6b9d2a41967f6ed998c5712ae2872efd12ac856d8c594317d6c3`.

### Staging

- Manual pre-update backup:
  `/opt/stacks/opcontrol-parental-staging/backups/parental-pre-job20e-20261101T201116Z.sql.gz`
  (5,610,568 bytes; `gzip -t` OK; dump complete = 1).
- First update: 0.17.19 → 0.17.20 (old build); updater backup
  `/data/updater/backups/parental-pre-update-20261101T202046Z.sql`
  (12,519,033 bytes).
- Final update: 0.17.20 → 0.17.20 (fixed build); updater backup
  `/data/updater/backups/parental-pre-update-20261101T204919Z.sql`
  (12,735,914 bytes).
- `/version` **v0.17.20 · build 2026-11-01 · b85fd07**; `/health` **200**;
  migrations **23**.
- The old 0.17.20 catalog files were moved aside and the new baked assets were
  published: MSI `376f3199…` and Setup `c78dcb47…`, matching the GitHub release.
- `.env` pinned to the final digests (backups
  `.env.bak-job20e-20261101T201116Z`, `.env.bak-job20e-pin-20261101T201116Z`,
  `.env.bak-job20e-pin2-20261101T205100Z`); `docker compose config -q` OK.

### Production

- Manual pre-update backups:
  - `/opt/stacks/opcontrol-parental/backups/parental-pre-job20e-20261101T201116Z.sql.gz`
    (**6,573,235** bytes; `gzip -t` OK; dump complete = 1);
  - `parental-keys-pre-job20e-20261101T201116Z.tgz` (**714** bytes);
  - `docker-compose.yml.bak-job20e-20261101T201116Z` and
    `.env.bak-job20e-20261101T201116Z`.
- Updater backups:
  - first update `/data/updater/backups/parental-pre-update-20261101T202203Z.sql`
    (**18,136,004** bytes);
  - final update `/data/updater/backups/parental-pre-update-20261101T205028Z.sql`
    (**18,367,370** bytes).
- `/version` **v0.17.20 · build 2026-11-01 · b85fd07**; `/health` **200**;
  migrations **23**.
- Catalog: `data/releases/OpControlParental-0.17.20.msi` `376f3199…` and
  `OpControlParental-Setup-0.17.20.exe` `c78dcb47…`, matching the release manifest.
  The old files were renamed `.old-20261101`.
- `.env` pinned to the final digests; `docker compose config -q` OK.
- Post-release production state: 1 family, 1 child, 2 devices; X1 **0.17.20**
  healthy; Yoga **0.17.19** (not touched); categories unchanged
  (adult/ai/fakenews/gambling/social on); `BrowserPageRules` **0** and
  `ChildBrowserSettings` **0**; `NotifyOnline` **false** for both devices;
  `NotifyOffline` unchanged (false); 0 active remote sessions; no owner or child
  login was used.

### X1 (only)

- The X1 was on 0.17.20 from the first build and the normal same-version update
  is skipped by the client's “not newer” rule. The **final 0.17.20 MSI** was
  downloaded from the GitHub release, SHA-256 checked against `376f3199…`
  (143,937,536 bytes) and installed with `msiexec /i … /qn /norestart`; exit
  **0**. No data was removed.
- Installed versions: Client **0.17.20.0**, Helper **0.17.20.0**, Luna
  **0.17.20.0**; service **Running / Automatic**; update log shows the earlier
  catalog update to 0.17.20.
- The fixed client migrated the Chrome/Edge keys from the legacy multi-string to
  numbered subkeys and removed the legacy value. The temp rule was removed and
  all three browser keys are now empty; the final state has no `URLBlocklist`
  value or subkey in Emma's hive.
- Tamper test: deleting the Brave subkey was repaired and produced
  `browser-policy-removed` plus the dashboard alert; the test event and alert
  were deleted afterwards.
- Brave policy evidence: `docs/job-20e-screens/x1-brave-policy.png` (URLBlocklist
  from Platform). Blocked navigation returned `net::ERR_BLOCKED_BY_ADMINISTRATOR`
  while `https://example.com/` returned 200.

### Yoga — not touched

`EMMA-YOGALAPTOP` remains **0.17.19**, last seen 2026-10-31 22:37:48 UTC. It was
not enrolled, updated, tested or troubleshot in this job.

## 5. Screenshots

`/srv/opcontrol-parental/docs/job-20e-screens/` and repo
`docs/job-20e-screens/`:

- `browser-rules-card-{dark,light}-{en,es}.png` — the card, four combinations;
- `browser-rules-{dark,light}-{en,es}.png` — full Websites page at 1440 px;
- `browser-rules-phone-{dark,light}-{en,es}.png` — 390 px;
- `prod-browser-rules.png` — production card after cleanup (empty, Shorts off);
- `prod-devices-notify-online.png` — production X1 device card with the new
  online switch off;
- `x1-brave-policy.png` — X1 Brave policy page showing URLBlocklist;
- `index.html`.

The card and full-page screenshots are real headless Chromium captures of the
local release preview with demo data (Ana), build `b85fd07`; they show the
Shorts toggle on for illustration only. The production card shows the real
final state with the toggle off.

## 6. Cleanup

- Production temporary parent `job20e-admin@opcontrol.local` deleted; the
  test `BrowserPageRules` row, test `browser-policy-removed` tamper event and
  its alert were deleted. 0 `job20e-%` users remain.
- X1 temporary scheduled task `Job20EVerify`, `C:\Temp\Job20E` (scripts, Brave
  profiles, downloaded MSI) and the temporary policy were removed; the service
  is running.
- XPS build folders `C:\ocp-build\parental-20e` and `parental-20e-fix`, their
  zip archives and `C:\Temp\Job20E` were removed.
- Local preview server and PostgreSQL container removed; local temp scripts and
  archives removed.
- No owner or child login, no PIN/password/family/display-scaling/Windows
  setting change, no Luna change, no Yoga action, 0 active remote sessions.
- The temporary `.old-20261101` catalog files are kept next to the releases for
  rollback reference.

## 7. Limits / notes

- Chrome and Edge are not installed on the X1, so only Brave was launched for
  the live policy check. Chrome and Edge support user-level policy on Windows
  and received the same numbered subkey; the report marks them as written and
  not live-verified.
- Firefox does not read user-level policy on Windows. The product deliberately
  does not write HKLM, so Firefox is not changed and the dashboard says so.
- No live five-minute offline test for the online notification; the logic and
  the five-minute boundary are covered by tests. No live uninstall test; the
  uninstall cleanup path is implemented and the manager's removal behaviour is
  unit-tested.
- The v0.17.20 tag was moved after the first build to include the registry
  encoding fix. The final release assets, catalog and X1 all use `b85fd07`; the
  first build's images and MSI are no longer in the catalog.
- The screenshots are headless Chromium at 1440 px and 390 px, not a physical
  phone.
