import { useQuery } from '@tanstack/react-query';
import { api, unwrap } from '../../api/client';
import { useSessionStatus } from '../auth/session';

export const profileKey = ['profile'] as const;

/** Roles and permissions always come from the server, never from browser storage (FE-IAM-007). */
export function useProfile() {
  const status = useSessionStatus();
  return useQuery({
    queryKey: profileKey,
    queryFn: () => unwrap(api.GET('/api/v1/users/me')),
    enabled: status === 'authenticated',
  });
}
