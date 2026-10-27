# Job 19k — Device icons, tidy Devices cards and automatic time zones (v0.17.16)

**Status: complete and released.** v0.17.16 is on staging and production. This
was a **dashboard/server-only** change: no client code changed, so no laptop
rollout step was run (the workflow still builds the usual 0.17.16 catalog
assets; X1 may pick them up on its normal 24-hour check).

- Branch: **`fix/job19k-devices-remote-0.17.16`** from `main` (`2b8c49a`, v0.17.15).
- Commits: **`a2d96f9`** (feature), **`76276aa`**, **`5ad08a7`**, **`2f7f4a0`**,
  **`e277687`**, **`05a888a`** (release fixes); `main` is at **`05a888a`**;
  tag **v0.17.16** (moved after test/visual fixes).
- Release workflow: **40553876070**, success; published
  **2026-10-27 17:55:08 UTC**.
- Tests: **414/414 Linux SDK 10.0.402** and **414/414 Dell XPS**; full Windows
  solution build **0 errors / 0 warnings**.
- Database migration: **`20261027164928_Job19kDeviceTimeZones`** (additive).
- Screenshots: `/srv/opcontrol-parental/docs/job-19k-screens/` (and repo
  `docs/job-19k-screens/`, index.html): Devices and Remote at desktop 1440×1000
  and phone 390×844, dark and light.

## 1. What changed

### Device icon and OS label (Devices + Remote)

- The green status dot is replaced by a 40 px rounded-square icon
  (`Components/Shared/DeviceIcon.razor`): a laptop outline for Windows/Linux, a
  phone outline for Android. Generic strokes only (no brand logos/mascots).
- Online: `#3ddc97` icon/ring with a soft green background. Offline: `#ff8f80`
  with a soft red background. The wrapper has `role="img"` and an accessible
  label such as “Laptop, online” / “Phone, offline”.
- A small outlined `WINDOWS` / `LINUX` / `ANDROID` label sits next to the name
  on both pages.

### Devices page (tidy cards)

- Each device is a card (`.device-card`):
  1. header: icon, name + OS label, then `{child} · client x · latest y ·
     update {status}` on one line; Online/Offline and last seen on the right;
  2. **Time zone** and **Client language** side by side (stack at phone width;
     the zone select uses the full card width and shows the full automatic
     label);
  3. checkboxes in a two-column grid: screen notice, camera notice, camera
     light/shutter, “Notify me when this device goes offline” (2×2 at desktop,
     one column on phone);
  4. **System resources** box with dashed border and an amber left-aligned
     “Coming soon” pill under the label; three 96 px, 9 px-stroke, 270° rounded
     dial gauges (CPU 42%, RAM 61%, Storage 78%) with captions. The gauges group
     is faded to 0.5 and uses placeholder values; storage is amber over 70%.
     The track is `#1f3a33`, value green (`#3ddc97`) or amber;
  5. Message button at the bottom (plus Update Now when the device is older than
     the catalog).
- The existing notice/offline/language/update behaviour is preserved.

### Remote page

- Same icon + OS label, child and online/offline text.
- **View screen** and **View camera** are disabled for offline devices, dimmed,
  and carry the tooltip “Device is offline”. Online devices stay enabled. No
  status dot remains.
- At ≤900 px the actions move to their own row under the device name (the
  desktop row keeps them on the right).

### Automatic time zones

- New device columns:
  - `ReportedTimeZoneId` — the raw id the client reports (`TimeZoneInfo.Local.Id`,
    e.g. `US Mountain Standard Time`);
  - `TimeZoneOverrideId` — an explicit parent choice, normally null;
  - `TimeZoneId` — the **effective IANA** id used everywhere.
- `DeviceTimeZonePolicy`:
  - a reported id updates `ReportedTimeZoneId`; when there is no override it
    stores `TimeZoneId = TimeZoneResolver.ToIanaId(reported)`;
  - an override stores the resolved IANA id and survives later heartbeats;
    choosing “Automatic” clears it and re-applies the reported id.
- The Devices card’s zone dropdown is **Automatic · from device: {display name}**
  by default; the other entries are explicit overrides.
- `TimeZoneResolver.ToIanaId` pins the ambiguous Windows Mexico zones so Linux
  hosts with different tzdata agree: `US Mountain Standard Time` →
  `America/Phoenix`, `Mountain Standard Time (Mexico)` → `America/Chihuahua`,
  `Central Standard Time (Mexico)` → `America/Mexico_City`. IANA inputs such as
  `America/Hermosillo` are kept. `DisplayName` returns the exact Windows label
  for the common zones (e.g. “(UTC-07:00) Arizona”) and falls back to the
  system/IANA name.
- Migration copies existing `TimeZoneId` into `ReportedTimeZoneId` and converts
  the known Windows ids (including Pacific → America/Los_Angeles and US Mountain
  → America/Phoenix). Existing overrides are null (automatic).
- **Family time zone**: `FamilySettings.TimeZoneId` is new (nullable). 
  `DashboardTime.FamilyZoneInfoAsync` uses the explicit family setting when
  present; otherwise it uses the effective zone of the family device with the
  most recent `LastSeenUtc`. It returns which device/zone is used.
  - The family settings page has a **Family time zone** card with an Automatic
    option and an explicit dropdown, and shows “Using automatic: {zone} (from
    {device})” or “Using explicit: {zone}”.
  - Websites, Screen time, Activity, Home, Apps, Messages, Alerts, Luna usage,
    invitation emails and push quiet hours now use this family zone instead of
    the first device’s zone. Per-device last-seen labels still use the device’s
    own zone.

## 2. Ziur’s Arizona check

- **ZIUR-X1-LAPTOP** reports **`US Mountain Standard Time`**; production now has
  effective `America/Phoenix`, `ReportedTimeZoneId=US Mountain Standard Time`,
  no override. This was picked up by the heartbeat after the 0.17.16 deploy
  (`LastSeenUtc 17:59 UTC`). The card shows “Automatic · from device:
  (UTC-07:00) Arizona”.
- **EMMA-YOGALAPTOP is offline** (last seen 2026-10-27 05:19 UTC, v0.17.13).
  Its stored values are still `ReportedTimeZoneId=Pacific Standard Time`,
  effective `America/Los_Angeles` from that last heartbeat. It cannot be
  confirmed that Yoga picked up the Arizona change until it is online; the next
  heartbeat will update it automatically. WOL magic packets to both its MACs did
  not wake it.

## 3. Tests

New `tests/OpControl.Parental.Tests/Job19KDevicesRemoteTests.cs` (12 cases):

- Windows→IANA mapping for US Mountain Standard Time → America/Phoenix,
  Mountain Standard Time (Mexico) → America/Chihuahua, Central Standard Time
  (Mexico) → America/Mexico_City, Pacific → America/Los_Angeles,
  America/Hermosillo unchanged, UTC unchanged;
- Windows display label contains “Arizona”;
- reported zone applies automatically, explicit override survives later
  reports, clearing the override returns to the reported zone;
- family zone is the explicit setting when set, otherwise the most recently seen
  device;
- icon/OS labels, phone detection, remote start policy;
- Remote page uses `CanStartRemote`, the offline tooltip and the icon; Devices
  page contains the automatic option, gauges, system-resources box, offline
  checkbox, two-column grid and OS label;
- Settings page exposes the family zone automatic/explicit choice and usage text.

Full suite **414/414 Linux** and **414/414 XPS**; full solution build 0/0 both.

## 4. Release v0.17.16

- `Directory.Build.props` **0.17.16**; notes
  `.github/release-notes/v0.17.16.{en,es}.md`; annotated tag **v0.17.16**.
- Workflow **40553876070** success; published 17:55:08 UTC.
- Web image (final):
  `ghcr.io/ziur-vela/parental-opcontrol@sha256:76d1a0e89ca90c17baa8c323d5fcbd5edc9b9cdb9ebc2c7bf0058f6012a4e7fa`
- Updater image (final):
  `ghcr.io/ziur-vela/parental-opcontrol-updater@sha256:5674261c4326eab32e6ee9c3e5d4bb054b49bbbf68bcf15fcd3150e288694dd7`
- MSI `OpControlParental-0.17.16.msi` **143,929,344** bytes SHA-256
  `5d3c3e5cfe3205ea28d5e13133cec4ea9309518f29642f905cdb99bec8e168b5`.
- Setup `OpControlParental-Setup-0.17.16.exe` **261,219,156** bytes SHA-256
  `38dc6fedc640dd991f577e94f47a96591dd83a56ea5627d926c0a21f40881517`.
- Catalog `data/releases` has 0.17.16; no laptop rollout step was run because no
  client code changed.

### Staging

- Multiple tag-review builds during the job; final 0.17.16 (commit `05a888a`,
  web `76d1a0e89ca9`) healthy on `:8097`; `/health` 200.
- Migration applied; `Devices.ReportedTimeZoneId`, `TimeZoneOverrideId` and
  `FamilySettings.TimeZoneId` present. `.env` pinned to the final digests;
  `docker compose config -q` OK.
- Latest updater backup:
  `/opt/stacks/opcontrol-parental-staging/data/updater/backups/parental-pre-update-20261027T175548Z.sql`
  (12,105,407 bytes).

### Production

- Manual pre-update backup:
  `/opt/stacks/opcontrol-parental/backups/parental-pre-job19k-20261027T170733Z.sql.gz`
  (**5,993,904** bytes; gzip OK; “PostgreSQL database dump complete” = 1), plus
  `docker-compose.yml.bak-job19k-20261027T170733Z`,
  `.env.bak-job19k-20261027T170733Z`, and
  `parental-keys-pre-job19k-20261027T170733Z.tgz` (key XML).
- Updater backup
  `/opt/stacks/opcontrol-parental/data/updater/backups/parental-pre-update-20261027T175810Z.sql`
  (15,380,813 bytes).
- `/version` **v0.17.16 · build 2026-10-27 · 05a888a**; `/health` **200**;
  `/luna` **302**; web container healthy; migrations **22**.
- `.env` pinned to the final digests; `docker compose config -q` OK.
- Settings unchanged: Luna enabled/night/limit 60; categories `adult`, `ai`,
  `fakenews`, `gambling`, `social` all true; `NotifyOffline=false` on both
  laptops; manual lock false; 0 active remote; 2 Luna chats.
- Production was **read-only for this job**: only SELECTs, plus the release
  migration and updater. No temp parent or test account was created in
  production.

## 5. Screenshots / staging verification

The screenshots were taken on staging against a **synthetic family** (“Job19k
Screens”, child Alex) with Windows, Android and Linux devices, so they show the
new cards without production data. Two known device tokens were connected via
SignalR to show the green online state; the Linux device was left offline to show
platform-agnostic paths. A temporary staging parent (display name Hannah) was
used to see the owner checkbox grid; it is not the real owner account. The
synthetic devices, family, temp parent and fake connections were deleted after
the shots; 0 remain.

Files in `/srv/opcontrol-parental/docs/job-19k-screens/` and repo
`docs/job-19k-screens/`: `devices-{dark,light}.png`,
`devices-phone-{dark,light}.png`, `remote-{dark,light}.png`,
`remote-phone-{dark,light}.png`, plus `index.html`.

## 6. What Ziur should check by hand

1. Devices: icons are green online/red offline, OS labels correct, all four
   checkboxes in a 2×2 grid as owner, gauges faded with “Coming soon”.
2. Remote: offline device buttons are dimmed, cannot be clicked, and show
   “Device is offline”; online ones start.
3. Devices time zone: first entry reads “Automatic · from device: (UTC-07:00)
   Arizona” for X1; no override is set. Changing it to an explicit zone and back
   to Automatic should work.
4. Settings → Family time zone: shows “Using automatic …” from the most recent
   device; choosing a family zone changes the label and the day boundaries on
   Websites/Screen time.
5. When Yoga is next online, confirm its reported zone is `US Mountain Standard
   Time` and the effective zone is `America/Phoenix`; its last report from
   26 Oct still said `Pacific Standard Time`.

## 7. Cleanup / limits

- XPS `C:\ocp-build\parental-19k` and its tgz were removed after the 414/414
  XPS run/build.
- Local screenshot scripts, fake-device scripts, SQL and temporary transfer
  files removed; staging synthetic data and temp parent deleted.
- No production temp accounts; no owner/child login; no PIN/password/family/DOB
  or Windows setting change; no OC git identity change.
- No client code changed, so laptops were not updated by this job. X1 remains
  0.17.15 but reports its Windows zone to the new server; Yoga is offline at
  0.17.13.
- The gauges are placeholders and deliberately not wired to real system data.
