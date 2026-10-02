import createClient from 'openapi-fetch';
import type { components, paths } from './identity';

export type Problem = components['schemas']['ProblemDetails'] & {
  traceId?: string;
  errors?: Record<string, string[]>;
};

export const baseUrl: string = import.meta.env.VITE_API_BASE_URL ?? '';

/** Sent with cookie-authenticated calls; the API rejects them without it. */
export const csrfHeader = { 'X-PowerHub-Csrf': '1' } as const;

/**
 * Client without bearer handling, used only by the session module for the calls that
 * establish, renew, or end a session. Everything else goes through `api` in client.ts.
 */
export const sessionApi = createClient<paths>({ baseUrl, credentials: 'same-origin' });

const messages: Record<string, string> = {
  'invalid-credentials': 'The email or password is incorrect.',
  'invalid-session': 'Your session has ended. Sign in again.',
  'account-disabled': 'This account is disabled. Contact an administrator.',
  'email-not-confirmed': 'Confirm your email address before signing in. Check your inbox for the link.',
  'invalid-token': 'This link is invalid or has expired. Request a new one.',
  conflict: 'That action is not allowed.',
  'not-found': 'That item no longer exists.',
};

/**
 * Maps an error response to text that is safe to show. Unknown problems fall back to a
 * generic message so backend detail is never rendered (FE-IAM-008).
 */
export function problemMessage(problem: unknown, status?: number): string {
  if (status === 429) {
    return 'Too many attempts. Wait a minute and try again.';
  }
  const type = (problem as Problem | undefined)?.type ?? '';
  return messages[type.slice(type.lastIndexOf('/') + 1)] ?? 'Something went wrong. Try again.';
}

/** Server-authored validation messages for one field, matched case-insensitively. */
export function fieldErrors(problem: unknown, field: string): string[] {
  const errors = (problem as Problem | undefined)?.errors ?? {};
  const key = Object.keys(errors).find((name) => name.toLowerCase() === field.toLowerCase());
  return key ? (errors[key] ?? []) : [];
}

export function traceId(problem: unknown): string | undefined {
  return (problem as Problem | undefined)?.traceId;
}
