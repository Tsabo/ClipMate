# Screenshots captured by hand

The `Docs-Screenshots` target takes most of the docs' screenshots (see `build/README.md`). These are the shots it can't take. Capture only ClipMate's own window or menu where you can. When a shot needs part of the desktop, such as a menu next to the tray, crop to that area. Desktop icons must never appear in a picture, and a new virtual desktop doesn't hide them. Check the picture's edges, taskbar and tray for anything personal before saving. Capture them on an isolated copy with made-up data:

```powershell
.\build\build.ps1 -Target Smoke-Test -Configuration Debug --keep-running --data-dir C:\ClipMateDocs
```

| Shot | Goes in | Why not scripted | How to capture |
|------|---------|------------------|----------------|
| Import ClipMate 7 Database dialog, opening state | `docs/docs/coming-from-clipmate-7/img/import-clipmate-7-database.png` | On opening, the dialog reads the signed-in user's own ClipMate 7 folder (`%APPDATA%\Thornsoft Development\ClipMate7`) and shows its path and clip counts, and the File box shows the data folder's path. Neither can be redirected without an app change. | In a Windows account with no ClipMate 7 database, with the copy above: Options > Database > **Import ClipMate 7...**. Check that the paths show no real name. |
| Copy to local menu and collection window, Search on a Remote Database | `docs/docs/devices/img/` | Selecting a clip writes to the clipboard, which the target never does. | Close every other clipboard manager, pair two isolated copies with **Share with Peers** on one, and capture each window. |
| ClipMate Setup window | `docs/docs/get-started/img/setup-window.png` | It only opens when ClipMate starts without `--data-dir` and with no settings, which would use the real data folder. | Use a Windows account that has never run ClipMate. |
| Exploding PowerPaste showing the next fragment | `docs/docs/user-interface/main-toolbar/img/powerpaste-next-fragment.png` | Starting PowerPaste puts a clip on the clipboard, which the target never does. | With the copy above: copy `January, 31, February, 28`, turn on **Explode Into Fragments**, start **PowerPaste Down**, and capture the main window: "January" selected in the text pane and **[Fragment [January]]** in the title. Check the paste-target bar shows nothing personal. |
| Tray menu | `docs/docs/user-interface/img/tray-menu.png` | A new exe's icon sits in the hidden icons, where its reported position can't be clicked, and it looks the same as the icon of any other running ClipMate. Right-clicking that position opened no menu. | Close your own ClipMate. Pin the copy's icon to the taskbar, right-click it and capture only the menu. |
| Devices tab with **Discovered Devices** | Next to the page that uses it | The list shows every real Device on the network. | Use a network with no other ClipMate Devices, and two copies named "Docs Desktop" and "Docs Laptop" (Options > Devices). |
