import axios from 'axios';
import { createApiClient, getErrorMessage, setSessionExpiredHandler } from '../src/api/client';
import { tokenStorage } from '../src/api/tokenStorage';

const BASE = 'http://api.test';

/** Swaps axios' network layer for a scripted one so interceptors run for real. */
function scriptAdapter(client: ReturnType<typeof createApiClient>, handler: (config: any) => { status: number; data?: unknown }) {
  client.defaults.adapter = async (config) => {
    const { status, data } = handler(config);
    const response = { data, status, statusText: String(status), headers: {}, config };
    if (status >= 400) {
      throw new axios.AxiosError('Request failed', String(status), config, null, response as any);
    }
    return response as any;
  };
}

describe('api client', () => {
  afterEach(() => {
    jest.restoreAllMocks();
    setSessionExpiredHandler(undefined);
  });

  // MT-06
  it('attaches the bearer token and device header', async () => {
    await tokenStorage.save({ accessToken: 'access-1', refreshToken: 'refresh-1' });
    const client = createApiClient(BASE);
    let seen: any;
    scriptAdapter(client, (config) => {
      seen = config.headers;
      return { status: 200, data: {} };
    });

    await client.get('/api/users/me');

    expect(seen.Authorization).toBe('Bearer access-1');
    expect(seen['X-Device-Info']).toBe('iPhone 15 (iOS 18.1)');
  });

  // MT-07
  it('refreshes the token on 401 and retries the request once', async () => {
    await tokenStorage.save({ accessToken: 'expired', refreshToken: 'refresh-1' });
    const post = jest.spyOn(axios, 'post').mockResolvedValue({
      data: { accessToken: 'access-2', refreshToken: 'refresh-2', expiresAtUtc: '', user: {} },
    });
    const client = createApiClient(BASE);
    const authHeaders: string[] = [];
    scriptAdapter(client, (config) => {
      authHeaders.push(config.headers.Authorization);
      return config.headers.Authorization === 'Bearer access-2' ? { status: 200, data: { ok: true } } : { status: 401 };
    });

    const response = await client.get('/api/users/me');

    expect(response.data).toEqual({ ok: true });
    expect(post).toHaveBeenCalledWith(`${BASE}/api/auth/refresh`, { refreshToken: 'refresh-1' }, expect.anything());
    expect(authHeaders).toEqual(['Bearer expired', 'Bearer access-2']);
    expect(await tokenStorage.get()).toEqual({ accessToken: 'access-2', refreshToken: 'refresh-2' });
  });

  it('signs out when the refresh token is rejected', async () => {
    await tokenStorage.save({ accessToken: 'expired', refreshToken: 'revoked' });
    jest.spyOn(axios, 'post').mockRejectedValue(new Error('401'));
    const expired = jest.fn();
    setSessionExpiredHandler(expired);
    const client = createApiClient(BASE);
    scriptAdapter(client, () => ({ status: 401 }));

    await expect(client.get('/api/users/me')).rejects.toThrow();

    expect(expired).toHaveBeenCalled();
    expect(await tokenStorage.get()).toBeNull();
  });

  it('does not try to refresh when a login call returns 401', async () => {
    const post = jest.spyOn(axios, 'post');
    const client = createApiClient(BASE);
    scriptAdapter(client, () => ({ status: 401, data: { detail: 'Invalid email or password.' } }));

    await expect(client.post('/api/auth/login', {})).rejects.toThrow();

    expect(post).not.toHaveBeenCalled();
  });

  it('getErrorMessage prefers server detail, then validation errors, then a network message', () => {
    const err = (response?: unknown) => Object.assign(new Error('x'), { isAxiosError: true, response });

    expect(getErrorMessage(err({ status: 401, data: { detail: 'Invalid email or password.' } }))).toBe('Invalid email or password.');
    expect(getErrorMessage(err({ status: 400, data: { errors: { Email: ['Bad email'] } } }))).toBe('Bad email');
    expect(getErrorMessage(err(undefined))).toBe('Cannot reach the server. Check your connection.');
    expect(getErrorMessage(new Error('boom'), 'fallback')).toBe('fallback');
  });
});
