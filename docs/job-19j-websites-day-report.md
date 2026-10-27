# Job 19j — Websites day view, subdomains and per-device offline alerts (v0.17.15)

**Status: complete and released.** v0.17.15 is on staging, production and **X1**.
**Yoga is still offline** (last seen 2026-10-27 05:19 UTC on 0.17.13; Tailscale
and TRMM both down, and magic packets to both of its MACs did not wake it), so it
will take 0.17.15 from the normal catalog when it is next online/restarted.
Production was read-only for this job: all production statements were `SELECT`s;
the only writes were the release migration and the updater itself.

- Branch: **`fix/job19j-websites-offline-0.17.15`** from `main` (`4b08076`, v0.17.14).
- Commit: **`47f1e4e`** — “Job 19j: family-day websites, subdomains and offline setting”;
  `main` fast-forwarded to it; tag **v0.17.15**.
- Release workflow: **40540691231**, success; published **2026-10-27 16:32:37 UTC**.
- Tests: **402/402 Linux SDK 10.0.402** and **402/402 Dell XPS**; full Windows
  solution build **0 errors / 0 warnings**.
- Build host: **Dell XPS** (`Ziur-Dell-XPS`, `100.98.112.35`), SDK 10.0.402.
- Database migration: **`20261027161317_Job19jWebsitesOffline`** (additive).

## 1. Causes

### Wrong day / empty days / click-order dependent

- `Websites.razor` started with `_date = DateTime.Today` and set it again in
  `OnInitializedAsync`. That is the **server's** date (UTC in production).
  Hermosillo is UTC-7, so at 18:57 local on 26 Oct the server was already on
  27 Oct and the picker opened on “tomorrow” (10/27), with no visits.
- The day start/end were converted with the family zone, but the selected date
  was read **after several awaits** (the visits query itself), not captured when
  the request started. Rapid ‹ / date-picker / › changes started overlapping
  requests with no cancellation or generation guard, so an older request could
  finish later and overwrite the list for the newly selected day. That is why the
  same 10/26 or 10/25 click could show different results depending on click order.
- The visit at 18:48 local on 26 Oct is 01:48 UTC on 27 Oct. With a UTC-selected
  10/26 window it falls outside; with the previous 10/25 window it appears, which
  matches “10/25 shows a list whose entries say last 26 Oct, 6:48 PM”.

Screen time had the same default: `_anchor = DateTime.Today` (UTC). Home,
Activity and Apps already used `UsageService.LocalDayBounds`/device-zone display
for their today buckets, so only the Websites and ScreenTime day anchors needed
changing.

### No refresh

There was no Refresh button and no timer on Visited sites. The only way to get
new visits was to navigate away/change the day.

### Subdomains hidden

The Windows client threw the queried host away: `DnsProxy` called
`DomainUtil.GetRegistrableDomain(q.Name)` and passed only that to
`DataSyncService.RecordVisit` / `VisitEventDto` / `VisitRecord`. The server never
received `search.brave.com` or `talk.brave.com`, only `brave.com`, so the page
could only group by the main domain. The client now reports **both** the full
host (`Host`) and the registrable domain (`Domain`).

### Device offline alerts

`UsageLimitService.CheckOfflineAsync` alerted on every non-revoked device whose
`LastSeenUtc` exceeded the family threshold. There was no per-device opt-out, so
laptops that are off most of the day produced noisy “device offline” pushes and
emails.

## 2. Fix

### One family time zone on Websites (and ScreenTime)

- `OnInitializedAsync` no longer sets the date. `RefreshAsync` loads the child's
  device time zone first and, when `_date` is still unset, sets it to that zone's
  **today**: `DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(UtcNow, tz))`.
- The selected date is captured at the start of the request and the day bounds
  use the new `UsageService.LocalDayBounds(tz, DateOnly)` overload (same family
  zone as the picker and the displayed times).
- ScreenTime `_anchor` is now set to the family-local today after its time zone
  is loaded; `SetPeriod` and `ShiftPeriod` use the same helper. Home/Activity/Apps
  already used `LocalDayBounds`.

### No stale results

New `LatestRequestGate` in `WebsiteDayView.cs` is a monotonic token. Every
refresh takes `Next()` at the start and only applies its results if
`IsCurrent(token)` after all queries. The selected-days/visits/rules are read
into locals and only written to the page state when current, so an older day can
no longer overwrite a newer one.

### Refresh

- A `⟳` Refresh button (`Common.Refresh`) sits next to the day arrows.
- `PeriodicTimer(30 s)` runs a quiet refresh while the page is open. Quiet
  refreshes do not show the skeleton and do not touch `_filter` or the expanded
  subdomain state.
- Scroll position is preserved with `ocpVisitsScrollTop` / `ocpVisitsScrollSet`
  and restored in `OnAfterRenderAsync`; row identity uses `@key`, so Blazor only
  patches changed rows.

### Subdomains

- `VisitEventDto` has an optional `Host`; the client queue records the full host.
  `VisitRecord` has a nullable `Host` (older rows fall back to `Domain`).
  `SyncVisitsAsync` groups/dedupes by host, keeps `Domain` as the registrable
  domain, and resolves the block reason with the host (so an exact subdomain
  rule is found).
- New `WebsiteDayView.Build` groups one day by registrable domain:
  - main row: total count, latest visit, total blocked attempts, effective rule;
  - `Subdomains`: every visited host under it, with its own count, latest visit,
    blocked attempts, effective rule, and whether it has its own rule.
  Rules resolve closest-parent-first (exact host, then parents), so a subdomain
  rule affects that host and its own subdomains only.
- The page shows a small “n subdomains” / “1 subdomain” toggle on each main row.
  Expanding shows the hosts with Review / Block / Allow. Main rows keep totals.
- Subdomain Block/Allow call `UpsertWebsiteRuleAsync(..., exactHost: true)`.
  `NormalizeHost` preserves `www.` for exact-host rules; normal domain entry
  still strips `www.` as before. The Review panel for a subdomain uses the exact
  host for its Block/Allow actions.

Staging check with synthetic hosts on the 27th: the date input defaulted to
`2026-10-27` (family local, not UTC tomorrow), the brave.com row showed
“2 subdomains” with total 4 visits, and www.example.com produced “1 subdomain”.
Synthetic rows and the temporary staging parent were deleted afterwards.

### Offline alerts

- New `Device.NotifyOffline` (default `true` for new rows) and the Devices page
  checkbox **Notify me when this device goes offline** /
  **Avisarme cuando este dispositivo se desconecte**.
- Enrollment sets it **off for Windows and Linux**, **on for Android**.
  Migration `20261027161317_Job19jWebsitesOffline` sets existing computers to
  false (`UPDATE Devices ... WHERE Platform IN ('Windows','Linux')`), leaving
  phones as they were. Production rows after migration: X1 `f`, Yoga `f`.
- `UsageLimitService.ShouldNotifyOffline` gates only the time-since-`LastSeen`
  alert. The dashboard still shows online/offline and last seen from
  `Presence`/`LastSeenUtc`.
- **Tamper is separate:** service-stopped, DNS-changed and uninstall-attempt
  alerts come from `/api/device/tamper` → `RuleService`/`Program` → `AlertService`
  with type `tampering`. They never read `NotifyOffline` and are unchanged; the
  test suite checks that the tamper endpoint has no `NotifyOffline` reference.

## 3. Tests

New `tests/OpControl.Parental.Tests/Job19JWebsitesOfflineTests.cs` (10 cases):

- family UTC-7 day bounds around local midnight, 17:00 local (= 00:00 UTC on
  the 27th), 18:48 local and 23:59 local; “today” at 18:57 local is still the 26th;
- older request token is ignored after a newer one starts;
- subdomain grouping: `search.brave.com`, `talk.brave.com`, `brave.com` grouped
  under `brave.com` with correct totals and per-subdomain blocked state;
- subdomain allow does not change the main row or sibling;
- `exactHost` rule keeps `search.brave.com` / `www.example.com` exact while
  `NormalizeDomain` still strips `www.`;
- `SyncVisitsAsync` stores both `Domain=brave.com` and the full `Host`;
- offline alert suppressed for Windows with the setting off, still sent when on,
  still sent for Android; enrollment defaults computers off / phones on; tamper
  path independent;
- source checks for the family-zone bounds, request gate, 30 s quiet timer,
  Refresh button, scroll restore and subdomain grouping.

## 4. Release v0.17.15

- `Directory.Build.props` **0.17.15**; notes
  `.github/release-notes/v0.17.15.{en,es}.md`; annotated tag **v0.17.15**.
- Workflow **40540691231** success; published 16:32:37 UTC.
- Web image:
  `ghcr.io/ziur-vela/parental-opcontrol@sha256:6b38cc0f194498d3d94739170535da5448337eb794e3f76bdaec99c832deaae9`
- Updater image:
  `ghcr.io/ziur-vela/parental-opcontrol-updater@sha256:7b6956bc8708e34f05d494ca9e0011a1476afe637d221be4c81bc5abe1cecc20`
- MSI `OpControlParental-0.17.15.msi` **143,880,192** bytes SHA-256
  `5dd5bacfd47f89759d870274cb46c2d80a9008f73b879d8028bea433f0fd5b8b`.
- Setup `OpControlParental-Setup-0.17.15.exe` **261,170,004** bytes SHA-256
  `f02f2254c650111859f708850cdd36e22443e321d0c7ca9b6c813ea7d0c4044f`.
- Production catalog `data/releases` has both 0.17.15 files with matching
  SHA-256; 0.17.14 remains as the previous release.

### Staging

- 0.17.14 → 0.17.15 success; updater backup
  `/opt/stacks/opcontrol-parental-staging/data/updater/backups/parental-pre-update-20261027T163325Z.sql`
  (12,102,110 bytes).
- `/version` **v0.17.15 · build 2026-10-27 · 47f1e4e**; `/health` **200**;
  web healthy.
- Migration applied; `VisitRecords.Host` and `Devices.NotifyOffline` present.
- `.env` pinned; backup
  `/opt/stacks/opcontrol-parental-staging/.env.bak-job19j-20261027T162443Z`;
  `docker compose config -q` OK.

### Production

- Manual pre-update backup:
  `/opt/stacks/opcontrol-parental/backups/parental-pre-job19j-20261027T162443Z.sql.gz`
  (**5,991,308** bytes; gzip OK; “PostgreSQL database dump complete” = 1),
  plus `docker-compose.yml.bak-job19j-20261027T162443Z`,
  `.env.bak-job19j-20261027T162443Z`, and
  `parental-keys-pre-job19j-20261027T162443Z.tgz` (key XML).
- Updater backup
  `/opt/stacks/opcontrol-parental/data/updater/backups/parental-pre-update-20261027T163426Z.sql`
  (15,336,281 bytes).
- `/version` **v0.17.15 · build 2026-10-27 · 47f1e4e**; `/health` **200**;
  `/luna` **302**; web container healthy; migrations **21** (latest
  `20261027161317_Job19jWebsitesOffline`).
- `.env` pinned; `docker compose config -q` OK.
- Data unchanged: Luna enabled, night, daily limit 60; `adult`, `ai`,
  `fakenews`, `gambling`, `social` all true; manual lock false; 0 active remote.
- Devices after migration: X1 `NotifyOffline=f`, Yoga `NotifyOffline=f`.

### Laptops (normal catalog)

- **ZIUR-X1-LAPTOP** (`89f9db76-3e1c-458a-8053-44cf26df451e`): service restart
  triggered the normal catalog check; update log **16:36:02 → 16:36:35 UTC**,
  `msiexec` exit 0, new service healthy. Client/Helper/Luna **0.17.15.0**,
  service Running/Automatic, one Luna in session 1 (`Sleep`), dashboard
  **0.17.15 healthy**. Kid theme `night`, avatar unchanged; no sleep-scale file
  (default 1.6). No new errors in the Luna log.
- **EMMA-YOGALAPTOP** (`d3b94f63-678b-492e-83d6-19f3036b89f8`): still **offline**
  (last seen 05:19 UTC on 0.17.13; Tailscale and TRMM down; magic packets to
  Wi-Fi `7C:B0:C2:18:C3:E2` and Ethernet `54:AB:3A:B9:B5:28` did not wake it).
  0.17.15 is in the catalog; it will update on its next check/restart. Its
  `NotifyOffline` is already false from the migration and its sleep scale 1.9
  remains untouched.

## 5. What Ziur should check by hand

On X1 (v0.17.15, Hermosillo time):

1. Open **Websites → Visited sites**. The date picker must open on the **local**
   day (not tomorrow), and 26 Oct must show the 6:48 PM visit when selected.
2. Move quickly between ‹ / › and dates; the list must always match the last
   date chosen (no flashback to an older day).
3. Leave the page open; new visits should appear within 30 s without the list
   flashing, and the scroll/filter must stay.
4. Find a domain with subdomains (e.g. `brave.com`), open the “n subdomains”
   toggle, and confirm `search.brave.com` / `talk.brave.com` rows appear with
   their own counts and Review / Block / Allow.
5. Block a subdomain; confirm only that host and its own subdomains are blocked,
   the main row still shows the totals, and Allow/Unblock works.
6. On **Screen time**, the day view must also open on the local today.
7. On **Devices**, confirm “Notify me when this device goes offline” is off for
   X1 and Yoga and on for any phone. Turning it on/off should save.
8. After Yoga is powered on, confirm it reaches 0.17.15 and its sleep scale 1.9
   is kept.

## 6. Cleanup / notes

- XPS `C:\ocp-build\parental-19j` and `parental-19j.tgz` removed after the
  Windows suite/build; no XPS tasks or scripts left.
- Local transfer archives, temporary scripts and the staging test HTML removed.
- Staging synthetic visits and temporary staging parent deleted (0 remaining).
- No production INSERT/UPDATE/DELETE by this job (only the release migration and
  updater); no owner/child login; no PIN/password/family/DOB/Windows setting
  change; no OC git identity change; Tailscale untouched.

## Limits

- Yoga could not be updated or verified (offline); its 0.17.15 install is the
  only open item.
- The live staging page was checked from the prerendered HTML (date default,
  main totals, subdomain toggles); the expand animation itself was not clicked
  in a browser. The grouping and exact-host rules are covered by tests.
- Offline notification suppression applies to the `device-offline` alert only;
  tamper alerts are intentionally unaffected (see §2).
