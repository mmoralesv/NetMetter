# Packaging NetMetter for the Microsoft Store

NetMetter ships as an **MSIX bundle** (x64 + arm64). The Store signs and hosts it,
so no code‑signing certificate is needed for Store submissions.

## Layout

| Path | Purpose |
| --- | --- |
| `AppxManifest.xml` | Package manifest **template** (`{Tokens}` filled in by the build script) |
| `Assets/` | Generated MSIX logos (regenerate with `dotnet run --project tools/AssetGen`) |
| `Store/AppTile300.png` | 300×300 "app tile icon" for the Store listing |
| `../build/Package.ps1` | Builds the bundle |

`Assets/` and `Store/` are generated from [`src/AppIcon.cs`](../src/AppIcon.cs) —
edit the icon there, then rerun AssetGen so the tray icon and the Store logos stay
identical.

## Build a bundle

```powershell
# From the repo root. Uses the <Version> in src/NetMetter.csproj by default.
./build/Package.ps1
```

The bundle lands in `out/NetMetter_<version>.0.msixbundle`. Upload that file to
Partner Center as‑is.

### Real identity values

For a Store build, pass the values from **Partner Center › Product management ›
Product identity** (or set them as environment variables so CI can supply them):

```powershell
./build/Package.ps1 `
  -IdentityName        "1234 Publisher.NetMetter" `
  -Publisher           "CN=ABCD1234-0000-0000-0000-0000000000AB" `
  -PublisherDisplayName "Your Publisher Name"
```

Environment variables: `NETMETTER_IDENTITY_NAME`, `NETMETTER_PUBLISHER`,
`NETMETTER_PUBLISHER_DISPLAY_NAME`.

## Sideload test build (before any submission)

The Store signs your package, but to install it locally for testing you must sign it
yourself with a certificate whose subject **exactly matches** `-Publisher`.

```powershell
# One-time: create a self-signed test certificate.
$cert = New-SelfSignedCertificate -Type Custom -Subject "CN=NetMetter Dev" `
  -KeyUsage DigitalSignature -CertStoreLocation "Cert:\CurrentUser\My" `
  -TextExtension @("2.5.29.37={text}1.3.6.1.5.5.7.3.3", "2.5.29.19={text}")

# Build and sign with it (the default -Publisher is "CN=NetMetter Dev").
./build/Package.ps1 -CertificateThumbprint $cert.Thumbprint

# Trust the certificate (elevated), then install the bundle:
#   Import-Certificate -FilePath dev.cer -CertStoreLocation Cert:\LocalMachine\TrustedPeople
Add-AppxPackage .\out\NetMetter_1.0.0.0.msixbundle
```

Check: the meter appears, the tray menu works, **Start with Windows** turns on in
Settings › Apps › Startup, settings persist, and `Remove-AppxPackage` removes it
cleanly.

## Windows App Certification Kit

Run WACK before the first submission (elevated shell, signed bundle):

```powershell
./build/Package.ps1 -CertificateThumbprint $cert.Thumbprint -RunWack
```

The report is written to `out/wack-report.xml`.

## Version numbers

Pass `MAJOR.MINOR.PATCH`; the script appends `.0` because the Store reserves the
fourth field. The first field cannot be 0, and each field must be ≤ 65535.
