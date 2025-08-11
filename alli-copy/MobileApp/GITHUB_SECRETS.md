# 🔐 GitHub Actions Secrets Configuration

## Required GitHub Repository Secrets

Add these secrets in your GitHub repository settings at:
`Settings → Secrets and variables → Actions → Repository secrets`

### Core EAS Configuration
```
EXPO_TOKEN=your-expo-access-token
```
**How to get:** Run `eas auth:tokens:create` or get from [https://expo.dev/settings/access-tokens](https://expo.dev/settings/access-tokens)

### API Endpoints
```
TEST_API_BASE_URL=https://test-api.alliance.com
STAGING_API_BASE_URL=https://staging-api.alliance.com  
PROD_API_BASE_URL=https://api.alliance.com
```

### Apple Developer Credentials
```
APPLE_ID=your-apple-id@email.com
ASC_APP_ID=your-app-store-connect-app-id
APPLE_TEAM_ID=your-apple-team-id
```
**Where to find:**
- APPLE_ID: Your Apple ID email
- ASC_APP_ID: App Store Connect → Your App → App Information → Apple ID
- APPLE_TEAM_ID: Apple Developer Portal → Membership → Team ID

### Google Play Console Credentials
```
GOOGLE_SERVICE_ACCOUNT_KEY_PATH=service-account-key-content
```
**How to create:**
1. Go to Google Cloud Console
2. Create a service account with Play Console access
3. Download the JSON key file
4. Copy the entire JSON content as the secret value

## Optional Secrets (for monitoring/analytics)
```
SENTRY_DSN=your-sentry-dsn
AMPLITUDE_API_KEY=your-amplitude-key
CODECOV_TOKEN=your-codecov-token
```

## EAS Secrets (Alternative to GitHub Secrets)

You can also store these in EAS instead of GitHub:

```bash
# Set EAS secrets
eas secret:create --scope project --name TEST_API_BASE_URL --value "https://test-api.alliance.com"
eas secret:create --scope project --name PROD_API_BASE_URL --value "https://api.alliance.com"
eas secret:create --scope project --name APPLE_ID --value "your-apple-id@email.com"
eas secret:create --scope project --name ASC_APP_ID --value "your-app-id"
eas secret:create --scope project --name APPLE_TEAM_ID --value "your-team-id"
```

## Verification Commands

Test your secrets are working:

```bash
# Check EAS secrets
eas secret:list

# Check GitHub Actions (after setting up secrets)
# Push to test branch and watch the workflow run
```

## Security Notes

- Never commit actual secret values to git
- Use the `.env.example` file as a template
- Rotate tokens periodically
- Use least-privilege access for service accounts