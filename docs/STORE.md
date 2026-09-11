# Publishing NetMetter to the Microsoft Store

This is the working checklist. The full write-up with rationale is in the release-plan
artifact; this file is what lives in the repo.

## Format

Ship as an **MSIX bundle** (x64 + arm64). The Store re-signs it after certification, so
no code-signing certificate is required. Registration (individual or company) is free.

The app is published **self-contained** — an MSIX cannot depend on the .NET Desktop
Runtime, because no framework package exists for it. Expect ~55 MB per architecture.

## One-time Partner Center setup

1. Create the account at <https://storedeveloper.microsoft.com>.
   - **Individual**: government ID + selfie.
   - **Company** (required if published for/as a business, per policy 10.14): DUNS number
     or business documents + a work email; manual review takes 2–5 business days.
2. Reserve the product name. Copy **Identity/Name**, **Publisher** (`CN=…`),
   **PublisherDisplayName** and the **Store ID** from *Product management › Product identity*.
   Feed them to `build/Package.ps1` (see [packaging/README.md](../packaging/README.md)).
3. Prepare the listing:
   - Description, category **Utilities & tools**.
   - ≥ 1 desktop screenshot (4+ recommended, PNG, ≥ 1366×768). Show both display modes.
   - 300×300 app tile icon: `packaging/Store/AppTile300.png`.
   - **Privacy policy URL** (required for all Win32 apps): host [docs/PRIVACY.md](PRIVACY.md)
     at a public HTTPS URL and fill in the support email first.
   - Support contact.
4. Complete the **IARC age-rating** questionnaire (a utility with no user content rates lowest).
5. Set price **Free**; choose markets.
6. On **Submission options**, justify `runFullTrust` and write certification notes:
   > NetMetter is a Win32 taskbar utility. It reads per-adapter byte counters via
   > standard .NET networking APIs to show upload/download speed. `runFullTrust` is
   > required because it is a packaged desktop (Win32) app. In taskbar mode it draws a
   > small always-on-top overlay beside the notification area; the user consents on first
   > run and can switch to a floating window instead. It uses no accessibility or
   > undocumented APIs and sends no data over the network.
7. Upload the bundle from `build/Package.ps1` and submit. **The first submission is manual** —
   CI updates only work once the app is already live.

## Store-readiness changes (done)

- [x] Start-up via MSIX `StartupTask` when packaged, Run key otherwise (`StartupRegistration.cs`).
- [x] First-run consent + display-mode picker (`WelcomeDialog.cs`).
- [x] Floating-window mode as the low-risk alternative to the overlay.
- [x] No UI Automation; Widgets-button space derived from documented taskbar prefs.
- [x] WPF dependency removed.
- [x] Global exception handling + diagnostic log (`Program.cs`, `AppLog.cs`).
- [x] Package manifest, generated logos/`.ico`, `build/Package.ps1`.
- [x] Privacy policy (`docs/PRIVACY.md`).
- [ ] Fill in the support email and host the privacy policy.
- [ ] Add a unit-test project (recommended before wiring CI).

## Certification

Up to 3 business days. Listing goes live ~15 minutes after it passes.

## CI/CD (after the first release)

GitHub Actions on `windows-latest`, four workflows:

| Workflow | Trigger | Does |
| --- | --- | --- |
| `ci.yml` | PR / push to main | build, format, tests, pack unsigned bundle |
| `release.yml` | tag `v*.*.*` | build x64+arm64, bundle, WACK, GitHub Release, Store flight → approval → production 10% |
| `store-rollout.yml` | manual | raise / finalize / halt the rollout |
| `store-listing.yml` | `store/listing.json` change | push listing text to the Store |

Secrets (in protected `store-flight` / `store-production` environments):
`PARTNER_CENTER_TENANT_ID`, `PARTNER_CENTER_CLIENT_ID`, `PARTNER_CENTER_CLIENT_SECRET`,
`PARTNER_CENTER_SELLER_ID`. Variables: `STORE_PRODUCT_ID`, `STORE_FLIGHT_ID`.

Publishing uses the [`microsoft/microsoft-store-apppublisher`](https://github.com/microsoft/msstore-cli)
action + `msstore` CLI. Notes:

- `msstore publish` recreates the draft — for a release that also changes the listing,
  run `msstore publish --noCommit` first, then `submission update`, then `submission publish`.
- The Store CLI's GitHub flow supports **free products only** today.
- The CLI is in preview: pin the action version, keep the manual upload runbook as a fallback.
- Certification is asynchronous — poll with `msstore submission poll` in a follow-up job.

Distribution outside the Store (GitHub Releases, winget) would need your own code signing
(e.g. Azure Artifact Signing, from $9.99/month) — not required for Store-only.
