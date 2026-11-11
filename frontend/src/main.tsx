import { QueryClientProvider } from '@tanstack/react-query';
import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import { BrowserRouter } from 'react-router';
import { createAppQueryClient } from './api/queryClient';
import { App } from './App';
import './styles.css';

const rootElement = document.getElementById('root');
if (rootElement === null) {
  throw new Error('index.html has no element with id "root".');
}

createRoot(rootElement).render(
  <StrictMode>
    <QueryClientProvider client={createAppQueryClient()}>
      <BrowserRouter>
        <App />
      </BrowserRouter>
    </QueryClientProvider>
  </StrictMode>,
);
