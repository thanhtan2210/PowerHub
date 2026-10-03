import { render, screen } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { MemoryRouter, Route, Routes } from 'react-router';
import { beforeEach, describe, expect, it, vi } from 'vitest';

const json = (status: number, body?: unknown) =>
  new Response(body === undefined ? null : JSON.stringify(body), {
    status,
    headers: { 'Content-Type': status >= 400 ? 'application/problem+json' : 'application/json' },
  });

const tokens = (accessToken: string) => json(200, { accessToken, tokenType: 'Bearer', expiresIn: 900 });

const fetchMock = vi.fn<(request: Request) => Promise<Response>>();

// openapi-fetch captures fetch when a client is created, so modules are loaded fresh per test.
async function load() {
  const { session } = await import('./session');
  const { api } = await import('../../api/client');
  const guards = await import('./guards');
  return { session, api, ...guards };
}

beforeEach(() => {
  vi.resetModules();
  fetchMock.mockReset();
  vi.stubGlobal('fetch', fetchMock);
});

describe('session', () => {
  it('shares one refresh request between concurrent callers', async () => {
    fetchMock.mockResolvedValue(tokens('token-1'));
    const { session } = await load();

    const results = await Promise.all([session.refresh(), session.refresh(), session.refresh()]);

    expect(results).toEqual([true, true, true]);
    expect(fetchMock).toHaveBeenCalledTimes(1);
    expect(fetchMock.mock.calls[0]![0].headers.get('X-PowerHub-Csrf')).toBe('1');
    expect(session.getStatus()).toBe('authenticated');
  });

  it('clears the session when refresh fails', async () => {
    fetchMock.mockResolvedValue(json(401, { type: 'https://docs.powerhub.example/problems/invalid-session' }));
    const { session } = await load();

    expect(await session.refresh()).toBe(false);
    expect(session.getStatus()).toBe('anonymous');
    expect(session.getAccessToken()).toBeNull();
  });

  it('clears the session when the network is unavailable', async () => {
    fetchMock.mockRejectedValue(new TypeError('offline'));
    const { session } = await load();

    expect(await session.refresh()).toBe(false);
    expect(session.getStatus()).toBe('anonymous');
  });
});

describe('api client', () => {
  it('refreshes once and replays the request when the access token has expired', async () => {
    const { session, api } = await load();
    fetchMock.mockResolvedValueOnce(tokens('expired'));
    await session.refresh();

    fetchMock
      .mockResolvedValueOnce(json(401))
      .mockResolvedValueOnce(tokens('fresh'))
      .mockResolvedValueOnce(json(200, { id: '1', email: 'a@example.test', displayName: 'A', roles: ['User'], permissions: [] }));

    const { data } = await api.GET('/api/v1/users/me');

    expect(data?.email).toBe('a@example.test');
    const [, first, refresh, replay] = fetchMock.mock.calls.map(([request]) => request);
    expect(first!.headers.get('Authorization')).toBe('Bearer expired');
    expect(new URL(refresh!.url).pathname).toBe('/api/v1/auth/refresh');
    expect(replay!.headers.get('Authorization')).toBe('Bearer fresh');
  });

  it('returns the 401 and ends the session when refresh is refused', async () => {
    const { session, api } = await load();
    fetchMock.mockResolvedValueOnce(tokens('expired'));
    await session.refresh();
    fetchMock.mockResolvedValue(json(401));

    const { response } = await api.GET('/api/v1/users/me');

    expect(response.status).toBe(401);
    expect(session.getStatus()).toBe('anonymous');
    expect(fetchMock).toHaveBeenCalledTimes(3);
  });
});

describe('RequireAuth', () => {
  async function renderProtected() {
    const { session, RequireAuth, RequirePermission } = await load();
    render(
      <QueryClientProvider client={new QueryClient({ defaultOptions: { queries: { retry: false } } })}>
        <MemoryRouter initialEntries={['/admin']}>
          <Routes>
            <Route path="/sign-in" element={<h1>Sign in</h1>} />
            <Route
              path="/admin"
              element={
                <RequireAuth>
                  <RequirePermission permission="users.read">
                    <h1>Secret administration</h1>
                  </RequirePermission>
                </RequireAuth>
              }
            />
          </Routes>
        </MemoryRouter>
      </QueryClientProvider>,
    );
    return session;
  }

  it('redirects an anonymous visitor to sign-in without rendering protected content', async () => {
    fetchMock.mockResolvedValue(json(401));
    const session = await renderProtected();
    expect(screen.queryByText('Secret administration')).not.toBeInTheDocument();

    await session.refresh();

    expect(await screen.findByRole('heading', { name: 'Sign in' })).toBeInTheDocument();
    expect(screen.queryByText('Secret administration')).not.toBeInTheDocument();
  });

  it('withholds administrative content from a user without the permission', async () => {
    fetchMock
      .mockResolvedValueOnce(tokens('token'))
      .mockResolvedValueOnce(json(200, { id: '1', email: 'a@example.test', displayName: 'A', roles: ['User'], permissions: [] }));
    const session = await renderProtected();

    await session.refresh();

    expect(await screen.findByRole('heading', { name: 'Not authorized' })).toBeInTheDocument();
    expect(screen.queryByText('Secret administration')).not.toBeInTheDocument();
  });

  it('renders administrative content when the server reports the permission', async () => {
    fetchMock
      .mockResolvedValueOnce(tokens('token'))
      .mockResolvedValueOnce(json(200, { id: '1', email: 'a@example.test', displayName: 'A', roles: ['Administrator'], permissions: ['users.read'] }));
    const session = await renderProtected();

    await session.refresh();

    expect(await screen.findByRole('heading', { name: 'Secret administration' })).toBeInTheDocument();
  });
});
