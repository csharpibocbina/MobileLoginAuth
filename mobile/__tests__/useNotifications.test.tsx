import { renderHook } from '@testing-library/react-native';
import Toast from 'react-native-toast-message';
import { RECEIVE_NOTIFICATION, showNotificationToast, useNotifications } from '../src/notifications/useNotifications';
import type { NotificationMessage } from '../src/api/types';

const mockConnection = {
  on: jest.fn(),
  off: jest.fn(),
  start: jest.fn(() => Promise.resolve()),
  stop: jest.fn(() => Promise.resolve()),
};
const mockWithUrl = jest.fn();

jest.mock('@microsoft/signalr', () => {
  const builder: any = {
    withUrl: (...args: unknown[]) => {
      mockWithUrl(...args);
      return builder;
    },
    withAutomaticReconnect: () => builder,
    configureLogging: () => builder,
    build: () => mockConnection,
  };
  return { HubConnectionBuilder: jest.fn(() => builder), LogLevel: { Warning: 3 } };
});

const notification: NotificationMessage = {
  eventId: 'e1',
  type: 'LoginFailed',
  title: 'Failed sign-in attempt',
  message: 'Someone tried to sign in with a wrong password from iPhone 15.',
  occurredAtUtc: '2026-01-15T10:00:00Z',
};

describe('useNotifications', () => {
  beforeEach(() => jest.clearAllMocks());

  it('does not connect while signed out', async () => {
    await renderHook(() => useNotifications(false));

    expect(mockConnection.start).not.toHaveBeenCalled();
  });

  // MT-08
  it('connects to the hub when signed in and stops when signed out', async () => {
    const { rerender } = await renderHook(({ enabled }: { enabled: boolean }) => useNotifications(enabled), { initialProps: { enabled: true } });

    expect(mockWithUrl).toHaveBeenCalledWith(expect.stringMatching(/\/hubs\/notifications$/), expect.objectContaining({ accessTokenFactory: expect.any(Function) }));
    expect(mockConnection.start).toHaveBeenCalledTimes(1);

    await rerender({ enabled: false });

    expect(mockConnection.stop).toHaveBeenCalledTimes(1);
  });

  // MT-09
  it('passes incoming notifications to the handler', async () => {
    const handler = jest.fn();
    await renderHook(() => useNotifications(true, handler));

    const [event, callback] = mockConnection.on.mock.calls[0];
    expect(event).toBe(RECEIVE_NOTIFICATION);
    callback(notification);

    expect(handler).toHaveBeenCalledWith(notification);
  });

  it('shows an error toast for a failed login notification', () => {
    showNotificationToast(notification);

    expect(Toast.show).toHaveBeenCalledWith(expect.objectContaining({ type: 'error', text1: 'Failed sign-in attempt' }));
  });

  it('shows a success toast for a successful login', () => {
    showNotificationToast({ ...notification, type: 'LoginSucceeded', title: 'New sign-in to your account' });

    expect(Toast.show).toHaveBeenCalledWith(expect.objectContaining({ type: 'success' }));
  });
});
