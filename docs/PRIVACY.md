# NetMetter Privacy Policy

_Last updated: 11 September 2026_

NetMetter is a desktop utility that shows the upload and download speed of your
network connections. This policy explains what it does and does not do with your
information.

## What NetMetter reads

To display network speeds, NetMetter reads information about the network adapters
on the PC it runs on:

- Adapter names and descriptions (for example "Ethernet" or "Wi‑Fi").
- The IP addresses assigned to those adapters.
- The number of bytes each adapter has sent and received, sampled once per second.

This information is read locally, on your device, using standard Windows APIs. It
is shown in the meter and its tooltip.

## What NetMetter does **not** do

- It does **not** collect, transmit, or upload any of this information anywhere.
- It does **not** contain analytics, advertising, or tracking of any kind.
- It does **not** inspect the contents of your network traffic. It only reads the
  byte counters Windows keeps for each adapter.
- It does **not** create user accounts or require sign‑in.

NetMetter does not request the `internetClient` capability, so it has no network
access of its own.

## What NetMetter stores

NetMetter saves only its own settings (display mode, units, update interval, chosen
interfaces and window position) and a small diagnostic log, both on your device:

- Settings: `%APPDATA%\NetMetter\settings.json`
- Diagnostic log: `%LOCALAPPDATA%\NetMetter\netmetter.log`

When NetMetter is installed from the Microsoft Store, Windows stores these inside
the app's private storage and removes them when you uninstall the app. These files
never leave your device.

## Children's privacy

NetMetter is a general‑purpose utility and is not directed at children. It does not
knowingly collect any personal information from anyone.

## Changes to this policy

If this policy changes, the updated version will be published at the same URL and
the "Last updated" date above will change.

## Contact

Questions about this policy can be sent to: **{support-email}**

<!-- Before submitting to the Store: replace {support-email} with a real address and
     host this file at a public HTTPS URL (e.g. GitHub Pages). Put that URL in the
     Privacy policy URL field in Partner Center. -->
