import { screen, userEvent, waitFor } from '@testing-library/react-native';
import React from 'react';
import { authApi } from '../src/api/authApi';
import { RegisterScreen } from '../src/screens/RegisterScreen';
import { renderWithAuth, sampleAuthResponse, sampleUser } from '../test/testUtils';

jest.mock('expo-router', () => ({ useRouter: () => require('../test/testUtils').mockRouter }));
jest.mock('../src/api/authApi', () => ({
  authApi: { login: jest.fn(), logout: jest.fn(), me: jest.fn(), register: jest.fn(), loginHistory: jest.fn(), deleteAccount: jest.fn() },
}));

const mockedApi = authApi as jest.Mocked<typeof authApi>;

async function fill(user: ReturnType<typeof userEvent.setup>, password: string, confirm = password) {
  await user.type(screen.getByLabelText('Name'), 'Jay');
  await user.type(screen.getByLabelText('Email'), 'jay@example.com');
  await user.type(screen.getByLabelText('Password'), password);
  await user.type(screen.getByLabelText('Confirm password'), confirm);
  await user.press(screen.getByRole('button', { name: 'Sign up' }));
}

describe('RegisterScreen', () => {
  beforeEach(() => jest.clearAllMocks());

  // MT-10
  it('rejects a weak password without calling the API', async () => {
    await renderWithAuth(<RegisterScreen />);
    const user = userEvent.setup();

    await fill(user, 'weak');

    expect(screen.getByText('Password must be at least 8 characters.')).toBeOnTheScreen();
    expect(mockedApi.register).not.toHaveBeenCalled();
  });

  it('rejects mismatched passwords', async () => {
    await renderWithAuth(<RegisterScreen />);
    const user = userEvent.setup();

    await fill(user, 'Passw0rd!', 'Passw0rd?');

    expect(screen.getByText('Passwords do not match.')).toBeOnTheScreen();
  });

  it('registers then logs in', async () => {
    mockedApi.register.mockResolvedValue(sampleUser);
    mockedApi.login.mockResolvedValue(sampleAuthResponse);
    await renderWithAuth(<RegisterScreen />);
    const user = userEvent.setup();

    await fill(user, 'Passw0rd!');

    await waitFor(() => expect(mockedApi.login).toHaveBeenCalledWith('jay@example.com', 'Passw0rd!'));
    expect(mockedApi.register).toHaveBeenCalledWith('jay@example.com', 'Passw0rd!', 'Jay');
  });
});
