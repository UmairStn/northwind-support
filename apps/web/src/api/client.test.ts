import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import {
  clearStoredSession,
  getStoredToken,
  request,
  setStoredSession,
} from './client';

describe('api client auth handling', () => {
  beforeEach(() => {
    localStorage.clear();
    vi.restoreAllMocks();
  });

  afterEach(() => {
    clearStoredSession();
  });

  it('attaches Authorization Bearer header when token is stored', async () => {
    setStoredSession('test-token-123', { id: 1, fullName: 'Alex Turner' });

    const fetchMock = vi.fn().mockResolvedValue({
      ok: true,
      status: 200,
      json: () => Promise.resolve({ success: true }),
    });
    vi.stubGlobal('fetch', fetchMock);

    await request('/tickets');

    expect(fetchMock).toHaveBeenCalledTimes(1);
    const [, init] = fetchMock.mock.calls[0];
    expect(init.headers).toMatchObject({
      Authorization: 'Bearer test-token-123',
    });
  });

  it('omits Authorization header when no token is stored', async () => {
    const fetchMock = vi.fn().mockResolvedValue({
      ok: true,
      status: 200,
      json: () => Promise.resolve([]),
    });
    vi.stubGlobal('fetch', fetchMock);

    await request('/tickets');

    expect(fetchMock).toHaveBeenCalledTimes(1);
    const [, init] = fetchMock.mock.calls[0];
    expect(init.headers?.Authorization).toBeUndefined();
  });

  it('clears stored session and dispatches auth:unauthorized on 401 response', async () => {
    setStoredSession('expired-token', { id: 1, fullName: 'Alex Turner' });
    expect(getStoredToken()).toBe('expired-token');

    const unauthorizedEventSpy = vi.fn();
    window.addEventListener('auth:unauthorized', unauthorizedEventSpy);

    const fetchMock = vi.fn().mockResolvedValue({
      ok: false,
      status: 401,
      statusText: 'Unauthorized',
      headers: new Headers({ 'content-type': 'application/problem+json' }),
      text: () => Promise.resolve(JSON.stringify({ title: 'Unauthorized', detail: 'Token expired.' })),
    });
    vi.stubGlobal('fetch', fetchMock);

    await expect(request('/tickets')).rejects.toThrow();

    expect(getStoredToken()).toBeNull();
    expect(unauthorizedEventSpy).toHaveBeenCalledTimes(1);

    window.removeEventListener('auth:unauthorized', unauthorizedEventSpy);
  });
});
