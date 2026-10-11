import React, { createContext, useCallback, useContext, useEffect, useMemo, useState } from 'react';
import { authApi } from '../api/authApi';
import { setSessionExpiredHandler } from '../api/client';
import { tokenStorage } from '../api/tokenStorage';
import type { User } from '../api/types';

export type AuthStatus = 'loading' | 'signedOut' | 'signedIn';

interface AuthContextValue {
  status: AuthStatus;
  user: User | null;
  login: (email: string, password: string) => Promise<void>;
  register: (email: string, password: string, displayName: string) => Promise<void>;
  logout: () => Promise<void>;
  deleteAccount: () => Promise<void>;
}

const AuthContext = createContext<AuthContextValue | undefined>(undefined);

export function AuthProvider({ children }: { children: React.ReactNode }) {
  const [status, setStatus] = useState<AuthStatus>('loading');
  const [user, setUser] = useState<User | null>(null);

  const signOutLocally = useCallback(async () => {
    await tokenStorage.clear();
    setUser(null);
    setStatus('signedOut');
  }, []);

  // Restore a saved session on app start.
  useEffect(() => {
    let cancelled = false;
    (async () => {
      const tokens = await tokenStorage.get();
      if (!tokens) {
        if (!cancelled) setStatus('signedOut');
        return;
      }
      try {
        const me = await authApi.me();
        if (!cancelled) {
          setUser(me);
          setStatus('signedIn');
        }
      } catch {
        if (!cancelled) await signOutLocally();
      }
    })();
    return () => {
      cancelled = true;
    };
  }, [signOutLocally]);

  useEffect(() => {
    setSessionExpiredHandler(() => void signOutLocally());
    return () => setSessionExpiredHandler(undefined);
  }, [signOutLocally]);

  const login = useCallback(async (email: string, password: string) => {
    const result = await authApi.login(email.trim(), password);
    await tokenStorage.save({ accessToken: result.accessToken, refreshToken: result.refreshToken });
    setUser(result.user);
    setStatus('signedIn');
  }, []);

  const register = useCallback(
    async (email: string, password: string, displayName: string) => {
      await authApi.register(email.trim(), password, displayName.trim());
      await login(email, password);
    },
    [login],
  );

  const logout = useCallback(async () => {
    const tokens = await tokenStorage.get();
    try {
      await authApi.logout(tokens?.refreshToken ?? null);
    } catch {
      // Sign out on this device even if the server call fails (e.g. offline).
    }
    await signOutLocally();
  }, [signOutLocally]);

  const deleteAccount = useCallback(async () => {
    await authApi.deleteAccount();
    await signOutLocally();
  }, [signOutLocally]);

  const value = useMemo(
    () => ({ status, user, login, register, logout, deleteAccount }),
    [status, user, login, register, logout, deleteAccount],
  );

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export function useAuth(): AuthContextValue {
  const ctx = useContext(AuthContext);
  if (!ctx) throw new Error('useAuth must be used inside <AuthProvider>');
  return ctx;
}
