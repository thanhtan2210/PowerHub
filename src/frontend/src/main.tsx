import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import { BrowserRouter } from 'react-router';
import { ApiError } from './api/client';
import { App } from './app/App';
import { session } from './features/auth/session';
import './styles.css';

const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      staleTime: 30_000,
      // Client errors will not succeed on retry; only transient failures are retried.
      retry: (failures, error) => !(error instanceof ApiError && error.status < 500) && failures < 2,
    },
  },
});

// Cached server data belongs to the signed-in user; drop it when the session ends.
session.subscribe(() => {
  if (session.getStatus() === 'anonymous') {
    queryClient.clear();
  }
});

// A page load has no access token in memory; the HttpOnly refresh cookie restores it.
void session.refresh();

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <QueryClientProvider client={queryClient}>
      <BrowserRouter>
        <App />
      </BrowserRouter>
    </QueryClientProvider>
  </StrictMode>,
);
