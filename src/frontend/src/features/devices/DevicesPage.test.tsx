import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { beforeEach, describe, expect, it, vi } from 'vitest';

const json = (status: number, body?: unknown, headers: Record<string, string> = {}) =>
  new Response(body === undefined ? null : JSON.stringify(body), {
    status,
    headers: { 'Content-Type': status >= 400 ? 'application/problem+json' : 'application/json', ...headers },
  });

const device = (overrides: Record<string, unknown> = {}) => ({
  id: '0199aaaa-0000-7000-8000-000000000001',
  name: 'Kitchen plug',
  kind: 'smart-plug',
  permission: 'manage',
  isOwner: true,
  createdAt: '2026-10-02T08:00:00Z',
  version: 1,
  ...overrides,
});

const page = (...items: unknown[]) => json(200, { items, nextCursor: null });
const fetchMock = vi.fn<(request: Request) => Promise<Response>>();

// openapi-fetch captures fetch when a client is created, so modules are loaded fresh per test.
async function renderPage() {
  const { DevicesPage } = await import('./DevicesPage');
  render(
    <QueryClientProvider client={new QueryClient({ defaultOptions: { queries: { retry: false } } })}>
      <DevicesPage />
    </QueryClientProvider>,
  );
}

beforeEach(() => {
  vi.resetModules();
  fetchMock.mockReset();
  vi.stubGlobal('fetch', fetchMock);
  vi.stubGlobal('confirm', () => true);
});

describe('DevicesPage', () => {
  it('shows an empty state, then the one-time credential after registering', async () => {
    fetchMock
      .mockResolvedValueOnce(page())
      .mockResolvedValueOnce(json(201, { device: device(), credential: 'one-time-secret' }))
      .mockResolvedValueOnce(page(device()));
    await renderPage();

    expect(await screen.findByText(/no devices yet/i)).toBeInTheDocument();
    await userEvent.type(screen.getByLabelText('Name'), 'Kitchen plug');
    await userEvent.click(screen.getByRole('button', { name: 'Register device' }));

    expect(await screen.findByText('one-time-secret')).toBeInTheDocument();
    expect(screen.getByText(/cannot be shown again/i)).toBeInTheDocument();
    expect(await screen.findByRole('heading', { name: 'Kitchen plug' })).toBeInTheDocument();

    const post = fetchMock.mock.calls[1]![0];
    expect(post.method).toBe('POST');

    await userEvent.click(screen.getByRole('button', { name: 'I have saved it' }));
    expect(screen.queryByText('one-time-secret')).not.toBeInTheDocument();
  });

  it('sends the device version in If-Match when renaming', async () => {
    fetchMock
      .mockResolvedValueOnce(page(device({ version: 4 })))
      .mockResolvedValueOnce(json(200, device({ name: 'Pantry plug', version: 5 })))
      .mockResolvedValueOnce(page(device({ name: 'Pantry plug', version: 5 })));
    await renderPage();

    await userEvent.click(await screen.findByRole('button', { name: /Rename/ }));
    const input = screen.getByLabelText(/New name/);
    await userEvent.clear(input);
    await userEvent.type(input, 'Pantry plug');
    await userEvent.click(screen.getByRole('button', { name: 'Save' }));

    expect(await screen.findByRole('heading', { name: 'Pantry plug' })).toBeInTheDocument();
    const patch = fetchMock.mock.calls[1]![0];
    expect(patch.method).toBe('PATCH');
    expect(patch.headers.get('If-Match')).toBe('"4"');
  });

  it('explains a conflicting change instead of overwriting it', async () => {
    fetchMock
      .mockResolvedValueOnce(page(device()))
      .mockResolvedValueOnce(json(412, { type: 'https://docs.powerhub.example/problems/precondition-failed' }))
      .mockResolvedValueOnce(page(device({ name: 'Renamed elsewhere', version: 2 })));
    await renderPage();

    await userEvent.click(await screen.findByRole('button', { name: /Rename/ }));
    await userEvent.click(screen.getByRole('button', { name: 'Save' }));

    expect(await screen.findByRole('alert')).toHaveTextContent(/changed somewhere else/i);
  });

  it('offers no management actions for a device shared with view permission', async () => {
    fetchMock.mockResolvedValueOnce(page(device({ name: 'Shared meter', permission: 'view', isOwner: false })));
    await renderPage();

    const card = (await screen.findByRole('heading', { name: 'Shared meter' })).closest('li')!;
    expect(within(card).getByText(/Shared with you \(view\)/)).toBeInTheDocument();
    expect(within(card).queryByRole('button')).not.toBeInTheDocument();
  });

  it('offers a retry when the list cannot be loaded', async () => {
    fetchMock.mockResolvedValueOnce(json(500)).mockResolvedValueOnce(page(device()));
    await renderPage();

    await userEvent.click(await screen.findByRole('button', { name: 'Try again' }));

    expect(await screen.findByRole('heading', { name: 'Kitchen plug' })).toBeInTheDocument();
  });
});
