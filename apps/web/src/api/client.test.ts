import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { getSession, saveSession } from '../auth/session';
import { ApiError, request } from './client';

const session = {
  token: 'signed-token',
  expiresAtUtc: new Date(Date.now() + 60 * 60_000).toISOString(),
  agent: { id: 4, fullName: 'Sara Lindqvist', email: 'sara@example.com' },
};

describe('request', () => {
  const fetchMock = vi.fn();

  beforeEach(() => {
    vi.stubGlobal('fetch', fetchMock);
    sessionStorage.clear();
  });

  afterEach(() => {
    vi.unstubAllGlobals();
    fetchMock.mockReset();
  });

  it('sends the signed-in agent\'s token', async () => {
    saveSession(session);
    fetchMock.mockResolvedValue(new Response('{}', { status: 200 }));

    await request('/tickets');

    const headers = fetchMock.mock.calls[0][1].headers as Record<string, string>;
    expect(headers.Authorization).toBe('Bearer signed-token');
  });

  it('signs the agent out on a 401, so the route guard sends them to login', async () => {
    saveSession(session);
    fetchMock.mockResolvedValue(new Response(JSON.stringify({ title: 'Unauthorized' }), { status: 401 }));

    await expect(request('/tickets')).rejects.toBeInstanceOf(ApiError);

    expect(getSession()).toBeNull();
  });
});
