---
name: windows-store-partner-center-pipeline
description: >-
  Step-by-step runbook and implementation guide for building, packaging (MSIX and Inno Setup),
  and automating Microsoft Store (Partner Center REST API) and WinGet publication via GitHub Actions.
---

# Windows Store & WinGet CI/CD Publishing Pipeline Guide

This skill provides an automated CI/CD blueprint for building desktop Windows applications (C# / WPF / WinUI), packaging them as self-contained MSIX bundles and Inno Setup installers, and publishing them directly to the Microsoft Store via the Partner Center REST API and to the Windows Package Manager (WinGet).

---

## 1. Distribution Strategy: Dual Packaging Model

| Channel | Format | Build Command | Key Advantage |
| :--- | :--- | :--- | :--- |
| **GitHub Releases & Direct Download** | Inno Setup `.exe` | `dotnet publish -c Release -r win-x64 --self-contained false` | Extremely compact installer (~7 MB); downloads missing .NET runtime dynamically if needed. |
| **Microsoft Store** | `.msix` | `dotnet publish -c Release -r win-x64 --self-contained true` | 100% zero-dependency execution. Eliminates missing .NET runtime errors in app containers. |
| **WinGet** | Inno Setup `.exe` / `msix` | Managed via `wingetcreate` | Automated manifest submission to `microsoft/winget-pkgs`. |

---

## 2. Microsoft Partner Center API Authentication

To publish without manual browser interaction, authenticate with Microsoft Entra ID (Azure AD) using a Service Principal:

### Required GitHub Secrets
1. `MS_STORE_TENANT_ID`: Directory (Tenant) ID from Entra ID.
2. `MS_STORE_CLIENT_ID`: Application (Client) ID of the registered App in Entra ID.
3. `MS_STORE_CLIENT_SECRET`: Client Secret Value generated in Certificates & Secrets.
4. `MS_STORE_SELLER_ID`: Publisher / Seller ID from Partner Center Account Settings > Identifiers.
5. `MS_STORE_APP_ID`: 12-character Product ID (e.g. `9NCJG5RVRR1C`) from Partner Center Product Overview.

### Token Acquisition
```powershell
$body = @{
    grant_type    = "client_credentials"
    client_id     = $clientId
    client_secret = $clientSecret
    resource      = "https://api.partner.microsoft.com"
}
$tokenResponse = Invoke-RestMethod -Method Post `
    -Uri "https://login.microsoftonline.com/$tenantId/oauth2/token" `
    -Body $body `
    -ContentType "application/x-www-form-urlencoded"

$accessToken = $tokenResponse.access_token
```

---

## 3. Partner Center Submission Lifecycle

1. **Query App & Active Submissions**:
   `GET https://api.partner.microsoft.com/v1.0/my/applications/{appId}`
2. **Create New Draft Submission**:
   `POST https://api.partner.microsoft.com/v1.0/my/applications/{appId}/submissions`
3. **Upload MSIX to Azure Blob Storage**:
   The draft submission returns a pre-signed Azure Blob Storage SAS URI (`fileUploadUrl`).
   - Create a `.zip` containing the `.msix`.
   - Upload using Block Blob REST API with header `x-ms-blob-type: BlockBlob`.
4. **Update Submission Metadata & Release Notes**:
   `PUT https://api.partner.microsoft.com/v1.0/my/applications/{appId}/submissions/{submissionId}`
   - Attach package file name.
   - Update `baseListing.description` and `listings.{lang}.releaseNotes` ("What's New in this version", max 1500 chars).
5. **Commit for Certification**:
   `POST https://api.partner.microsoft.com/v1.0/my/applications/{appId}/submissions/{submissionId}/commit`
   - Returns `{"status": "CommitStarted"}`.
   - Poll until status transitions to `CommitCompleted` or `InCertification`.

---

## 4. WinGet Manifest Automation (`wingetcreate`)

```powershell
Invoke-WebRequest -Uri "https://github.com/microsoft/winget-create/releases/latest/download/wingetcreate.exe" -OutFile "wingetcreate.exe"

.\wingetcreate.exe update "$PackageId" `
    --version "$Version" `
    --urls "$InstallerUrl" `
    --token "$WingetToken" `
    --submit
```
*(Requires a GitHub Personal Access Token with `public_repo` scope stored as `WINGET_TOKEN`)*.
