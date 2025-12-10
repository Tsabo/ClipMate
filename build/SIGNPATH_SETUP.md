# SignPath Code Signing Setup

This document explains how to activate code signing for ClipMate installers using SignPath.io's free OSS tier.

## Current Status

⚠️ **NOT YET CONFIGURED** - Code signing is not currently active. Initial releases will be unsigned with clear warnings.

## Why Code Signing?

- **Eliminates SmartScreen warnings** - Users won't see "Unknown Publisher" dialogs
- **Builds trust** - Verifies authenticity of installers
- **Prevents impersonation** - Protects against malicious copies
- **Professional appearance** - Signals legitimate software

## SignPath.io Free OSS Tier

### Benefits

✅ **Free for open source projects**
✅ **EV certificate equivalent** - Full SmartScreen trust
✅ **GitHub Actions integration**
✅ **Cloud-based** - No local HSM required
✅ **Timestamping included**

### Requirements

- Public GitHub repository ✅
- Active open source project ✅
- MIT or similar OSS license ✅
- Regular maintenance/updates ✅

## Setup Process

### Phase 1: Application (Before First Release)

#### Step 1: Create SignPath Account

1. Visit [https://signpath.io](https://signpath.io)
2. Click **"Sign up for free"**
3. Choose **"Open Source Project"** tier
4. Create account with GitHub authentication

#### Step 2: Submit Project for Approval

1. In SignPath dashboard, click **"New Project"**
2. Fill out application:
   - **Project Name:** ClipMate
   - **Repository URL:** https://github.com/Tsabo/ClipMate
   - **License:** MIT
   - **Description:** "Windows clipboard manager with advanced search, formatting, and QuickPaste features"
3. Submit application
4. **Wait for approval** (typically 1-2 weeks)

### Phase 2: Configuration (After Approval)

#### Step 3: Get SignPath Credentials

Once approved, SignPath will provide:

- **Organization ID** - Your unique org identifier
- **API Token** - For GitHub Actions authentication
- **Project Slug** - Your project identifier (usually "clipmate")
- **Signing Policy Slug** - Policy name (usually "release-signing")

#### Step 4: Configure GitHub Secrets

Add secrets to GitHub repository:

1. Go to `https://github.com/Tsabo/ClipMate/settings/secrets/actions`
2. Click **"New repository secret"**
3. Add the following secrets:

| Secret Name | Value | Description |
|-------------|-------|-------------|
| `SIGNPATH_API_TOKEN` | (from SignPath) | API authentication token |
| `SIGNPATH_ORGANIZATION_ID` | (from SignPath) | Your organization ID |
| `SIGNPATH_PROJECT_SLUG` | `clipmate` | Project identifier |
| `SIGNPATH_SIGNING_POLICY` | `release-signing` | Signing policy name |

#### Step 5: Update build.cake

The build script already has SignPath integration code, but it's currently commented/disabled.

In `build/build.cake`, the `Sign-Installers` task will automatically activate when secrets are present.

**No code changes needed** - signing activates automatically when:
- `SIGNPATH_API_TOKEN` environment variable exists
- `SIGNPATH_ORGANIZATION_ID` environment variable exists

#### Step 6: Test Signing

Create a test release to verify signing works:

```bash
git tag v0.1.0-alpha.1
git push origin v0.1.0-alpha.1
```

Monitor GitHub Actions:
1. Go to `https://github.com/Tsabo/ClipMate/actions`
2. Watch the release workflow
3. Verify signing step completes successfully
4. Download installer and check signature

**Verify signature:**
```powershell
Get-AuthenticodeSignature ClipMate-Setup-0.1.0-alpha.1.exe
```

Should show:
- **Status:** Valid
- **SignerCertificate:** SignPath Foundation
- **TimeStamperCertificate:** Present

### Phase 3: Production (First Signed Release)

#### Step 7: Create First Signed Release

```bash
git tag v1.0.0
git push origin v1.0.0
```

#### Step 8: Update Release Notes

Add to release notes:
```markdown
## Security

✅ This release is **code-signed** with an EV certificate via SignPath.io
- No SmartScreen warnings
- Verified publisher: ClipMate Contributors
- Timestamped for long-term validity
```

#### Step 9: Update Documentation

Remove "unsigned" warnings from:
- `.github/release-template.md`
- `README.md`
- `build/BUILD.md`

## SignPath Integration Details

### How It Works

1. **Trigger:** GitHub Actions release workflow runs
2. **Build:** Installers are built unsigned
3. **Submit:** Installers uploaded to SignPath via API
4. **Sign:** SignPath signs with EV certificate
5. **Download:** Signed installers downloaded back
6. **Publish:** Signed installers attached to GitHub release

### Build Script Integration

The `build.cake` script includes a `Sign-Installers` task:

```csharp
Task("Sign-Installers")
    .WithCriteria(() => canSign)  // Checks for SignPath credentials
    .IsDependentOn("Build-Installers")
    .Does(() =>
{
    // SignPath API integration
    // Submits both installers for signing
    // Polls for completion
    // Downloads signed versions
});
```

### GitHub Actions Integration

In `.github/workflows/release.yml`:

```yaml
- name: Sign installers
  if: ${{ secrets.SIGNPATH_API_TOKEN != '' }}
  run: dotnet cake --target=Sign-Installers
  env:
    SIGNPATH_API_TOKEN: ${{ secrets.SIGNPATH_API_TOKEN }}
    SIGNPATH_ORGANIZATION_ID: ${{ secrets.SIGNPATH_ORGANIZATION_ID }}
```

## Signing Quotas

SignPath free tier includes:

- **Signing operations:** Generous monthly allowance
- **File size:** Up to 500MB per file
- **Number of files:** Multiple files per signing request

**We sign 2 files per release:**
- ClipMate-Setup-{version}.exe (~50MB)
- ClipMate-Portable-{version}.exe (~650MB)

**Estimated quota usage:** ~2-4 operations per release

## Monitoring

### Check Signing Status

View SignPath dashboard:
1. Log in to [https://app.signpath.io](https://app.signpath.io)
2. View **"Signing Requests"** history
3. Monitor quota usage
4. Review any errors

### Verify Signatures

Always verify signatures after release:

```powershell
# Windows PowerShell
Get-AuthenticodeSignature ClipMate-Setup-1.0.0.exe | Format-List

# Expected output:
# SignerCertificate: CN=SignPath Foundation
# Status: Valid
# StatusMessage: Signature verified
```

## Troubleshooting

### Application Not Approved

**Timeline:** 1-2 weeks typical, up to 4 weeks possible

**Actions:**
- Be patient
- Respond promptly to any SignPath questions
- Ensure repository is public and active
- Continue releasing unsigned builds with warnings

### Signing Fails in CI

**Check:**
1. Secrets are set correctly in GitHub
2. SignPath account is active
3. Quota not exceeded
4. Network connectivity (rare)

**Debug:**
- Check GitHub Actions logs
- Check SignPath dashboard for error messages
- Test signing locally with API token

### Signature Invalid

**Causes:**
- Installer modified after signing
- Timestamp server issue
- Certificate revoked (very rare)

**Solution:**
- Re-sign the installer
- Contact SignPath support if persistent

### Quota Exceeded

**Solution:**
- Wait for monthly reset
- Contact SignPath for quota increase (usually granted for active projects)
- Temporarily release unsigned builds

## Support

### SignPath Support

- **Email:** support@signpath.io
- **Documentation:** https://docs.signpath.io
- **Status Page:** https://status.signpath.io

### ClipMate Issues

If users report signature problems:
1. Verify signature validity
2. Check SignPath service status
3. Test on clean Windows installation
4. Open GitHub issue with details

## Timeline Summary

| Phase | Duration | Status |
|-------|----------|--------|
| **Application Submission** | 1 day | ⏳ Not started |
| **Approval Wait** | 1-4 weeks | ⏳ Pending |
| **Configuration** | 1 day | ⏳ Pending approval |
| **Testing** | 1-2 days | ⏳ Pending approval |
| **Production** | Ongoing | ⏳ Pending approval |

**Current Status:** Awaiting first stable release before applying

## Next Steps

1. **Complete initial development** and stabilize for v1.0.0
2. **Create first pre-release** (unsigned) for community testing
3. **Apply to SignPath** once ready for public v1.0.0 release
4. **Configure secrets** after approval
5. **Release v1.0.0** with code signing active

---

**Last Updated:** 2025-12-08  
**SignPath Status:** Not yet applied  
**Target Application Date:** Before v1.0.0 release
