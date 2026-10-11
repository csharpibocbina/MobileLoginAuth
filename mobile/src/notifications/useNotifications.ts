import { HubConnection, HubConnectionBuilder, LogLevel } from '@microsoft/signalr';
import { useEffect, useRef } from 'react';
import Toast from 'react-native-toast-message';
import { getApiBaseUrl, HUB_PATH } from '../config';
import { tokenStorage } from '../api/tokenStorage';
import type { NotificationMessage } from '../api/types';

export const RECEIVE_NOTIFICATION = 'ReceiveNotification';

export function showNotificationToast(n: NotificationMessage) {
  Toast.show({
    type: n.type === 'LoginFailed' ? 'error' : n.type === 'Logout' ? 'info' : 'success',
    text1: n.title,
    text2: n.message,
    visibilityTime: 5000,
  });
}

export function buildConnection(baseUrl = getApiBaseUrl()): HubConnection {
  return new HubConnectionBuilder()
    .withUrl(`${baseUrl}${HUB_PATH}`, {
      // Read on every (re)connect so a refreshed token is picked up.
      accessTokenFactory: async () => (await tokenStorage.get())?.accessToken ?? '',
    })
    .withAutomaticReconnect()
    .configureLogging(LogLevel.Warning)
    .build();
}

/**
 * Keeps a SignalR connection open while the user is signed in and shows a toast for each
 * login / failed login / logout notification pushed by the server.
 */
export function useNotifications(enabled: boolean, onNotification: (n: NotificationMessage) => void = showNotificationToast) {
  const handlerRef = useRef(onNotification);
  handlerRef.current = onNotification;

  useEffect(() => {
    if (!enabled) return;

    const connection = buildConnection();
    connection.on(RECEIVE_NOTIFICATION, (n: NotificationMessage) => handlerRef.current(n));
    connection.start().catch((err) => console.warn('SignalR connection failed', err));

    return () => {
      connection.off(RECEIVE_NOTIFICATION);
      void connection.stop();
    };
  }, [enabled]);
}
