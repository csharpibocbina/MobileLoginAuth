import { act, screen, userEvent, waitFor } from '@testing-library/react-native';
import * as SecureStore from 'expo-secure-store';
import React from 'react';
import { Alert, Text } from 'react-native';
import { authApi } from '../src/api/authApi';
import { tokenStorage } from '../src/api/tokenStorage';
import { useAuth } from '../src/auth/AuthContext';
import { HomeScreen } from '../src/screens/HomeScreen';
import { renderWithAuth, sampleUser } from '../test/testUtils';

jest.mock('../src/api/authApi', () => ({
  authApi: { login: jest.fn(), logout: jest.fn(), me: jest.fn(), register: jest.fn(), loginHistory: jest.fn(), deleteAccount: jest.fn() },
}));

const mockedApi = authApi as jest.Mocked<typeof authApi>;

function Status() {
  const { status } = useAuth();
  return <Text testID="status">{status}</Text>;
}

async function renderSignedIn() {
  await tokenStorage.save({ accessToken: 'access-1', refreshToken: 'refresh-1' });
  mockedApi.me.mockResolvedValue(sampleUser);
  await renderWithAuth(
    <>
      <HomeScreen />
      <Status />
    </>,
  );
  await waitFor(() => expect(screen.getByTestId('status')).toHaveTextContent('signedIn'));
}

describe('HomeScreen', () => {
  beforeEach(() => {
    jest.clearAllMocks();
    mockedApi.loginHistory.mockResolvedValue([
      { eventType: 'LoginSucceeded', ipAddress: '10.0.0.1', deviceInfo: 'iPhone 15', occurredAtUtc: '2026-01-15T10:00:00Z' },
      { eventType: 'LoginFailed', ipAddress: '10.0.0.1', deviceInfo: 'iPhone 15', occurredAtUtc: '2026-01-15T09:59:00Z' },
    ]);
  });

  it('restores the session and shows the user and recent activity', async () => {
    await renderSignedIn();

    expect(await screen.findByText('Hi, Jay')).toBeOnTheScreen();
    expect(await screen.findByText('Signed in')).toBeOnTheScreen();
    expect(screen.getByText('Failed sign-in')).toBeOnTheScreen();
  });

  // MT-05
  it('logout calls the API with the refresh token and clears stored tokens', async () => {
    mockedApi.logout.mockResolvedValue();
    await renderSignedIn();
    const user = userEvent.setup();

    await user.press(screen.getByRole('button', { name: 'Log out' }));

    await waitFor(() => expect(screen.getByTestId('status')).toHaveTextContent('signedOut'));
    expect(mockedApi.logout).toHaveBeenCalledWith('refresh-1');
    expect(SecureStore.deleteItemAsync).toHaveBeenCalledWith('mla.accessToken');
    expect(await tokenStorage.get()).toBeNull();
  });

  it('logout still signs out locally when the server call fails', async () => {
    mockedApi.logout.mockRejectedValue(new Error('offline'));
    await renderSignedIn();
    const user = userEvent.setup();

    await user.press(screen.getByRole('button', { name: 'Log out' }));

    await waitFor(() => expect(screen.getByTestId('status')).toHaveTextContent('signedOut'));
  });

  it('delete account asks for confirmation, then deletes and signs out', async () => {
    mockedApi.deleteAccount.mockResolvedValue();
    const alertSpy = jest.spyOn(Alert, 'alert');
    await renderSignedIn();
    const user = userEvent.setup();

    await user.press(screen.getByRole('button', { name: 'Delete account' }));
    const buttons = alertSpy.mock.calls[0][2]!;
    await act(async () => {
      await buttons.find((b) => b.text === 'Delete')!.onPress!();
    });

    await waitFor(() => expect(screen.getByTestId('status')).toHaveTextContent('signedOut'));
    expect(mockedApi.deleteAccount).toHaveBeenCalled();
  });
});
