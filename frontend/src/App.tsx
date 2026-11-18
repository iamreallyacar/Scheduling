import { Navigate, Route, Routes } from 'react-router';
import { RequireSession } from './auth/RequireSession';
import { SIGN_IN_PATH } from './auth/signInLocation';
import { SignInPage } from './auth/SignInPage';
import { CalendarPage } from './calendar/CalendarPage';
import { MachinePage } from './machines/MachinePage';
import { MachineRegisterPage } from './machines/MachineRegisterPage';
import { OrderListPage } from './orders/OrderListPage';
import { OrderPage } from './orders/OrderPage';
import { ProductRegisterPage } from './products/ProductRegisterPage';
import { RoutingPage } from './routing/RoutingPage';
import { SchedulePage } from './schedule/SchedulePage';
import { AppShell } from './shell/AppShell';
import { NotFoundPage } from './shell/NotFoundPage';

// Routes per module area (ARCHITECTURE.md section "Frontend rules").
export function App() {
  return (
    <Routes>
      <Route path={SIGN_IN_PATH} element={<SignInPage />} />
      <Route element={<RequireSession />}>
        <Route element={<AppShell />}>
          <Route index element={<Navigate to="/machines" replace />} />
          <Route path="/orders" element={<OrderListPage />} />
          <Route path="/orders/:orderId" element={<OrderPage />} />
          <Route path="/schedule" element={<SchedulePage />} />
          <Route path="/machines" element={<MachineRegisterPage />} />
          <Route path="/machines/:machineId" element={<MachinePage />} />
          <Route path="/calendar" element={<CalendarPage />} />
          <Route path="/products" element={<ProductRegisterPage />} />
          <Route path="/products/:productId/routing" element={<RoutingPage />} />
          <Route path="*" element={<NotFoundPage />} />
        </Route>
      </Route>
    </Routes>
  );
}
