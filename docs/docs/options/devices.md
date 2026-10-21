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
- adds a Windows Firewall rule for ClipMate. Windows asks for administrator permission to add it. Uninstalling ClipMate offers to remove the rule again (Windows asks for permission once more).

Only Devices you have paired with can connect. Turning networking off stops the listener and the announcements.

### Public networks

The firewall rule only allows connections on networks Windows treats as **Private** or **Domain**. On a **Public** network (a café, a hotel, or a home network Windows was never told to trust), your other Devices can't connect to this one and show it as offline, although this Device can still connect to them.

While networking is on and this computer is on a Public network, the Devices tab shows a warning naming the network. If you trust the network, click **Open network settings** (or go to **Settings > Network & internet**), choose your Wi-Fi or Ethernet connection and set **Network profile type** to **Private**. The warning clears by itself within a few seconds.

## This Device

| Setting | Description |
|---------|-------------|
| **Device Name** | The name your other Devices see for this Device. Defaults to the computer name. |
| **Device ID** | This Device's unique fingerprint, shown in groups of four characters. Use **Copy** or **QR Code** to give it to another Device when adding it manually. |
| **Shared with Peers** | The Databases your Peers can browse. Shows **Nothing shared yet** until you tick **Share with Peers** in a Database's Properties; until then your Peers see no collections from this Device. |

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

To keep another computer from flooding you with pairing requests, a Device answers only one request at a time from each network address, and after a request from an address is declined or goes unanswered, requests from that address are turned away for 30 seconds. The requesting Device is told the other Device is busy; wait a moment and try again.

### Add Device manually

To pair with a Device that is not on your local network (on another subnet, through a VPN, or over the internet), click **Add Device manually...** under **Discovered Devices** and enter:

- **Address**: the other Device's host name or IP address, for example `192.168.5.20`, `office-pc.example.com` or `fe80::1`. You can also type the port after it, as in `office-pc.example.com:50123`;
- **Port**: the port the other Device listens on (its **Port** setting, default 47823);
- **Device ID**: the other Device's Device ID, from its Devices tab (**Copy** it there). Dashes, spaces and letter case don't matter.

An address alone is never enough: ClipMate connects only if the Device at that address proves it has exactly that Device ID. If it has a different one, the connection is refused and the pairing dialog says so; check the address and the Device ID.

Click **Pair...** and the same pairing dialog opens as for a Discovered Device, with the same Verification Code check on both Devices. Until the other Device answers, the dialog names it by its address; once it accepts, **Show as** is filled in with its Device Name. Networking must already be turned on (click **OK** after turning it on) before you add a Device.

The address you entered is remembered as one of the Peer's known addresses, so ClipMate can reach the Peer there again later. It is kept for good: addresses the Peer is later found at on your local network never push it out, and ClipMate always tries it when it can't reach the Peer elsewhere.

## Peers

The **Peers** list shows the Devices you have paired with: the name you gave it in **Show as** (or its Device Name), its own Device Name, the first characters of its Device ID and its status:

- **Online**: the Peer is reachable now.
- **Offline (last seen ...)**: the Peer could not be reached; shows when it was last reached.
- **Update required**: the Peer runs an incompatible version of ClipMate. Update ClipMate on both Devices.

The **Shares** column lists the Databases the Peer shares with you (using any name you gave them), **Nothing shared** if it shares none, or **Not known yet** if the Peer has never been reached. A Peer that is online but shares nothing has no collections to show in the collection tree.

While networking is on, ClipMate checks each Peer about every 30 seconds. Peers are saved in `clipmate.toml` and never expire.

Only Peers can connect to this Device. Any other Device is refused while the encrypted connection is being set up, even if it knows the right address and port. The only thing it can do is send a pairing request, which you have to accept.

### Renaming a Peer

Click **Rename...** next to a Peer to change the name this Device shows for it (its Peer Alias). The alias is only stored on this Device: the Peer never sees it, and two Peers may share the same alias. Leave the name blank to go back to showing the Peer's own Device Name, which then follows any change the Peer makes to its Device Name.

### Unpairing

Click **Unpair** next to a Peer and confirm to end the Pairing. Either Device can unpair, and it takes effect for both straight away:

- the Peer leaves this Device's Peers list and can no longer connect; any open connection between the two Devices drops;
- if the Peer is reachable, it is told and removes this Device from its own Peers list too;
- if the Peer is offline, it is refused when it next tries to connect, and removes this Device from its Peers list then.

To pair again, send a new pairing request: both Devices have to accept it again.

If a pairing was interrupted so that only one of the two Devices finished it, that Device removes the other from its Peers list the next time it reaches it. Pair again to fix it.
## Shared and Remote Databases

To let your Peers browse a database, turn on **Share with Peers** in its [Database Properties](databases.md). Peers can only read it. Nothing else is shared: not your other databases, templates, shortcuts, application profiles or settings.

Each database a Peer shares with you appears at the bottom of the collection tree as a **Remote Database**, labelled "*Database* — *Device*" (the Device part uses the name you gave the Peer in **Show as**). Under it are the Peer's collections and folders, and a **Virtual** folder with its Smart and SQL-based collections, which are always worked out on the Peer.

To give a Remote Database your own name, right-click it and choose **Rename...**. The name replaces the *Database* part of the label on this Device only; the Peer never sees it, and it doesn't have to be unique. It stays when the Peer renames the database or goes offline. Leave it blank to go back to the Peer's own name for the database. Unpairing the Peer removes the names you gave its databases.

When the Peer can't be reached, its Remote Databases stay in the tree, greyed out and marked "offline · last seen ...". When the Peer stops sharing a database, or you unpair it, the database leaves the tree. A Peer running this version of ClipMate tells you straight away when it stops sharing a database; older versions are checked about every 30 seconds.

A Remote Database is never where new clips are captured, and it isn't part of your database list, backups, maintenance or purging.

### Browsing remote clips

Select a collection or folder of a Remote Database to list its clips. They load from the Peer in pages: the first clips appear straight away and the rest follow, with progress shown above the list. Select a clip to see all its formats in the viewer; large images arrive in pieces in the background.

Selecting or copying a remote clip puts it on the clipboard, ready to paste, but doesn't add it to any of your own databases.

Encrypted clips arrive from the Peer still encrypted and look just like your own encrypted clips. Enter your passphrase to view one; it is decrypted on this Device, never by the Peer.

Remote Databases are read-only. You can't edit, delete, move, rename or encrypt their clips, or decrypt them in place; ClipMate shows "Remote Databases are read-only." instead.

If the Peer goes offline or takes too long to answer, the list or viewer says so instead of holding up ClipMate. Select the collection again to retry.

While you're connected, the list keeps itself up to date: when the Peer captures, deletes, moves or edits a clip in the collection you're looking at, the list reloads from the Peer on its own, keeping the clip you had selected. The Peer only says which collection changed; the clips themselves are still fetched only when you look at them. Changes in databases the Peer doesn't share are never announced. Live updates stop when you unpair the Peer or turn networking off, and resume when a Peer that went offline comes back.

### Copy to local

To keep a remote clip, right-click it and choose **Copy to local...** (or **Copy Item To Collection**), then pick one of your own collections. You can also drag remote clips onto one of your collections in the tree; this always copies, never moves.

The copy is a new clip in your database, with all its formats and its title, capture time, creator, source URL and source application. It doesn't depend on the Peer: changes the Peer makes later, or the Peer going offline, don't affect it. An encrypted clip is copied still encrypted and opens with the same passphrase.

### Searching a Remote Database

Select a Remote Database, or one of its collections or folders, and open the Search dialog. **Search in** shows the Remote Database, and the scope list offers **All collections**, the selected **Collection** or the selected **Folder**.

The Peer runs the search itself from your criteria; no SQL is ever sent to it. It sends back up to 1,000 of the newest matching clips, and the results appear in the Remote Database's **Search Results** node. When more clips match, the list says how many, for example "Showing 1,000 of 4,212 matches"; narrow your criteria to see the rest. The results are kept on this Device only.

A few things differ from searching your own databases:

- The SQL panel is hidden, and saved searches written as SQL aren't offered.
- Deleted clips and shortcuts are never shared, so **Has Shortcut** and **Included Deleted Clips** are turned off.
- Encrypted clips match only on what isn't encrypted: creator, source URL, capture date and a title you gave the clip yourself.
- The Peer stops a search that runs for more than about 15 seconds. Closing the Search dialog stops a search that is still running.
