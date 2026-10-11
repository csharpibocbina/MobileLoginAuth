import Constants from 'expo-constants';
import { Platform } from 'react-native';

/**
 * Backend base URL. Set EXPO_PUBLIC_API_URL (e.g. in .env) for a real device or production.
 * Defaults: Android emulator reaches the host machine via 10.0.2.2, iOS simulator via localhost.
 */
export function getApiBaseUrl(): string {
  const fromEnv = process.env.EXPO_PUBLIC_API_URL;
  const fromConfig = Constants.expoConfig?.extra?.apiUrl as string | undefined;
  const url = fromEnv || fromConfig || (Platform.OS === 'android' ? 'http://10.0.2.2:5080' : 'http://localhost:5080');
  return url.replace(/\/+$/, '');
}

export const HUB_PATH = '/hubs/notifications';
