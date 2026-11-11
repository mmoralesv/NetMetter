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
- [x] Unit-test project (`tests/NetMetter.Tests`, 37 tests: formatting, rate math, settings, placement).
- [ ] Fill in the support email and host the privacy policy.

## Certification

Up to 3 business days. Listing goes live ~15 minutes after it passes.

## Releasing an update (the actual process)

Automated Store publishing is **deliberately off** — see the next section for why. Releases
are made with this runbook; CI does everything except the final upload.

1. **Bump the version** in [`src/NetMetter.csproj`](../src/NetMetter.csproj)
   (`<Version>` — `MAJOR.MINOR.PATCH`; the package appends `.0`).
2. Commit, merge to `main`, then **tag it**: `git tag -a v1.0.2 -m "..." && git push origin v1.0.2`.
3. `release.yml` builds x64 + arm64, runs the tests, and attaches both artifacts to a
   **draft GitHub Release**. CI builds with the real Partner Center identity (from the
   `STORE_*` repo variables), so its `.msixupload` is submittable as-is — download it from
   the draft release, or build locally:

   ```powershell
   ./build/Package.ps1 -IdentityName "JEMA.Tech.NetMetter" `
     -Publisher "CN=33A6D027-F324-4DFC-9111-791492CD98F8" -PublisherDisplayName "JEMA.Tech"
   ```

4. **Partner Center** → NetMetter → **Start update** → **Packages** → upload
   `NetMetter_<version>.msixupload`.
   **Upload the `.msixupload`, not the `.msixbundle`** — it carries the per-architecture
   `.appxsym` symbols, which is what makes crash and hang reports resolve to stack traces
   in the Health report. The `.msixbundle` is for sideload testing only.
5. Re-use the `runFullTrust` justification in [`store/listing.md`](../store/listing.md),
   update the release notes, and submit. Certification takes up to ~3 business days.
6. When it goes live, **publish the draft GitHub Release**.

## Why automated Store publishing is off

`release.yml` and `store-listing.yml` contain working Store-publish jobs, gated on the repo
variable `STORE_PUBLISH_ENABLED`. It is **unset on purpose**, so those jobs skip cleanly and
tag builds stay green.

The blocker is account-level, not technical. The `msstore` CLI authenticates as a Microsoft
Entra application, which requires an Entra **workforce tenant** associated with the Partner
Center account. This is an individual developer account on a personal Microsoft account,
which has none, and every route to one is paywalled:

| Route | Outcome |
| --- | --- |
| Azure subscription signup | Needs a card, and Microsoft [does not accept prepaid or virtual cards](https://learn.microsoft.com/en-us/azure/cost-management-billing/troubleshoot-billing/troubleshoot-declined-card) |
| Partner Center → "Create Microsoft Entra ID" | Routes into the Microsoft 365 business signup, which ends at a mandatory "Payment setup" step |
| Entra admin center | Free/trial accounts [cannot create workforce tenants](https://learn.microsoft.com/en-us/entra/fundamentals/create-new-tenant) |
| Microsoft 365 Developer Program | Joining does not grant a sandbox; eligibility needs a qualifying paid subscription (Visual Studio Pro/Enterprise, ISV Success, …) **and a valid billing account**. Also time-limited and revocable — a bad foundation for release credentials |
| Entra External ID free trial | Creates a CIAM tenant, which Partner Center cannot use for app registration |

The trade is small: automation would save ~10 minutes per release on an app that ships
every few months, so the manual upload above is the rational choice.

**If that ever changes**, switching on takes three steps:

1. Register an Entra app in the tenant, and add it in Partner Center under
   *Account settings → User management → Microsoft Entra applications* with the **Manager** role.
2. Create a GitHub environment `store-production` (add a required reviewer) and set these
   secrets on it: `PARTNER_CENTER_TENANT_ID`, `PARTNER_CENTER_CLIENT_ID`,
   `PARTNER_CENTER_CLIENT_SECRET`, `PARTNER_CENTER_SELLER_ID`.
3. Set the repo variable `STORE_PUBLISH_ENABLED=true`.

Already configured (not secret): `STORE_PRODUCT_ID`, `STORE_IDENTITY_NAME`,
`STORE_PUBLISHER`, `STORE_PUBLISHER_DISPLAY_NAME`.

### Notes if it is ever switched on

- `msstore publish` recreates the draft — for a release that also changes the listing,
  run `msstore publish --noCommit` first, then `submission update`, then `submission publish`.
- The Store CLI's GitHub flow supports **free products only** today.
- The CLI is in preview: pin the action version; the manual runbook above stays the fallback.
- Certification is asynchronous — `msstore submission poll` waits for the result.
- A submission already in certification blocks starting another one.

## Workflows

| Workflow | Trigger | Does |
| --- | --- | --- |
| `ci.yml` | PR / push to main | build, format, tests, unsigned package smoke build — no credentials needed |
| `release.yml` | tag `v*.*.*` | build x64+arm64, test, draft GitHub Release with `.msixupload` + `.msixbundle`; Store publish job skipped unless enabled |
| `store-rollout.yml` | manual | raise / finalize / halt a gradual rollout (needs publishing enabled) |
| `store-listing.yml` | `store/listing.json` change | push listing text to the Store (needs publishing enabled) |

Distribution outside the Store (GitHub Releases, winget) would need your own code signing
(e.g. Azure Artifact Signing, from $9.99/month) — not required for Store-only.
