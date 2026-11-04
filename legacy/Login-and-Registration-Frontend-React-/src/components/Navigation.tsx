import React from 'react';
import { useNavigate, useLocation } from 'react-router-dom';
import { useAuth } from '../contexts/AuthContext';

interface NavigationProps {
  title: string;
  showBackButton?: boolean;
  backPath?: string;
}

const Navigation: React.FC<NavigationProps> = ({ 
  title, 
  showBackButton = true, 
  backPath 
}) => {
  const navigate = useNavigate();
  const location = useLocation();
  const { user, logout } = useAuth();

  const handleBack = () => {
    if (backPath) {
      navigate(backPath);
    } else {
      // Smart back navigation based on current path
      const path = location.pathname;
      if (path.startsWith('/scheduling/')) {
        navigate('/scheduling/dashboard');
      } else {
        navigate('/dashboard');
      }
    }
  };

  const handleLogout = () => {
    logout();
    navigate('/login');
  };
  const navigationItems = [
    { name: 'Dashboard', path: '/dashboard' },
    { name: 'Production Dashboard', path: '/scheduling/dashboard' },
    { name: 'Scheduler', path: '/scheduling/scheduler' },
    { name: 'Orders', path: '/scheduling/orders' },
    { name: 'Create Order', path: '/scheduling/create-order' },
    { name: 'Resources', path: '/scheduling/resources' },
  ];

  return (
    <header className="bg-white shadow-sm border-b border-gray-200">
      <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8">
        <div className="flex justify-between items-center h-16">
          <div className="flex items-center space-x-4">
            {showBackButton && (
              <button
                onClick={handleBack}
                className="p-2 rounded-md text-gray-400 hover:text-gray-600 hover:bg-gray-100 transition-colors"
                title="Go back"
              >
                <svg className="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                  <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M15 19l-7-7 7-7" />
                </svg>
              </button>
            )}
            <h1 className="text-xl font-semibold text-gray-900">{title}</h1>
          </div>

          {/* Navigation Menu */}
          <nav className="hidden md:flex space-x-1">
            {navigationItems.map((item) => {
              const isActive = location.pathname === item.path;
              return (
                <button
                  key={item.path}
                  onClick={() => navigate(item.path)}
                  className={`px-3 py-2 rounded-md text-sm font-medium transition-colors ${
                    isActive
                      ? 'bg-blue-100 text-blue-700'
                      : 'text-gray-600 hover:text-gray-900 hover:bg-gray-100'
                  }`}
                >
                  {item.name}
                </button>
              );
            })}
          </nav>

          <div className="flex items-center space-x-4">
            <span className="text-sm text-gray-600">Welcome, {user?.username}!</span>
            <button
              onClick={handleLogout}
              className="bg-red-600 text-white px-4 py-2 rounded-lg text-sm font-medium hover:bg-red-700 focus:outline-none focus:ring-2 focus:ring-red-500 focus:ring-offset-2 transition duration-200"
            >
              Logout
            </button>
          </div>
        </div>
      </div>

      {/* Mobile Navigation Menu */}
      <div className="md:hidden border-t border-gray-200">
        <div className="px-4 py-2 space-y-1">
          {navigationItems.map((item) => {
            const isActive = location.pathname === item.path;
            return (
              <button
                key={item.path}
                onClick={() => navigate(item.path)}
                className={`block w-full text-left px-3 py-2 rounded-md text-sm font-medium transition-colors ${
                  isActive
                    ? 'bg-blue-100 text-blue-700'
                    : 'text-gray-600 hover:text-gray-900 hover:bg-gray-100'
                }`}
              >
                {item.name}
              </button>
            );
          })}
        </div>
      </div>
    </header>
  );
};

export default Navigation;
