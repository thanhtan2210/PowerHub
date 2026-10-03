import { useInfiniteQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { useState, type FormEvent } from 'react';
import { api, ApiError, unwrap } from '../../api/client';
import { problemMessage, traceId } from '../../api/http';
import type { components } from '../../api/identity';
import { Field, Notice } from '../../components/Form';
import { useProfile } from '../account/useProfile';

type UserSummary = components['schemas']['UserSummary'];

export function UsersPage() {
  const queryClient = useQueryClient();
  const profile = useProfile();
  const canManage = profile.data?.permissions.includes('users.manage') ?? false;
  const [draft, setDraft] = useState('');
  const [search, setSearch] = useState('');

  const users = useInfiniteQuery({
    queryKey: ['admin-users', search],
    queryFn: ({ pageParam, signal }) =>
      unwrap(api.GET('/api/v1/users', { params: { query: { search: search || undefined, cursor: pageParam, limit: 25 } }, signal })),
    initialPageParam: undefined as string | undefined,
    getNextPageParam: (page) => page.nextCursor ?? undefined,
  });

  const setDisabled = useMutation({
    mutationFn: ({ user, disabled }: { user: UserSummary; disabled: boolean }) =>
      unwrap(
        api.POST(disabled ? '/api/v1/users/{userId}/disable' : '/api/v1/users/{userId}/reactivate', {
          params: { path: { userId: user.id } },
        }),
      ),
    onSettled: () => queryClient.invalidateQueries({ queryKey: ['admin-users'] }),
  });

  function submitSearch(event: FormEvent) {
    event.preventDefault();
    setSearch(draft.trim());
  }

  function toggle(user: UserSummary) {
    const disabling = !user.disabled;
    if (window.confirm(`${disabling ? 'Disable' : 'Reactivate'} the account ${user.email}?`)) {
      setDisabled.mutate({ user, disabled: disabling });
    }
  }

  const rows = users.data?.pages.flatMap((page) => page.items) ?? [];
  const failure = setDisabled.error instanceof ApiError ? setDisabled.error : null;

  return (
    <>
      <h1>Users</h1>
      <form onSubmit={submitSearch} role="search">
        <Field label="Search by email or name" type="search" value={draft} onChange={(e) => setDraft(e.target.value)} />
        <button type="submit">Search</button>
      </form>

      {setDisabled.isError && (
        <Notice kind="error">
          {problemMessage(failure?.problem, failure?.status)}
          {traceId(failure?.problem) && ` Reference: ${traceId(failure?.problem)}`}
        </Notice>
      )}

      {users.isPending && <p role="status">Loading users…</p>}
      {users.isError && (
        <>
          <Notice kind="error">Users could not be loaded.</Notice>
          <button type="button" onClick={() => users.refetch()}>
            Try again
          </button>
        </>
      )}
      {users.isSuccess && rows.length === 0 && <p>No users match.</p>}

      {rows.length > 0 && (
        <div className="table-scroll">
          <table>
            <caption className="visually-hidden">User accounts</caption>
            <thead>
              <tr>
                <th scope="col">Email</th>
                <th scope="col">Name</th>
                <th scope="col">Status</th>
                {canManage && <th scope="col">Action</th>}
              </tr>
            </thead>
            <tbody>
              {rows.map((user) => (
                <tr key={user.id}>
                  <td>{user.email}</td>
                  <td>{user.displayName}</td>
                  <td>{user.disabled ? 'Disabled' : user.emailConfirmed ? 'Active' : 'Awaiting confirmation'}</td>
                  {canManage && (
                    <td>
                      <button type="button" disabled={setDisabled.isPending || user.id === profile.data?.id} onClick={() => toggle(user)}>
                        {user.disabled ? 'Reactivate' : 'Disable'}
                        <span className="visually-hidden"> {user.email}</span>
                      </button>
                    </td>
                  )}
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      {users.hasNextPage && (
        <button type="button" disabled={users.isFetchingNextPage} onClick={() => users.fetchNextPage()}>
          {users.isFetchingNextPage ? 'Loading…' : 'Load more'}
        </button>
      )}
    </>
  );
}
