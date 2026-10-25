# Luminark development

## 🛠️ Building from Source

### Prerequisites
- Windows 10 (Build 19041+) or Windows 11
- [.NET 10.0 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- Inno Setup 6.7.3 (optional, for packaging the installer)

### Build Steps
```powershell
# Clone the repository
git clone https://github.com/silviuk/Luminark.git
cd Luminark

# Restore dependencies and build
dotnet build -c Release

# Publish framework-dependent binaries
dotnet publish Luminark.csproj -c Release -o publish

# (Optional) Compile Inno Setup installer
& "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" installer\Luminark.iss
```

---

## 🏛️ Architecture & Interop

- **Framework**: .NET 10 WPF with C# 14
- **UI Design**: [WPF-UI](https://github.com/lepoco/wpfui) (Windows 11 Fluent Design System)
- **Hardware & Windows APIs**:
  - `dxva2.dll`: `GetMonitorBrightness`, `SetMonitorBrightness`, `GetPhysicalMonitorsFromHMONITOR`
  - `user32.dll`: `WH_MOUSE_LL` low-level mouse hook, `LockWorkStation`, `SendMessageTimeout`, `PostMessage` (`SC_MONITORPOWER`), `RegisterHotKey`
  - `kernel32.dll`: `SetThreadExecutionState` (`ES_CONTINUOUS | ES_SYSTEM_REQUIRED | ES_DISPLAY_REQUIRED`)
  - `shell32.dll`: `Shell_NotifyIconGetRect` bounding box query
  - `root\wmi`: `WmiMonitorBrightnessMethods` for internal laptop screens
  - Windows Registry: `Software\Microsoft\Windows\CurrentVersion\Themes\Personalize`

---

## 🌐 Store & WinGet Publishing

Luminark includes a dedicated GitHub Action workflow (`.github/workflows/publish-store-and-winget.yml`) that can be manually dispatched to publish any release directly to the **Windows Store** and **WinGet**:

### How to Trigger:
1. Go to your repository on GitHub and click the **Actions** tab.
2. Select the **"Publish to Microsoft Store and WinGet"** workflow on the left sidebar.
3. Click **"Run workflow"**:
   - **Release Tag**: Required exact tag (for example `v1.1.19`). Wait for Build and Release to finish before publishing.
   - **Publish to WinGet**: Checkbox (default: disabled; explicit opt-in while the initial submission awaits moderation).
   - **Publish to Windows Store**: Checkbox (default: disabled; explicit authorization is required).
4. Click **"Run workflow"**.

The workflow verifies the release asset SHA256 digests and the embedded MSIX identity/version before contacting Partner Center. Release notes come from the selected tag. Publication is serialized; an existing committed submission is only accepted after its package version, architecture, category and delivery settings are verified. A different active submission blocks publication instead of reporting false success.

Store submissions use **Utilities + tools** (`UtilitiesAndTools`), immediate publication after certification, and no gradual rollout. Luminark is a desktop utility, not a downloadable Windows theme. The existing package name, publisher and application ID must remain stable so Store installations can upgrade normally. A successful workflow means the submission was verified; only Partner Center's **Published** status means it is live.

The product privacy policy is [PRIVACY.md](https://github.com/silviuk/Luminark/blob/main/PRIVACY.md). Keep that URL configured under **Partner Center > Properties > Privacy policy URL**. The submission API's legacy `baseListing.privacyPolicy` field is ignored by Microsoft, so the workflow does not pretend to update it. Do not edit an API-created submission in the portal while its API publication is in progress.

Run the publishing regression checks locally with `./Tools/Test-StorePublishing.ps1`.

### Required Repository Secrets:
Configure these under **Settings > Secrets and variables > Actions**:

| Secret | Purpose | Source |
|---|---|---|
| `WINGET_TOKEN` | Submits manifest pull requests to `microsoft/winget-pkgs` | GitHub Personal Access Token (classic with `public_repo` scope) |
| `MS_STORE_TENANT_ID` | Azure AD / Entra ID Tenant ID | Azure Portal / Entra ID Overview |
| `MS_STORE_SELLER_ID` | Partner Center Publisher / Seller ID | Partner Center Account Settings > Identifiers |
| `MS_STORE_CLIENT_ID` | Azure AD App Registration (Client ID) | Entra ID App Registrations |
| `MS_STORE_CLIENT_SECRET` | Azure AD App Registration Secret | Entra ID App Registrations > Certificates & secrets |
| `MS_STORE_APP_ID` | Product ID in Partner Center | Partner Center Product Overview |

---

## 📄 License

Distributed under the [MIT License](LICENSE). Copyright © 2026 Silviu Vlasceanu.

## Validation and signing

Use the SDK pinned by `global.json`. Run `dotnet restore Tests/BrightnessRegression --locked-mode`,
then `dotnet run --project Tests/BrightnessRegression -c Release --no-restore`.
Run `./Tools/Test-StorePublishing.ps1`, `./Tools/Test-ReleaseInputs.ps1`, and
`./Tools/Audit-Dependencies.ps1` before a release.

The regression suite uses fake monitor writes and temporary registry/file fixtures. It covers
rapid hardware events, bounded/fair brightness and volume scheduling, shutdown, stale discovery,
settings backup recovery, registry idle/change notifications, scroll batching, and log rotation.
Physical USB-C/DP reconnect, input switching, sleep/wake, and clean-machine installation still
need a hardware/VM acceptance pass.

For a read-only ten-minute process sample, run
`./Tools/Measure-Performance.ps1 -ProcessId <Luminark PID> -OutputPath baseline.csv`.
Compare old and new builds under the same conditions, using private bytes, CPU, page faults,
and handle counts. Repeat show/hide and reconnect cycles; do not infer heap savings from a
small working set. UI latency and actual panel latency should be assessed separately.

Trusted signing uses `CODESIGN_PFX_BASE64` and `CODESIGN_PFX_PASSWORD` GitHub secrets
for a trusted Windows code-signing certificate. These are not configured automatically.
The workflow signs the application and installer, verifies the signer and RFC3161 timestamp,
and removes the imported certificate. Set the `sign_release` workflow input or repository
variable `CODESIGN_REQUIRED=true` to require signing. When enabled, missing credentials stop
publication. While signing enrollment is deferred, unsigned releases are explicitly labeled. Configure a signing service instead if your certificate requires remote signing;
never export an HSM-backed key to create a PFX.

The runtime bootstrapper pins an official Microsoft URL and SHA256 and checks the publisher
before execution. Update it with `./Tools/Update-Runtime.ps1 -Version 10.0.<patch>`;
the update command verifies the download against Microsoft's SHA512 metadata and signature.
Review and commit the changed runtime constants, SDK version, and dependency locks together.
Third-party actions and release tools are pinned; Dependabot proposes action and NuGet updates.
Store and WinGet publication remain explicit opt-ins and existing historical manifests are unchanged.
Actual-display acceptance can be run explicitly with `dotnet run --project Tests/BrightnessRegression -c Release -- --hardware-smoke` while unlocked. It performs a small brightness burst, validates readback, repeats handle recovery 100 times, and restores brightness. CI also exercises missing runtime files, installer bootstrapper install/repair, and application startup on a disposable hosted runner.
