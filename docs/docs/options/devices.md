---
sidebar_position: 13
title: Devices Options
---

# Devices

The **Devices** tab controls peer networking: letting your own ClipMate installations (Devices) connect to each other.

## Enable Networking

**Enable networking** is off by default. While it is off, ClipMate opens no network port and sends no network traffic.

When you turn it on and click **OK**, ClipMate:

- creates this Device's identity the first time (a private key and certificate, stored encrypted for your Windows account in the ClipMate data folder, never in `clipmate.toml`);
- starts listening for your other Devices on the configured **Port** (default 47823);
- adds a Windows Firewall rule for ClipMate. Windows asks for administrator permission to add it.

Only Devices you have paired with can connect. Turning networking off stops the listener.

## This Device

| Setting | Description |
|---------|-------------|
| **Device Name** | The name your other Devices see for this Device. Defaults to the computer name. |
| **Device ID** | This Device's unique fingerprint, shown in groups of four characters. Use **Copy** or **QR Code** to give it to another Device when adding it manually. |

The Device ID never changes unless the identity file is deleted, in which case the Device gets a new Device ID and has to be paired again.
