import { useState, type FormEvent } from 'react';
import { Link, Navigate, useLocation, useNavigate, useSearchParams } from 'react-router';
import { fieldErrors, problemMessage, sessionApi } from '../../api/http';
import { Field, Notice, SubmitButton } from '../../components/Form';
import { session, useSessionStatus } from './session';

type Outcome = { kind: 'idle' } | { kind: 'pending' } | { kind: 'done' } | { kind: 'failed'; problem: unknown; status: number };

function useSubmit(action: () => Promise<{ error?: unknown; response: Response }>) {
  const [outcome, setOutcome] = useState<Outcome>({ kind: 'idle' });

  async function submit(event: FormEvent) {
    event.preventDefault();
    setOutcome({ kind: 'pending' });
    try {
      const { error, response } = await action();
      setOutcome(response.ok ? { kind: 'done' } : { kind: 'failed', problem: error, status: response.status });
    } catch {
      setOutcome({ kind: 'failed', problem: undefined, status: 0 });
    }
  }

  const problem = outcome.kind === 'failed' ? outcome.problem : undefined;
  const status = outcome.kind === 'failed' ? outcome.status : undefined;
  return { outcome, submit, problem, status, pending: outcome.kind === 'pending' };
}

/** A general message, unless the failure is fully explained by per-field messages. */
function FormError({ problem, status, fields }: { problem: unknown; status?: number; fields: string[] }) {
  if (status === undefined || fields.some((field) => fieldErrors(problem, field).length > 0)) {
    return null;
  }
  return <Notice kind="error">{problemMessage(problem, status)}</Notice>;
}

export function SignInPage() {
  const status = useSessionStatus();
  const navigate = useNavigate();
  const from = (useLocation().state as { from?: string } | null)?.from ?? '/';
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [failure, setFailure] = useState<{ problem: unknown; status: number } | null>(null);
  const [pending, setPending] = useState(false);

  if (status === 'authenticated') {
    return <Navigate to={from} replace />;
  }

  async function submit(event: FormEvent) {
    event.preventDefault();
    setPending(true);
    setFailure(null);
    try {
      const result = await session.signIn(email, password);
      if (result.ok) {
        navigate(from, { replace: true });
      } else {
        setFailure({ problem: result.error, status: result.status });
      }
    } catch {
      setFailure({ problem: undefined, status: 0 });
    } finally {
      setPending(false);
    }
  }

  return (
    <form onSubmit={submit} noValidate>
      <h1>Sign in</h1>
      {failure && <Notice kind="error">{problemMessage(failure.problem, failure.status)}</Notice>}
      <Field label="Email" type="email" autoComplete="username" required value={email} onChange={(e) => setEmail(e.target.value)} />
      <Field label="Password" type="password" autoComplete="current-password" required value={password} onChange={(e) => setPassword(e.target.value)} />
      <SubmitButton pending={pending}>Sign in</SubmitButton>
      <p>
        <Link to="/forgot-password">Forgot your password?</Link> · <Link to="/register">Create an account</Link>
      </p>
    </form>
  );
}

export function RegisterPage() {
  const [email, setEmail] = useState('');
  const [displayName, setDisplayName] = useState('');
  const [password, setPassword] = useState('');
  const form = useSubmit(() => sessionApi.POST('/api/v1/auth/register', { body: { email, displayName, password } }));

  if (form.outcome.kind === 'done') {
    return (
      <>
        <h1>Check your email</h1>
        <p>If this address can be registered, we sent a message to {email} with the next step.</p>
      </>
    );
  }

  return (
    <form onSubmit={form.submit} noValidate>
      <h1>Create an account</h1>
      <FormError problem={form.problem} status={form.status} fields={['email', 'displayName', 'password']} />
      <Field label="Email" type="email" autoComplete="email" required value={email} onChange={(e) => setEmail(e.target.value)} errors={fieldErrors(form.problem, 'email')} />
      <Field label="Name" autoComplete="name" required maxLength={100} value={displayName} onChange={(e) => setDisplayName(e.target.value)} errors={fieldErrors(form.problem, 'displayName')} />
      <Field label="Password" hint="At least 12 characters. A long phrase works well." type="password" autoComplete="new-password" required value={password} onChange={(e) => setPassword(e.target.value)} errors={fieldErrors(form.problem, 'password')} />
      <SubmitButton pending={form.pending}>Create account</SubmitButton>
      <p>
        <Link to="/sign-in">Already have an account? Sign in</Link>
      </p>
    </form>
  );
}

export function ConfirmEmailPage() {
  const token = useSearchParams()[0].get('token') ?? '';
  const form = useSubmit(() => sessionApi.POST('/api/v1/auth/email/confirm', { body: { token } }));

  if (form.outcome.kind === 'done') {
    return (
      <>
        <h1>Email confirmed</h1>
        <p>
          <Link to="/sign-in">Sign in to PowerHub</Link>
        </p>
      </>
    );
  }

  // Confirmation needs a click so that mail scanners following the link cannot use up the proof.
  return (
    <form onSubmit={form.submit}>
      <h1>Confirm your email address</h1>
      <FormError problem={form.problem} status={form.status} fields={[]} />
      <SubmitButton pending={form.pending}>Confirm email address</SubmitButton>
    </form>
  );
}

export function ForgotPasswordPage() {
  const [email, setEmail] = useState('');
  const form = useSubmit(() => sessionApi.POST('/api/v1/auth/recovery/request', { body: { email } }));

  if (form.outcome.kind === 'done') {
    return (
      <>
        <h1>Check your email</h1>
        <p>If an account exists for {email}, we sent a link to choose a new password.</p>
      </>
    );
  }

  return (
    <form onSubmit={form.submit} noValidate>
      <h1>Reset your password</h1>
      <FormError problem={form.problem} status={form.status} fields={['email']} />
      <Field label="Email" type="email" autoComplete="email" required value={email} onChange={(e) => setEmail(e.target.value)} errors={fieldErrors(form.problem, 'email')} />
      <SubmitButton pending={form.pending}>Send reset link</SubmitButton>
    </form>
  );
}

export function ResetPasswordPage() {
  const token = useSearchParams()[0].get('token') ?? '';
  const [newPassword, setNewPassword] = useState('');
  const form = useSubmit(() => sessionApi.POST('/api/v1/auth/recovery/complete', { body: { token, newPassword } }));

  if (form.outcome.kind === 'done') {
    return (
      <>
        <h1>Password changed</h1>
        <p>
          <Link to="/sign-in">Sign in with your new password</Link>
        </p>
      </>
    );
  }

  return (
    <form onSubmit={form.submit} noValidate>
      <h1>Choose a new password</h1>
      <FormError problem={form.problem} status={form.status} fields={['newPassword']} />
      <Field label="New password" hint="At least 12 characters." type="password" autoComplete="new-password" required value={newPassword} onChange={(e) => setNewPassword(e.target.value)} errors={fieldErrors(form.problem, 'newPassword')} />
      <SubmitButton pending={form.pending}>Change password</SubmitButton>
    </form>
  );
}
