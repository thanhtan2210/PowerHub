import { useId, type InputHTMLAttributes, type ReactNode } from 'react';

interface FieldProps extends InputHTMLAttributes<HTMLInputElement> {
  label: string;
  hint?: string;
  errors?: string[];
}

export function Field({ label, hint, errors = [], ...input }: FieldProps) {
  const id = useId();
  const describedBy = [hint && `${id}-hint`, errors.length > 0 && `${id}-error`].filter(Boolean).join(' ');

  return (
    <div className="field">
      <label htmlFor={id}>{label}</label>
      {hint && (
        <p id={`${id}-hint`} className="hint">
          {hint}
        </p>
      )}
      <input id={id} aria-describedby={describedBy || undefined} aria-invalid={errors.length > 0 || undefined} {...input} />
      {errors.length > 0 && (
        <p id={`${id}-error`} className="error">
          {errors.join(' ')}
        </p>
      )}
    </div>
  );
}

/** Announced to assistive technology when it appears; prefixed so meaning does not rely on colour. */
export function Notice({ kind, children }: { kind: 'error' | 'success'; children: ReactNode }) {
  return (
    <p className={`notice ${kind}`} role={kind === 'error' ? 'alert' : 'status'}>
      <strong>{kind === 'error' ? 'Error: ' : 'Done: '}</strong>
      {children}
    </p>
  );
}

export function SubmitButton({ pending, children }: { pending: boolean; children: ReactNode }) {
  return (
    <button type="submit" disabled={pending}>
      {pending ? 'Please wait…' : children}
    </button>
  );
}
