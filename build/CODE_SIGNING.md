# Code Signing

Releases are signed with a Certum code signing certificate (Jeremy Brown, issued by Certum Code Signing 2021 CA). The private key lives in Certum's cloud, so it is reached through **SimplySign Desktop**. Once SimplySign Desktop is logged in, the certificate appears in the Windows certificate store and `signtool` can select it by thumbprint.

## What gets signed

The `Release` target signs, in order:

1. **`Sign-Binaries`** signs everything in `build/publish/Release` that is either a ClipMate file (`ClipMate.*`) or has no valid signature. Third-party files that are already signed, such as Microsoft and DevExpress assemblies, keep their own signature.
2. **`Build-Installer`** has Inno Setup sign the setup exe and the uninstaller it embeds (`SignTool` / `SignedUninstaller` in `installer/ClipMate.iss`).
3. **`Verify-Signatures`** fails the build if `ClipMate.App.exe` or the setup exe is not validly signed.
4. **`Package`** zips the signed publish folder for the portable download.

Every signature uses SHA-256 and an RFC 3161 timestamp from `http://time.certum.pl`, so it stays valid after the certificate expires.

Signing only runs when the `CERTUM_KEY_ID` environment variable holds the certificate's SHA1 thumbprint. Without it the build produces unsigned output exactly as before.

## Signing locally

1. Install [SimplySign Desktop](https://www.certum.eu/en/simplysign-desktop/) and log in with the code from the SimplySign mobile app.
2. Run the release build with the thumbprint set:

```powershell
$env:CERTUM_KEY_ID = '372659B4402149D52D013F4F3B1403A61534C17A'
.\build\build.ps1 -Target Release --release-version=0.0.0-signtest
```

`signtool.exe` comes from the Windows 10/11 SDK. The build uses the newest x64 copy under `C:\Program Files (x86)\Windows Kits\10\bin`.

## Signing in GitHub Actions

`release.yml` runs `dismine/windows-app-signing-setup-action`. It installs SimplySign Desktop, logs in with a TOTP generated from the token's seed and checks that the certificate is in the store. Then the Cake build signs as above. The setup steps are skipped when `CERTUM_KEY_ID` is empty, so releases stay unsigned until the secrets exist.

### One-time setup

1. **Get the TOTP seed.** In the Certum / SimplySign portal, start a new activation of the SimplySign mobile app token. When the QR code is shown, **decode it before scanning it with your phone**, for example with a QR reader app or by saving a screenshot and running `zbarimg`. The text is an `otpauth://totp/...?secret=...` URI. Then scan the same QR code in the mobile app, so your phone and CI generate the same codes. Anyone holding this URI and your username can sign as you, so keep it in a password manager.
2. **Create the `signing` environment** under the repository's *Settings → Environments* and add these **environment secrets**:

   | Secret | Value |
   |--------|-------|
   | `CERTUM_USERNAME` | SimplySign login e-mail |
   | `CERTUM_OTP_URI` | The `otpauth://` URI from step 1 |
   | `CERTUM_KEY_ID` | `372659B4402149D52D013F4F3B1403A61534C17A` (certificate SHA1 thumbprint) |

   To read these secrets a job must wait for an approval. To require it, add yourself as a required reviewer on the environment, and limit deployment branches/tags to `v*` tags.

### Testing it

Push a pre-release tag such as `v0.1.0-alpha.signtest`. The WinGet job skips alpha tags. Check the "Set up Certum SimplySign" step log, then download the setup exe and look at *Properties → Digital Signatures*, or run:

```powershell
signtool verify /pa /v ClipMate-Setup-0.1.0-alpha.signtest.exe
```

## Renewing the certificate

The current certificate is valid until **1 February 2028**. After renewing:

1. Read the new certificate's SHA1 thumbprint, for example from the `.cer` file's *Details* tab or with `certutil -dump <file>.cer`.
2. Update `CERTUM_KEY_ID` in the `signing` environment and in this document.
3. If Certum issues a new token, repeat step 1 of the one-time setup and update `CERTUM_OTP_URI`.

To update SimplySign Desktop in CI, change `SIMPLYSIGN_URL` in `release.yml`. The download is cached by that URL.
