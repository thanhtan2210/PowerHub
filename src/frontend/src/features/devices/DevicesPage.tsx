import { useInfiniteQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { useId, useState, type FormEvent } from 'react';
import { api, ApiError, unwrap } from '../../api/client';
import type { components } from '../../api/device';
import { fieldErrors, problemMessage } from '../../api/http';
import { Field, Notice, SubmitButton } from '../../components/Form';

type Device = components['schemas']['DeviceResponse'];
type IssuedCredential = { deviceName: string; credential: string };

const devicesKey = ['devices'] as const;
const kinds = ['smart-plug', 'light', 'sensor', 'meter', 'other'] as const;

function problemOf(error: unknown) {
  return error instanceof ApiError ? { problem: error.problem, status: error.status } : { problem: undefined, status: 0 };
}

export function DevicesPage() {
  const [issued, setIssued] = useState<IssuedCredential | null>(null);
  const devices = useInfiniteQuery({
    queryKey: devicesKey,
    queryFn: ({ pageParam, signal }) =>
      unwrap(api.GET('/api/v1/devices', { params: { query: { cursor: pageParam, limit: 25 } }, signal })),
    initialPageParam: undefined as string | undefined,
    getNextPageParam: (page) => page.nextCursor ?? undefined,
  });
  const rows = devices.data?.pages.flatMap((page) => page.items) ?? [];

  return (
    <>
      <h1>Devices</h1>
      {issued && <CredentialNotice issued={issued} onDismiss={() => setIssued(null)} />}

      {devices.isPending && <p role="status">Loading devices…</p>}
      {devices.isError && (
        <>
          <Notice kind="error">Devices could not be loaded.</Notice>
          <button type="button" onClick={() => devices.refetch()}>
            Try again
          </button>
        </>
      )}
      {devices.isSuccess && rows.length === 0 && <p>You have no devices yet. Register one below.</p>}

      {rows.length > 0 && (
        <ul className="cards">
          {rows.map((device) => (
            <DeviceCard key={device.id} device={device} onCredential={setIssued} />
          ))}
        </ul>
      )}
      {devices.hasNextPage && (
        <button type="button" disabled={devices.isFetchingNextPage} onClick={() => devices.fetchNextPage()}>
          {devices.isFetchingNextPage ? 'Loading…' : 'Load more'}
        </button>
      )}

      <RegisterForm onCredential={setIssued} />
    </>
  );
}

/** The API returns a device credential exactly once, so it stays visible until dismissed. */
function CredentialNotice({ issued, onDismiss }: { issued: IssuedCredential; onDismiss: () => void }) {
  return (
    <section className="notice success" role="status" aria-labelledby="credential-heading">
      <h2 id="credential-heading">Credential for {issued.deviceName}</h2>
      <p>
        <strong>Copy this credential now.</strong> It cannot be shown again. If you lose it, issue a new one, which
        stops the old one from working.
      </p>
      <p>
        <code>{issued.credential}</code>
      </p>
      <button type="button" onClick={onDismiss}>
        I have saved it
      </button>
    </section>
  );
}

function RegisterForm({ onCredential }: { onCredential: (issued: IssuedCredential) => void }) {
  const queryClient = useQueryClient();
  const kindId = useId();
  const [name, setName] = useState('');
  const [kind, setKind] = useState<string>(kinds[0]);
  const register = useMutation({
    mutationFn: () => unwrap(api.POST('/api/v1/devices', { body: { name, kind } })),
    onSuccess: (created) => {
      onCredential({ deviceName: created.device.name, credential: created.credential });
      setName('');
      return queryClient.invalidateQueries({ queryKey: devicesKey });
    },
  });
  const { problem, status } = problemOf(register.error);

  function submit(event: FormEvent) {
    event.preventDefault();
    register.mutate();
  }

  return (
    <form onSubmit={submit} noValidate aria-labelledby="register-heading">
      <h2 id="register-heading">Register a device</h2>
      {register.isError && fieldErrors(problem, 'name').length === 0 && <Notice kind="error">{problemMessage(problem, status)}</Notice>}
      <Field label="Name" required maxLength={100} value={name} onChange={(e) => setName(e.target.value)} errors={fieldErrors(problem, 'name')} />
      <div className="field">
        <label htmlFor={kindId}>Type</label>
        <select id={kindId} value={kind} onChange={(e) => setKind(e.target.value)}>
          {kinds.map((option) => (
            <option key={option} value={option}>
              {option}
            </option>
          ))}
        </select>
      </div>
      <SubmitButton pending={register.isPending}>Register device</SubmitButton>
    </form>
  );
}

function DeviceCard({ device, onCredential }: { device: Device; onCredential: (issued: IssuedCredential) => void }) {
  const queryClient = useQueryClient();
  const [editing, setEditing] = useState(false);
  const [name, setName] = useState(device.name);
  const refresh = () => queryClient.invalidateQueries({ queryKey: devicesKey });
  const path = { deviceId: device.id };
  // Actions follow the permission the server reported for this device (FE-DEV-002).
  const canManage = device.permission === 'manage';

  const rename = useMutation({
    // If-Match carries the version this edit started from, so a concurrent change is not overwritten.
    mutationFn: () =>
      unwrap(api.PATCH('/api/v1/devices/{deviceId}', { params: { path }, body: { name }, headers: { 'If-Match': `"${device.version}"` } })),
    onSuccess: () => setEditing(false),
    onSettled: refresh,
  });
  const remove = useMutation({
    mutationFn: () => unwrap(api.DELETE('/api/v1/devices/{deviceId}', { params: { path } })),
    onSettled: refresh,
  });
  const rotate = useMutation({
    mutationFn: () => unwrap(api.POST('/api/v1/devices/{deviceId}/credentials', { params: { path } })),
    onSuccess: (result) => onCredential({ deviceName: device.name, credential: result.credential }),
  });

  const failed = [rename, remove, rotate].find((mutation) => mutation.isError);
  const { problem, status } = problemOf(failed?.error);
  const busy = rename.isPending || remove.isPending || rotate.isPending;

  function submitRename(event: FormEvent) {
    event.preventDefault();
    rename.mutate();
  }

  return (
    <li className="card">
      {failed && fieldErrors(problem, 'name').length === 0 && <Notice kind="error">{problemMessage(problem, status)}</Notice>}
      {editing ? (
        <form onSubmit={submitRename} noValidate>
          <Field label={`New name for ${device.name}`} required maxLength={100} value={name} onChange={(e) => setName(e.target.value)} errors={fieldErrors(problem, 'name')} autoFocus />
          <SubmitButton pending={rename.isPending}>Save</SubmitButton>{' '}
          <button type="button" className="secondary" onClick={() => setEditing(false)}>
            Cancel
          </button>
        </form>
      ) : (
        <>
          <h2>{device.name}</h2>
          <p className="hint">
            {device.kind} · {device.isOwner ? 'Owner' : `Shared with you (${device.permission})`}
          </p>
          {canManage && (
            <p className="actions">
              <button type="button" className="secondary" disabled={busy} onClick={() => { setName(device.name); setEditing(true); }}>
                Rename<span className="visually-hidden"> {device.name}</span>
              </button>
              <button
                type="button"
                className="secondary"
                disabled={busy}
                onClick={() => {
                  if (window.confirm(`Issue a new credential for ${device.name}? The current one stops working.`)) {
                    rotate.mutate();
                  }
                }}
              >
                New credential<span className="visually-hidden"> for {device.name}</span>
              </button>
              <button
                type="button"
                className="secondary"
                disabled={busy}
                onClick={() => {
                  if (window.confirm(`Remove ${device.name}? Its credential stops working.`)) {
                    remove.mutate();
                  }
                }}
              >
                Remove<span className="visually-hidden"> {device.name}</span>
              </button>
            </p>
          )}
        </>
      )}
    </li>
  );
}
