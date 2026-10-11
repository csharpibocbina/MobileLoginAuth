import { api } from './client';
import type { AuthResponse, LoginHistoryItem, User } from './types';

export const authApi = {
  async register(email: string, password: string, displayName: string): Promise<User> {
    const { data } = await api.post<User>('/api/auth/register', { email, password, displayName });
    return data;
  },

  async login(email: string, password: string): Promise<AuthResponse> {
    const { data } = await api.post<AuthResponse>('/api/auth/login', { email, password });
    return data;
  },

  async logout(refreshToken: string | null): Promise<void> {
    await api.post('/api/auth/logout', { refreshToken });
  },

  async me(): Promise<User> {
    const { data } = await api.get<User>('/api/users/me');
    return data;
  },

  async loginHistory(): Promise<LoginHistoryItem[]> {
    const { data } = await api.get<LoginHistoryItem[]>('/api/users/me/login-history');
    return data;
  },

  async deleteAccount(): Promise<void> {
    await api.delete('/api/users/me');
  },
};
