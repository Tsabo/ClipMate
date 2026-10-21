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
- announces this Device on the local network about every 30 seconds (UDP port 47824), so your other Devices can find it;
- adds a Windows Firewall rule for ClipMate. Windows asks for administrator permission to add it.

Only Devices you have paired with can connect. Turning networking off stops the listener and the announcements.

## This Device

| Setting | Description |
|---------|-------------|
| **Device Name** | The name your other Devices see for this Device. Defaults to the computer name. |
| **Device ID** | This Device's unique fingerprint, shown in groups of four characters. Use **Copy** or **QR Code** to give it to another Device when adding it manually. |

The Device ID never changes unless the identity file is deleted, in which case the Device gets a new Device ID and has to be paired again.

## Discovered Devices

While networking is on, the **Discovered Devices** list shows the other ClipMate Devices on your local network that also have networking turned on, with their Device Name and the first characters of their Device ID (hover to see the full Device ID). A Device appears within about 30 seconds of turning networking on, and drops off the list about a minute and a half after it stops announcing itself.

Each announcement is signed with the Device's key, so another computer cannot pretend to have one of your Device IDs. Seeing a Device in this list does not let it connect: only Devices you have paired with can.

A Device that is already one of your Peers shows **Paired**. A Device running an incompatible version of ClipMate shows **Update required** and cannot be paired until ClipMate is updated.

## Pairing

Pairing makes two of your Devices trust each other. It completes only once **both** Devices have accepted.

1. On one Device, click **Pair...** next to the other Device in **Discovered Devices**. A pairing dialog opens and the request is sent.
2. The other Device shows a **Pairing request** notification. Click it to open the same pairing dialog. If you leave the notification, the dialog opens by itself; closing the notification declines the request.
3. Both dialogs show the same **Verification Code**, such as `K7QM-2XPA`, worked out from both Device IDs. Check that the codes match. If they don't, click **Decline**: you are not pairing with the Device you think you are.
4. On each Device, optionally change **Show as**, the name this Device uses for the other one (its Peer Alias, which only you see; it starts as the other Device's Device Name), then click **Accept**.

Declining on either Device, or not answering within two minutes, leaves both Devices unpaired.

### Add Device manually

To pair with a Device that is not on your local network (on another subnet, through a VPN, or over the internet), click **Add Device manually...** under **Discovered Devices** and enter:

- **Address**: the other Device's host name or IP address, for example `192.168.5.20`, `office-pc.example.com` or `fe80::1`. You can also type the port after it, as in `office-pc.example.com:50123`;
- **Port**: the port the other Device listens on (its **Port** setting, default 47823);
- **Device ID**: the other Device's Device ID, from its Devices tab (**Copy** it there). Dashes, spaces and letter case don't matter.

An address alone is never enough: ClipMate connects only if the Device at that address proves it has exactly that Device ID. If it has a different one, the connection is refused and the pairing dialog says so; check the address and the Device ID.

Click **Pair...** and the same pairing dialog opens as for a Discovered Device, with the same Verification Code check on both Devices. Until the other Device answers, the dialog names it by its address; once it accepts, **Show as** is filled in with its Device Name. Networking must already be turned on (click **OK** after turning it on) before you add a Device.

The address you entered is remembered as one of the Peer's known addresses, so ClipMate can reach the Peer there again later.

## Peers

The **Peers** list shows the Devices you have paired with: the name you gave it in **Show as** (or its Device Name), its own Device Name, the first characters of its Device ID and its status:

- **Online**: the Peer is reachable now.
- **Offline (last seen ...)**: the Peer could not be reached; shows when it was last reached.
- **Update required**: the Peer runs an incompatible version of ClipMate. Update ClipMate on both Devices.

While networking is on, ClipMate checks each Peer about every 30 seconds. Peers are saved in `clipmate.toml` and never expire.

Only Peers can connect to this Device. Any other Device is refused while the encrypted connection is being set up, even if it knows the right address and port. The only thing it can do is send a pairing request, which you have to accept.
