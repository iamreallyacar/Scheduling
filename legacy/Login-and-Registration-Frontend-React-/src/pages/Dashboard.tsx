import React from 'react';
import { useAuth } from '../contexts/AuthContext';
import { useNavigate, Link } from 'react-router-dom';

const Dashboard: React.FC = () => {
  const { user, logout } = useAuth();
  const navigate = useNavigate();

  const handleLogout = () => {
    logout();
    navigate('/login');
  };

  return (
    <div className="min-h-screen bg-gray-50">
      {/* Header */}
      <header className="bg-white shadow-sm border-b">
        <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8">
          <div className="flex justify-between items-center h-16">            <div className="flex items-center">
              <h1 className="text-xl font-semibold text-gray-900">Scheduling Dashboard</h1>
            </div>
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
      </header>      <main className="max-w-7xl mx-auto py-6 px-4 sm:px-6 lg:px-8">
        <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
          {/* Welcome Card */}
          <div className="bg-white rounded-lg shadow p-6">            <h2 className="text-lg font-medium text-gray-900 mb-4">Welcome to Tire Production Scheduler!</h2>
            <p className="text-gray-600 mb-4">
              Manage your tire production scheduling and manufacturing resources efficiently.
            </p>
            <div className="space-y-2">
              <div className="flex justify-between">
                <span className="text-sm font-medium text-gray-500">Username:</span>
                <span className="text-sm text-gray-900">{user?.username}</span>
              </div>
              <div className="flex justify-between">
                <span className="text-sm font-medium text-gray-500">Email:</span>
                <span className="text-sm text-gray-900">{user?.email}</span>
              </div>
              <div className="flex justify-between">
                <span className="text-sm font-medium text-gray-500">Role:</span>
                <span className="text-sm text-gray-900">Production Manager</span>
              </div>
            </div>
          </div>          {/* Stats Card 1 */}
          <div className="bg-gradient-to-r from-gray-400 to-gray-500 rounded-lg shadow p-6 text-white relative">
            <div className="absolute top-2 right-2">
              <span className="bg-yellow-500 text-yellow-900 text-xs px-2 py-1 rounded-full font-medium">
                DEMO
              </span>
            </div>
            <h3 className="text-lg font-medium mb-2">Active Orders</h3>
            <p className="text-3xl font-bold">23</p>
            <p className="text-gray-100 text-sm mt-2">Currently in production (placeholder data)</p>
          </div>

          {/* Stats Card 2 */}
          <div className="bg-gradient-to-r from-gray-400 to-gray-500 rounded-lg shadow p-6 text-white relative">
            <div className="absolute top-2 right-2">
              <span className="bg-yellow-500 text-yellow-900 text-xs px-2 py-1 rounded-full font-medium">
                DEMO
              </span>
            </div>
            <h3 className="text-lg font-medium mb-2">Efficiency</h3>
            <p className="text-3xl font-bold">87%</p>
            <p className="text-gray-100 text-sm mt-2">Overall production efficiency (placeholder data)</p>
          </div>

          {/* Scheduling Modules */}
          <div className="bg-white rounded-lg shadow p-6 col-span-full lg:col-span-3">
            <h2 className="text-lg font-medium text-gray-900 mb-6">Tire Production Modules</h2>
            <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-6">
              <Link 
                to="/scheduling/dashboard" 
                className="bg-gradient-to-r from-blue-500 to-blue-600 text-white rounded-lg p-6 hover:from-blue-600 hover:to-blue-700 transition duration-200 block"
              >                <div className="text-xl font-bold mb-2">Tire Production Dashboard</div>
                <div className="text-blue-100 text-sm">
                  View real-time tire production metrics, machine status, and overall manufacturing performance
                </div>
              </Link>
              
              <Link 
                to="/scheduling/scheduler" 
                className="bg-gradient-to-r from-green-500 to-green-600 text-white rounded-lg p-6 hover:from-green-600 hover:to-green-700 transition duration-200 block"
              >                <div className="text-xl font-bold mb-2">Tire Production Scheduler</div>
                <div className="text-green-100 text-sm">
                  Schedule tire orders across molding machines and optimize manufacturing workflow
                </div>
              
              </Link>
                <Link 
                to="/scheduling/orders" 
                className="bg-gradient-to-r from-purple-500 to-purple-600 text-white rounded-lg p-6 hover:from-purple-600 hover:to-purple-700 transition duration-200 block"
              >                <div className="text-xl font-bold mb-2">Tire Production Orders</div>
                <div className="text-purple-100 text-sm">
                  Manage tire production orders, track progress, and monitor delivery schedules
                </div>
              </Link>
              
              <Link 
                to="/scheduling/resources" 
                className="bg-gradient-to-r from-red-500 to-red-600 text-white rounded-lg p-6 hover:from-orange-600 hover:to-orange-700 transition duration-200 block"
              >                <div className="text-xl font-bold mb-2">Manufacturing Equipment</div>
                <div className="text-orange-100 text-sm">
                  Manage tire molding machines, operators, and manufacturing resources efficiently
                </div>
              </Link>
            </div>
          </div>
        </div>
      </main>
    </div>
  );
};

export default Dashboard;
