# 🚀 Alliance App Deployment Guide

## Overview

This guide covers the complete deployment pipeline for the Alliance mobile app to both iOS App Store and Google Play Store.

## Pipeline Structure

### Test Pipeline (`test` branch)
- **Purpose**: Internal testing and QA
- **Builds**: APK (Android) + IPA (iOS)
- **Distribution**: TestFlight (iOS) + Play Console Internal Testing (Android)
- **Environment**: Test API endpoints

### Production Pipeline (`main` branch)
- **Purpose**: Public app store releases
- **Builds**: AAB (Android) + IPA (iOS)
- **Distribution**: App Store (iOS) + Play Store Production (Android)
- **Environment**: Production API endpoints

## Prerequisites

### 1. EAS CLI Setup
```bash
npm install -g @expo/eas-cli
eas login
```

### 2. Apple Developer Account
- Valid Apple Developer Program membership
- App Store Connect access
- iOS Distribution Certificate
- Provisioning Profiles

### 3. Google Play Console
- Google Play Console developer account
- Upload key/keystore for signing
- Service account for automated uploads

## Environment Setup

### Required Environment Variables

Create these in EAS Secrets:

```bash
# API Endpoints
TEST_API_BASE_URL=https://test-api.alliance.com
STAGING_API_BASE_URL=https://staging-api.alliance.com  
PROD_API_BASE_URL=https://api.alliance.com

# Apple Store Connect
APPLE_ID=your-apple-id@email.com
ASC_APP_ID=your-app-store-connect-app-id
APPLE_TEAM_ID=your-apple-team-id

# Google Play Console
GOOGLE_SERVICE_ACCOUNT_KEY_PATH=path/to/service-account.json
```

### Set Environment Variables
```bash
# Set up environment variables
./scripts/setup-env.sh production
```

## Build Profiles

### Development Builds
```bash
# iOS Simulator
npm run build:development

# Android APK  
npm run build:development-android
```

### Test Builds
```bash
# Both platforms
npm run build:test

# Individual platforms
npm run build:test-ios
npm run build:test-android
```

### Production Builds
```bash
# Both platforms
npm run build:production

# Individual platforms
npm run build:production-ios
npm run build:production-android
```

## Deployment Process

### Test Deployment

1. **Push to test branch**:
   ```bash
   git checkout test
   git push origin test
   ```

2. **Manual test build**:
   ```bash
   npm run pipeline:test
   ```

3. **Submit to test tracks**:
   ```bash
   npm run submit:test
   ```

### Production Deployment

1. **Create release tag**:
   ```bash
   git checkout main
   git tag v1.0.0
   git push origin v1.0.0
   ```

2. **Manual production build**:
   ```bash
   npm run pipeline:production
   ```

3. **Submit to stores**:
   ```bash
   npm run submit:production
   ```

## GitHub Actions

### Automated Workflows

- **Test Pipeline**: Triggers on pushes to `test`, `develop`, `main` branches
- **Production Pipeline**: Triggers on pushes to `main` and version tags

### Required Secrets

Set these in GitHub repository secrets:

```
EXPO_TOKEN=your-expo-access-token
TEST_API_BASE_URL=https://test-api.alliance.com
PROD_API_BASE_URL=https://api.alliance.com
APPLE_ID=your-apple-id@email.com
ASC_APP_ID=your-app-store-connect-app-id
APPLE_TEAM_ID=your-apple-team-id
GOOGLE_SERVICE_ACCOUNT_KEY_PATH=service-account-key-content
```

## Store-Specific Configuration

### iOS App Store

#### App Store Connect Setup
1. Create app record in App Store Connect
2. Configure app information:
   - App name: "Alliance App"
   - Bundle ID: `com.allianceenergy.allianceapp`
   - SKU: `alliance-app-ios`

#### TestFlight Setup
1. Create beta groups
2. Add internal testers
3. Configure test information

#### Submission Process
```bash
# Build and submit to TestFlight
npm run build:test-ios
npm run submit:test-ios

# Build and submit to App Store
npm run build:production-ios
npm run submit:production-ios
```

### Google Play Store

#### Play Console Setup
1. Create app in Play Console
2. Configure app information:
   - App name: "Alliance App"
   - Package name: `com.allianceenergy.allianceapp`

#### Testing Tracks
- **Internal Testing**: Automated uploads from test builds
- **Closed Testing**: Manual promotion for broader QA
- **Open Testing**: Pre-production testing
- **Production**: Live app store release

#### Submission Process
```bash
# Build and submit to Internal Testing
npm run build:test-android
npm run submit:test-android

# Build and submit to Production
npm run build:production-android
npm run submit:production-android
```

## OTA Updates

### EAS Update for Quick Fixes

For JavaScript-only changes (no native code changes):

```bash
# Update test environment
npm run update:test

# Update production environment  
npm run update:production
```

## Troubleshooting

### Common Issues

1. **Build failures**:
   - Check EAS build logs
   - Verify credentials are set correctly
   - Ensure all dependencies are installed

2. **Submission failures**:
   - Verify App Store Connect/Play Console credentials
   - Check app bundle ID matches registered app
   - Ensure all required metadata is complete

3. **Environment variable issues**:
   - Run `eas secret:list` to verify secrets are set
   - Use `eas env:list` to check environment configuration

### Debug Commands

```bash
# Check EAS configuration
eas config

# List project secrets
eas secret:list

# View build logs
eas build:list
eas build:view [build-id]

# Check submission status
eas submit:list
```

## Monitoring and Maintenance

### Post-Release Monitoring

1. **Crash Reporting**: Monitor Expo/Sentry for crashes
2. **Store Analytics**: Track downloads and ratings
3. **User Feedback**: Monitor app store reviews
4. **Performance**: Watch for performance regressions

### Regular Maintenance

1. **Dependency Updates**: Keep dependencies current
2. **Security Patches**: Apply security updates promptly
3. **Store Guidelines**: Stay compliant with store policies
4. **Certificate Renewal**: Renew certificates before expiration

## Support

For deployment issues:
1. Check this documentation
2. Review EAS documentation: https://docs.expo.dev/eas/
3. Contact the development team
4. Check GitHub Issues for known problems