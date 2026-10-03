import { useMutation, useQueryClient } from '@tanstack/react-query';
import { useState, type FormEvent } from 'react';
import { api, ApiError, unwrap } from '../../api/client';
import { fieldErrors, problemMessage } from '../../api/http';
import { Field, Notice, SubmitButton } from '../../components/Form';
import { session } from '../auth/session';
import { profileKey, useProfile } from './useProfile';

export function AccountPage() {
  const profile = useProfile();

  if (profile.isPending) {
    return <p role="status">Loading your account…</p>;
  }
  if (profile.isError) {
    return (
      <>
        <Notice kind="error">Your account could not be loaded.</Notice>
        <button type="button" onClick={() => profile.refetch()}>
          Try again
        </button>
      </>
    );
  }

  return (
    <>
      <h1>Your account</h1>
      <dl>
        <dt>Email</dt>
        <dd>{profile.data.email}</dd>
        <dt>Roles</dt>
        <dd>{profile.data.roles.join(', ')}</dd>
      </dl>
      <DisplayNameForm current={profile.data.displayName} />
      <PasswordForm />
      <section aria-labelledby="sessions-heading">
        <h2 id="sessions-heading">Sessions</h2>
        <button type="button" onClick={() => session.signOut()}>
          Sign out
        </button>{' '}
        <button
          type="button"
          onClick={() => {
            if (window.confirm('Sign out of PowerHub on every device?')) {
              void session.signOut(true);
            }
          }}
        >
          Sign out everywhere
        </button>
      </section>
    </>
  );
}

function problemOf(error: unknown) {
  return error instanceof ApiError ? { problem: error.problem, status: error.status } : { problem: undefined, status: 0 };
}

function DisplayNameForm({ current }: { current: string }) {
  const queryClient = useQueryClient();
  const [displayName, setDisplayName] = useState(current);
  const update = useMutation({
    mutationFn: () => unwrap(api.PATCH('/api/v1/users/me', { body: { displayName } })),
    onSuccess: (profile) => queryClient.setQueryData(profileKey, profile),
  });
  const { problem, status } = problemOf(update.error);

  function submit(event: FormEvent) {
    event.preventDefault();
    update.mutate();
  }

  return (
    <form onSubmit={submit} noValidate aria-labelledby="name-heading">
      <h2 id="name-heading">Name</h2>
      {update.isSuccess && <Notice kind="success">Name saved.</Notice>}
      {update.isError && fieldErrors(problem, 'displayName').length === 0 && <Notice kind="error">{problemMessage(problem, status)}</Notice>}
      <Field label="Name" autoComplete="name" required maxLength={100} value={displayName} onChange={(e) => setDisplayName(e.target.value)} errors={fieldErrors(problem, 'displayName')} />
      <SubmitButton pending={update.isPending}>Save name</SubmitButton>
    </form>
  );
}

function PasswordForm() {
  const [currentPassword, setCurrentPassword] = useState('');
  const [newPassword, setNewPassword] = useState('');
  const change = useMutation({
    mutationFn: () => unwrap(api.POST('/api/v1/users/me/password', { body: { currentPassword, newPassword } })),
    onSuccess: () => {
      setCurrentPassword('');
      setNewPassword('');
    },
  });
  const { problem, status } = problemOf(change.error);
  const hasFieldErrors = ['currentPassword', 'newPassword'].some((field) => fieldErrors(problem, field).length > 0);

  function submit(event: FormEvent) {
    event.preventDefault();
    change.mutate();
  }

  return (
    <form onSubmit={submit} noValidate aria-labelledby="password-heading">
      <h2 id="password-heading">Password</h2>
      {change.isSuccess && <Notice kind="success">Password changed. Other devices were signed out.</Notice>}
      {change.isError && !hasFieldErrors && <Notice kind="error">{problemMessage(problem, status)}</Notice>}
      <Field label="Current password" type="password" autoComplete="current-password" required value={currentPassword} onChange={(e) => setCurrentPassword(e.target.value)} errors={fieldErrors(problem, 'currentPassword')} />
      <Field label="New password" hint="At least 12 characters." type="password" autoComplete="new-password" required value={newPassword} onChange={(e) => setNewPassword(e.target.value)} errors={fieldErrors(problem, 'newPassword')} />
      <SubmitButton pending={change.isPending}>Change password</SubmitButton>
    </form>
  );
}
