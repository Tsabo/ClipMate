---
title: Network and firewall
---

# Network and firewall

Use this page when your Devices can't find or reach each other, or when you need to allow ClipMate through a firewall yourself.

## Ports

| Port | Used for | Setting |
|------|----------|---------|
| TCP 47823 | Connections between paired Devices | **Port** on the [Devices](../options/devices.md) tab |
| UDP 47824 | Finding other Devices on the local network, with an announcement about every 30 seconds | Fixed |

## Windows Firewall

When you turn on networking, ClipMate adds an inbound Windows Firewall rule named **ClipMate Peer Networking** for its own program file. Windows asks for administrator permission. If you decline, ClipMate still runs, but your other Devices may not be able to connect to this one.

Uninstalling ClipMate offers to remove the rule.

## Public networks

The firewall rule only allows connections on networks Windows treats as **Private** or **Domain**. On a **Public** network, such as a café or hotel, your other Devices can't connect to this one and show it as offline, although this Device can still connect to them.

While networking is on and this computer is on a Public network, the **Devices** tab shows a warning naming the network. If you trust the network:

1. Click **Open network settings**, or go to **Settings > Network & internet**.
2. Choose your Wi-Fi or Ethernet connection.
3. Set **Network profile type** to **Private**.

The warning clears by itself within a few seconds.
