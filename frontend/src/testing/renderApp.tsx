import { QueryClientProvider } from '@tanstack/react-query';
import { render } from '@testing-library/react';
import { MemoryRouter } from 'react-router';
import { createAppQueryClient } from '../api/queryClient';
import { forgetAntiforgeryToken } from '../api/request';
import { App } from '../App';

/** Renders the whole app as the browser would at `path`, with a fresh cache and session token. */
export function renderApp(path: string) {
  forgetAntiforgeryToken();
  return render(
    <QueryClientProvider client={createAppQueryClient()}>
      <MemoryRouter initialEntries={[path]}>
        <App />
      </MemoryRouter>
    </QueryClientProvider>,
  );
}
