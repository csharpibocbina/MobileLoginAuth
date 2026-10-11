import { screen, userEvent, waitFor } from '@testing-library/react-native';
import * as SecureStore from 'expo-secure-store';
import React from 'react';
import { Text } from 'react-native';
import { authApi } from '../src/api/authApi';
import { useAuth } from '../src/auth/AuthContext';
import { LoginScreen } from '../src/screens/LoginScreen';
import { mockRouter, renderWithAuth, sampleAuthResponse } from '../test/testUtils';

jest.mock('expo-router', () => ({ useRouter: () => require('../test/testUtils').mockRouter }));
jest.mock('../src/api/authApi', () => ({
  authApi: { login: jest.fn(), logout: jest.fn(), me: jest.fn(), register: jest.fn(), loginHistory: jest.fn(), deleteAccount: jest.fn() },
}));

const mockedApi = authApi as jest.Mocked<typeof authApi>;

function Status() {
  const { status } = useAuth();
  return <Text testID="status">{status}</Text>;
}

async function renderLogin() {
  await renderWithAuth(
    <>
      <LoginScreen />
      <Status />
    </>,
  );
  await waitFor(() => expect(screen.getByTestId('status')).toHaveTextContent('signedOut'));
}

describe('LoginScreen', () => {
  beforeEach(() => jest.clearAllMocks());

  // MT-01
  it('renders email, password and Log in button', async () => {
    await renderLogin();

    expect(screen.getByLabelText('Email')).toBeOnTheScreen();
    expect(screen.getByLabelText('Password')).toBeOnTheScreen();
    expect(screen.getByRole('button', { name: 'Log in' })).toBeOnTheScreen();
  });

  // MT-02
  it('shows validation errors for empty fields and does not call the API', async () => {
    await renderLogin();
    const user = userEvent.setup();

    await user.press(screen.getByRole('button', { name: 'Log in' }));

    expect(screen.getByText('Email is required.')).toBeOnTheScreen();
    expect(screen.getByText('Password is required.')).toBeOnTheScreen();
    expect(mockedApi.login).not.toHaveBeenCalled();
  });

  // MT-03
  it('stores tokens and signs in on successful login', async () => {
    mockedApi.login.mockResolvedValue(sampleAuthResponse);
    await renderLogin();
    const user = userEvent.setup();

    await user.type(screen.getByLabelText('Email'), 'jay@example.com');
    await user.type(screen.getByLabelText('Password'), 'Passw0rd!');
    await user.press(screen.getByRole('button', { name: 'Log in' }));

    await waitFor(() => expect(screen.getByTestId('status')).toHaveTextContent('signedIn'));
    expect(mockedApi.login).toHaveBeenCalledWith('jay@example.com', 'Passw0rd!');
    expect(SecureStore.setItemAsync).toHaveBeenCalledWith('mla.accessToken', 'access-1');
    expect(SecureStore.setItemAsync).toHaveBeenCalledWith('mla.refreshToken', 'refresh-1');
  });

  // MT-04
  it('shows the server error on invalid login', async () => {
    mockedApi.login.mockRejectedValue(
      Object.assign(new Error('401'), {
        isAxiosError: true,
        response: { status: 401, data: { detail: 'Invalid email or password.' } },
      }),
    );
    await renderLogin();
    const user = userEvent.setup();

    await user.type(screen.getByLabelText('Email'), 'jay@example.com');
    await user.type(screen.getByLabelText('Password'), 'wrong');
    await user.press(screen.getByRole('button', { name: 'Log in' }));

    expect(await screen.findByText('Invalid email or password.')).toBeOnTheScreen();
    expect(screen.getByTestId('status')).toHaveTextContent('signedOut');
  });

  it('navigates to register', async () => {
    await renderLogin();
    const user = userEvent.setup();

    await user.press(screen.getByRole('button', { name: 'Create an account' }));

    expect(mockRouter.push).toHaveBeenCalledWith('/register');
  });
});
