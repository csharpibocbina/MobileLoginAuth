import axios, { AxiosError, AxiosInstance, InternalAxiosRequestConfig } from 'axios';
import { getApiBaseUrl } from '../config';
import { getDeviceInfo } from '../utils/device';
import { tokenStorage } from './tokenStorage';
import type { AuthResponse } from './types';

type RetriableConfig = InternalAxiosRequestConfig & { _retried?: boolean };

let onSessionExpired: (() => void) | undefined;
let refreshInFlight: Promise<string | null> | null = null;

/** Called by AuthContext so an unrecoverable 401 signs the user out of the UI. */
export function setSessionExpiredHandler(handler: (() => void) | undefined) {
  onSessionExpired = handler;
}

export function createApiClient(baseURL = getApiBaseUrl()): AxiosInstance {
  const client = axios.create({ baseURL, timeout: 15000 });

  client.interceptors.request.use(async (config) => {
    config.headers.set('X-Device-Info', getDeviceInfo());
    const tokens = await tokenStorage.get();
    if (tokens && !config.headers.has('Authorization')) {
      config.headers.set('Authorization', `Bearer ${tokens.accessToken}`);
    }
    return config;
  });

  client.interceptors.response.use(
    (response) => response,
    async (error: AxiosError) => {
      const original = error.config as RetriableConfig | undefined;
      const isAuthCall = original?.url?.includes('/api/auth/');
      if (error.response?.status !== 401 || !original || original._retried || isAuthCall) {
        throw error;
      }

      original._retried = true;
      const newAccessToken = await refreshAccessToken(baseURL);
      if (!newAccessToken) {
        onSessionExpired?.();
        throw error;
      }

      original.headers.set('Authorization', `Bearer ${newAccessToken}`);
      return client(original);
    },
  );

  return client;
}

/** Refreshes once even if several requests fail with 401 at the same time. */
export function refreshAccessToken(baseURL = getApiBaseUrl()): Promise<string | null> {
  refreshInFlight ??= (async () => {
    try {
      const tokens = await tokenStorage.get();
      if (!tokens) return null;
      const { data } = await axios.post<AuthResponse>(
        `${baseURL}/api/auth/refresh`,
        { refreshToken: tokens.refreshToken },
        { headers: { 'X-Device-Info': getDeviceInfo() } },
      );
      await tokenStorage.save({ accessToken: data.accessToken, refreshToken: data.refreshToken });
      return data.accessToken;
    } catch {
      await tokenStorage.clear();
      return null;
    } finally {
      refreshInFlight = null;
    }
  })();
  return refreshInFlight;
}

export const api = createApiClient();

/** Pulls the server's error text (ProblemDetails or validation errors) out of an axios error. */
export function getErrorMessage(error: unknown, fallback = 'Something went wrong. Please try again.'): string {
  if (axios.isAxiosError(error)) {
    if (!error.response) return 'Cannot reach the server. Check your connection.';
    const data = error.response.data as { detail?: string; errors?: Record<string, string[]> } | undefined;
    const firstValidation = data?.errors ? Object.values(data.errors)[0]?.[0] : undefined;
    return data?.detail ?? firstValidation ?? fallback;
  }
  return fallback;
}
