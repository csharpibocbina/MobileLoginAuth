import React from 'react';
import { render } from '@testing-library/react-native';
import { AuthProvider } from '../src/auth/AuthContext';

export const mockRouter = { push: jest.fn(), back: jest.fn(), replace: jest.fn() };

export function renderWithAuth(ui: React.ReactElement) {
  return render(<AuthProvider>{ui}</AuthProvider>);
}

export const sampleUser = {
  id: 'u1',
  email: 'jay@example.com',
  displayName: 'Jay',
  createdAtUtc: '2026-01-15T10:00:00Z',
};

export const sampleAuthResponse = {
  accessToken: 'access-1',
  refreshToken: 'refresh-1',
  expiresAtUtc: '2026-01-15T10:15:00Z',
  user: sampleUser,
};
