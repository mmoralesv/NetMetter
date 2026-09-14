# Microsoft Store listing — NetMetter

Paste-ready copy for the **Store listing** and **Properties** steps in Partner Center.
Fields are grouped as they appear there. Replace anything in `{curly braces}` first.

> Publisher: **JEMA.Tech** · Store ID: **9PHLZGZZN7CB** · Package identity: **JEMA.Tech.NetMetter**
>
> Still to fill in:
> - `{support-email}` — a monitored support address (also used in the privacy policy).
>
> Hosted (GitHub Pages) once the Pages workflow runs on `main`:
> - Privacy policy: `https://mmoralesv.github.io/NetMetter/privacy.html`
> - Website / landing: `https://mmoralesv.github.io/NetMetter/`

---

## Properties

- **Category:** Utilities & tools
- **Subcategory:** _(leave as "None" / not applicable)_
- **Price:** Free
- **In-app purchases:** None
- **Markets:** All markets
- **This app requires:** No special hardware
- **Accessibility:** Not tested for accessibility compliance _(uncheck the compliant box)_

---

## Product name

```
NetMetter
```

## Short title (optional, ≤ 50 chars)

```
NetMetter
```

## Short description (used in search results and previews, ≤ 270 chars)

```
See your live upload and download speed right on the taskbar. NetMetter shows the real-time throughput of every connected network adapter — Ethernet, Wi-Fi, VPN and more — in a compact readout or a floating window. Free, lightweight, and completely private.
```

## Description (≤ 10,000 chars)

```
NetMetter puts your network speed where you can always see it.

It shows the live upload and download speed of every connected network interface — Ethernet, Wi-Fi, VPN, and more — as a compact readout on your taskbar, or as a small floating window you can place anywhere on screen. No dashboards to open, no windows to dig for: just a glance tells you what your connection is doing right now.

WHERE IT LIVES
• Taskbar mode — a clean readout drawn next to the clock, updated every second. Drag it anywhere along the taskbar and it stays put.
• Floating mode — a small always-on-top panel you can move to any corner of any monitor.
You choose which one you want the first time you run it, and can switch anytime from the menu.

WHAT IT SHOWS
• One column per connected adapter, with separate upload (↑) and download (↓) figures.
• Hover any column for the full picture: adapter description, link speed, IP addresses, and total data sent and received.
• Switch between bytes (KB/s, MB/s) and bits (Kbps, Mbps).
• Show or hide adapter names for a compact two-line layout.

BUILT FOR WINDOWS 11 AND 10
• Follows your light or dark theme automatically.
• Sharp at any display scaling, and works with the taskbar on any edge of the screen.
• Leaves room for the Windows 11 Widgets button instead of covering it.
• Gets out of the way — it hides itself while a full-screen app or game is running.

MADE TO STAY OUT OF YOUR WAY
• Pick exactly which adapters to display. By default NetMetter shows only the ones that actually reach a network, so virtual adapters (Hyper-V, VirtualBox, and the like) stay hidden until you want them.
• Choose how often it updates — from twice a second to once every five seconds.
• Start it automatically when you sign in, if you like. It's off by default; you decide.

COMPLETELY PRIVATE
NetMetter reads network information locally, on your PC, to display it. That's all. It sends nothing anywhere — no analytics, no advertising, no accounts, no tracking of any kind. It doesn't even request network access. The only thing it stores is its own settings, on your device.

NetMetter is a small, focused utility that does one thing well. Pin your network speed to your desktop and get on with your day.
```

## What's new in this version (release notes)

```
First release of NetMetter.
• Live upload/download speed for every connected network interface.
• Taskbar readout or floating window — your choice.
• Hover for adapter details, link speed, IP addresses and totals.
• Bytes or bits, adjustable update interval, per-adapter visibility.
• Light/dark theme aware, high-DPI ready. Private by design — nothing leaves your PC.
```

## Product features (short bullets, up to 20; ~200 chars each)

```
Live upload and download speed for every connected network adapter
Show it on the taskbar or in a floating always-on-top window
Hover for link speed, IP addresses, and total data sent and received
Switch between bytes (KB/s, MB/s) and bits (Kbps, Mbps)
Choose which adapters to show; virtual adapters hidden by default
Adjustable update interval, from 0.5 to 5 seconds
Follows your light/dark theme and scales with your display
Private by design: reads locally, sends nothing, no ads or tracking
```

## Search terms (up to 7; each unique, relevant, no other product names)

```
network speed
bandwidth monitor
internet speed
taskbar meter
upload download
data usage
network monitor
```

## Copyright and trademark info (optional)

```
© 2026 JEMA.Tech
```

## Additional license terms (optional)

```
(leave blank, or link to a LICENSE if you add one to the repo)
```

## Developed by

```
JEMA.Tech
```

---

## Store listing → additional fields

- **Privacy policy URL:** `https://mmoralesv.github.io/NetMetter/privacy.html`
- **Website (optional):** `https://mmoralesv.github.io/NetMetter/`
- **Support contact info:** `{support-email}`

---

## Screenshots (Store listing → Screenshots)

Desktop, PNG, ≥ 1366×768, up to 10 (aim for 4). Suggested set and captions:

1. **Taskbar mode, one adapter** — "Your live network speed, right on the taskbar."
2. **Taskbar mode, multiple adapters** — "One column per connection — Ethernet, Wi-Fi, VPN and more."
3. **Hover tooltip** — "Hover for link speed, IP addresses, and total data transferred."
4. **Floating window** — "Prefer a floating readout? Put it anywhere on any monitor."
5. **Right-click menu** _(optional)_ — "Bytes or bits, which adapters to show, how often to update — your call."

> Capture at 100% scale on a clean desktop. Keep key content in the top two-thirds
> (the Store may overlay text on the bottom third). No added logos or marketing text.

## Store logo (Store listing → Store logos)

- **1:1 app tile icon, 300×300:** `packaging/Store/AppTile300.png` (already generated).

---

## Submission options → notes

### runFullTrust justification / certification notes

```
NetMetter is a Win32 desktop taskbar utility, packaged as MSIX. It reads per-adapter
byte counters and adapter properties (name, link speed, IP addresses) via standard
.NET networking APIs to display upload/download speed. The runFullTrust restricted
capability is required because it is a packaged desktop (Win32) application.

In taskbar mode it draws a small, always-on-top layered window over the taskbar next
to the notification area; the user consents to this on first run and can instead choose
a floating window. It uses only documented APIs — no accessibility (UI Automation) or
undocumented APIs — and requests no network capability, so it sends no data off the device.

To test: launch the app, accept the first-run dialog, and the meter appears with live
speeds. No sign-in or server is required.
```
