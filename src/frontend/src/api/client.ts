import createClient from 'openapi-fetch';
import { session } from '../features/auth/session';
import { baseUrl } from './http';
import type { paths } from './identity';

/** The single HTTP client for authenticated API calls (FE-ARC-003). */
export const api = createClient<paths>({ baseUrl, credentials: 'same-origin' });

// A request body can be read once, so keep a copy to replay after a token refresh.
const replays = new Map<string, Request>();

api.use({
  onRequest({ request, id }) {
    const token = session.getAccessToken();
    if (token) {
      request.headers.set('Authorization', `Bearer ${token}`);
    }
    replays.set(id, request.clone());
    return request;
  },

  async onResponse({ response, id }) {
    const replay = replays.get(id);
    replays.delete(id);

    if (response.status !== 401 || !replay || !(await session.refresh())) {
      return response;
    }

    // Retried once with the new token, outside the middleware, so it cannot loop.
    replay.headers.set('Authorization', `Bearer ${session.getAccessToken()}`);
    return fetch(replay);
  },

  onError({ id }) {
    replays.delete(id);
  },
});

export class ApiError extends Error {
  constructor(
    readonly status: number,
    readonly problem: unknown,
  ) {
    super(`Request failed with status ${status}`);
  }
}

/** Unwraps an openapi-fetch result for React Query, which expects failures to throw. */
export async function unwrap<T>(call: Promise<{ data?: T; error?: unknown; response: Response }>): Promise<T> {
  const { data, error, response } = await call;
  if (!response.ok) {
    throw new ApiError(response.status, error);
  }
  return data as T;
}
