import { useSyncExternalStore } from 'react';
import { csrfHeader, sessionApi } from '../../api/http';

export type SessionStatus = 'loading' | 'authenticated' | 'anonymous';

interface SessionState {
  status: SessionStatus;
  accessToken: string | null;
}

// The access token lives only in memory. The refresh token is an HttpOnly cookie that
// script cannot read, so nothing that grants access is ever placed in browser storage.
let state: SessionState = { status: 'loading', accessToken: null };
const listeners = new Set<() => void>();
let refreshInFlight: Promise<boolean> | null = null;

function set(next: SessionState) {
  state = next;
  listeners.forEach((listener) => listener());
}

function accept(accessToken: string) {
  set({ status: 'authenticated', accessToken });
}

// Serialises refresh across tabs: two tabs presenting the same rotating token at once
// would look like token theft to the server and end the session.
function withCrossTabLock<T>(work: () => Promise<T>): Promise<T> {
  return navigator.locks ? (navigator.locks.request('powerhub-session-refresh', work) as Promise<T>) : work();
}

export const session = {
  subscribe(listener: () => void) {
    listeners.add(listener);
    return () => listeners.delete(listener);
  },

  getStatus: () => state.status,

  getAccessToken: () => state.accessToken,

  clear() {
    set({ status: 'anonymous', accessToken: null });
  },

  /** Renews the access token. Concurrent callers share one request. */
  refresh(): Promise<boolean> {
    refreshInFlight ??= withCrossTabLock(async () => {
      const { data } = await sessionApi.POST('/api/v1/auth/refresh', { headers: csrfHeader });
      if (data) {
        accept(data.accessToken);
        return true;
      }
      session.clear();
      return false;
    })
      .catch(() => {
        session.clear();
        return false;
      })
      .finally(() => {
        refreshInFlight = null;
      });
    return refreshInFlight;
  },

  async signIn(email: string, password: string) {
    const { data, error, response } = await sessionApi.POST('/api/v1/auth/sign-in', { body: { email, password } });
    if (data) {
      accept(data.accessToken);
    }
    return { ok: Boolean(data), error, status: response.status };
  },

  async signOut(everywhere = false) {
    try {
      if (everywhere) {
        await sessionApi.POST('/api/v1/auth/sign-out-all', {
          headers: { Authorization: `Bearer ${state.accessToken}` },
        });
      } else {
        await sessionApi.POST('/api/v1/auth/sign-out', { headers: csrfHeader });
      }
    } finally {
      session.clear();
    }
  },
};

export function useSessionStatus(): SessionStatus {
  return useSyncExternalStore(session.subscribe, session.getStatus);
}
