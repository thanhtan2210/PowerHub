import type { ReactNode } from 'react';
import { Navigate, useLocation } from 'react-router';
import { useProfile } from '../account/useProfile';
import { useSessionStatus } from './session';

/**
 * Route guards are a usability feature only: every protected API enforces the same rule
 * on the server (FE-IAM-004).
 */
export function RequireAuth({ children }: { children: ReactNode }) {
  const status = useSessionStatus();
  const location = useLocation();

  if (status === 'loading') {
    return <p role="status">Loading…</p>;
  }
  if (status === 'anonymous') {
    return <Navigate to="/sign-in" replace state={{ from: location.pathname }} />;
  }
  return children;
}

/** Renders children only when the server-reported profile holds the permission. */
export function RequirePermission({ permission, children }: { permission: string; children: ReactNode }) {
  const profile = useProfile();

  if (profile.isPending) {
    return <p role="status">Loading…</p>;
  }
  if (!profile.data?.permissions.includes(permission)) {
    return (
      <>
        <h1>Not authorized</h1>
        <p>Your account does not have access to this page.</p>
      </>
    );
  }
  return children;
}
