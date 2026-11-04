import React from 'react';
import { BrowserRouter as Router, Routes, Route, Navigate } from 'react-router-dom';
import { AuthProvider, useAuth } from './contexts/AuthContext';
import ProtectedRoute from './components/ProtectedRoute';
import Landing from './pages/Landing';
import Login from './pages/Login';
import Register from './pages/Register';
import Dashboard from './pages/Dashboard';
import OAuthSuccess from './pages/OAuthSuccess';
import ProductionDashboard from './pages/scheduling/ProductionDashboard';
import ProductionScheduler from './pages/scheduling/ProductionScheduler';
import ProductionOrders from './pages/scheduling/ProductionOrders';
import CreateOrder from './pages/scheduling/CreateOrder';
import ResourceManagement from './pages/scheduling/ResourceManagement';

const AppRoutes: React.FC = () => {
  const { isAuthenticated } = useAuth();

  return (
    <Routes>
      <Route path="/" element={isAuthenticated ? <Navigate to="/dashboard" /> : <Landing />} />
      <Route path="/login" element={isAuthenticated ? <Navigate to="/dashboard" /> : <Login />} />
      <Route path="/register" element={isAuthenticated ? <Navigate to="/dashboard" /> : <Register />} />
      <Route path="/oauth-success" element={<OAuthSuccess />} />
      <Route 
        path="/dashboard" 
        element={
          <ProtectedRoute>
            <Dashboard />
          </ProtectedRoute>
        } 
      />
      <Route 
        path="/scheduling/dashboard" 
        element={
          <ProtectedRoute>
            <ProductionDashboard />
          </ProtectedRoute>
        } 
      />
      <Route 
        path="/scheduling/scheduler" 
        element={
          <ProtectedRoute>
            <ProductionScheduler />
          </ProtectedRoute>
        } 
      />
      <Route 
        path="/scheduling/orders" 
        element={
          <ProtectedRoute>
            <ProductionOrders />
          </ProtectedRoute>
        } 
      />
      <Route 
        path="/scheduling/orders/create" 
        element={
          <ProtectedRoute>
            <CreateOrder />
          </ProtectedRoute>
        } 
      />
      <Route 
        path="/scheduling/create-order" 
        element={
          <ProtectedRoute>
            <CreateOrder />
          </ProtectedRoute>
        } 
      />
      <Route 
        path="/scheduling/resources" 
        element={
          <ProtectedRoute>
            <ResourceManagement />
          </ProtectedRoute>
        } 
      />
    </Routes>
  );
};

function App() {
  return (
    <Router>
      <AuthProvider>
        <AppRoutes />
      </AuthProvider>
    </Router>
  );
}

export default App;
