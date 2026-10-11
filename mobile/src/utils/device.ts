import * as Device from 'expo-device';
import { Platform } from 'react-native';

/** Short human-readable device description sent with every request, e.g. "iPhone 15 (iOS 18.1)". */
export function getDeviceInfo(): string {
  const model = Device.modelName ?? Platform.OS;
  const os = Device.osName ? `${Device.osName} ${Device.osVersion ?? ''}`.trim() : Platform.OS;
  return `${model} (${os})`;
}
