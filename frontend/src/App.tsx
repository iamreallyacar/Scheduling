import { Navigate, Route, Routes } from 'react-router';
import { RequireSession } from './auth/RequireSession';
import { SIGN_IN_PATH } from './auth/signInLocation';
import { SignInPage } from './auth/SignInPage';
import { MachineRegisterPage } from './machines/MachineRegisterPage';
import { ProductRegisterPage } from './products/ProductRegisterPage';
import { RoutingPage } from './routing/RoutingPage';
import { AppShell } from './shell/AppShell';
import { NotFoundPage } from './shell/NotFoundPage';

// Routes per module area (ARCHITECTURE.md section 10).
export function App() {
  return (
    <Routes>
      <Route path={SIGN_IN_PATH} element={<SignInPage />} />
      <Route element={<RequireSession />}>
        <Route element={<AppShell />}>
          <Route index element={<Navigate to="/machines" replace />} />
          <Route path="/machines" element={<MachineRegisterPage />} />
          <Route path="/products" element={<ProductRegisterPage />} />
          <Route path="/products/:productId/routing" element={<RoutingPage />} />
          <Route path="*" element={<NotFoundPage />} />
        </Route>
      </Route>
    </Routes>
  );
}
