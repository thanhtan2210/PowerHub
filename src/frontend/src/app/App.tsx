import { lazy, Suspense, useEffect, useRef } from 'react';
import { Link, Navigate, Outlet, Route, Routes, useLocation } from 'react-router';
import { AccountPage } from '../features/account/AccountPage';
import { useProfile } from '../features/account/useProfile';
import { ConfirmEmailPage, ForgotPasswordPage, RegisterPage, ResetPasswordPage, SignInPage } from '../features/auth/AuthPages';
import { RequireAuth, RequirePermission } from '../features/auth/guards';
import { DevicesPage } from '../features/devices/DevicesPage';
import { useSessionStatus } from '../features/auth/session';

// Administration is a separate chunk so standard users never download it (FE-PERF-001).
const UsersPage = lazy(() => import('../features/admin/UsersPage').then((module) => ({ default: module.UsersPage })));

function Layout() {
  const status = useSessionStatus();
  const profile = useProfile();
  const location = useLocation();
  const main = useRef<HTMLElement>(null);

  // Move focus to the new page content after navigation (NFR-UX-006).
  useEffect(() => {
    main.current?.focus();
  }, [location.pathname]);

  return (
    <>
      <a className="skip-link" href="#main">
        Skip to content
      </a>
      <header>
        <nav aria-label="Main">
          <Link to="/" className="brand">
            PowerHub
          </Link>
          {status === 'authenticated' && <Link to="/devices">Devices</Link>}
          {status === 'authenticated' && <Link to="/account">Account</Link>}
          {profile.data?.permissions.includes('users.read') && <Link to="/admin/users">Users</Link>}
          {status === 'anonymous' && <Link to="/sign-in">Sign in</Link>}
        </nav>
      </header>
      <main id="main" ref={main} tabIndex={-1}>
        <Suspense fallback={<p role="status">Loading…</p>}>
          <Outlet />
        </Suspense>
      </main>
    </>
  );
}

export function App() {
  return (
    <Routes>
      <Route element={<Layout />}>
        <Route path="/sign-in" element={<SignInPage />} />
        <Route path="/register" element={<RegisterPage />} />
        <Route path="/confirm-email" element={<ConfirmEmailPage />} />
        <Route path="/forgot-password" element={<ForgotPasswordPage />} />
        <Route path="/reset-password" element={<ResetPasswordPage />} />
        <Route path="/" element={<Navigate to="/devices" replace />} />
        <Route
          path="/devices"
          element={
            <RequireAuth>
              <DevicesPage />
            </RequireAuth>
          }
        />
        <Route
          path="/account"
          element={
            <RequireAuth>
              <AccountPage />
            </RequireAuth>
          }
        />
        <Route
          path="/admin/users"
          element={
            <RequireAuth>
              <RequirePermission permission="users.read">
                <UsersPage />
              </RequirePermission>
            </RequireAuth>
          }
        />
        <Route path="*" element={<h1>Page not found</h1>} />
      </Route>
    </Routes>
  );
}
