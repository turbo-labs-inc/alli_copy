# Alliance App

This is an [Expo](https://expo.dev) project for the Alliance Energy mobile application.

## Development

1. Install dependencies

   ```bash
   npm install
   ```

2. Start the app

   ```bash
   npm start
   ```

   Or for a specific platform:

   ```bash
   npm run ios
   npm run android
   ```

## EAS Builds and Updates

This project uses [EAS (Expo Application Services)](https://docs.expo.dev/eas/) for builds and over-the-air updates.

### Setup

Before you can use EAS, you need to set up your project:

1. Install the EAS CLI globally (optional)

   ```bash
   npm install -g eas-cli
   ```

2. Log in to your Expo account

   ```bash
   npx eas-cli login
   ```

3. Set up your project with your Expo project ID

   ```bash
   npm run eas:setup YOUR_PROJECT_ID
   ```

   Replace `YOUR_PROJECT_ID` with your actual Expo project ID.

### Building the App

To create builds for testing or production:

```bash
# Create a preview build for internal testing
npm run build:preview

# Create a production build for app stores
npm run build:production
```

### Publishing Updates

Once you have a build installed on devices, you can push updates without going through the app stores:

```bash
# Push an update to the preview channel
npm run update:preview

# Push an update to the production channel
npm run update:production
```

## Project Structure

- **app/**: Main application code with file-based routing
- **assets/**: Images, fonts, and theme files
- **components/**: React components organized by feature
- **constants/**: App constants like colors
- **navigation/**: Navigation setup and components
- **util/**: Utility functions

## Learn More

To learn more about the technologies used in this project:

- [Expo Documentation](https://docs.expo.dev/)
- [Expo Router](https://docs.expo.dev/router/introduction/)
- [EAS Build](https://docs.expo.dev/build/introduction/)
- [EAS Update](https://docs.expo.dev/eas-update/introduction/)
