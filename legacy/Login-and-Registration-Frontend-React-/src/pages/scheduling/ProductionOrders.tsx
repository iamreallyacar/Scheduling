import React, { useState, useEffect } from 'react';
import { Link } from 'react-router-dom';
import Navigation from '../../components/Navigation';
import { statusColors, priorityColors } from '../../styles/theme';
import { apiService } from '../../services/api';

interface ProductionOrder {
  id: string;
  orderNumber: string;
  customerName: string;
  productName: string;
  quantity: number;
  dueDate: string;
  priority: 'high' | 'medium' | 'low';
  status: 'pending' | 'in-progress' | 'completed' | 'delayed' | 'cancelled';
  progress: number;
  estimatedHours: number;
  assignedMachine?: string;
  notes?: string;
}

const ProductionOrders: React.FC = () => {
  const [orders, setOrders] = useState<ProductionOrder[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [filterStatus, setFilterStatus] = useState<string>('all');
  const [filterPriority, setFilterPriority] = useState<string>('all');

  useEffect(() => {
    const fetchOrders = async () => {
      try {
        setLoading(true);
        const fetchedOrders = await apiService.getProductionOrders();
        // Convert API data to match local interface
        const convertedOrders: ProductionOrder[] = fetchedOrders.map(order => ({
          id: order.id.toString(),
          orderNumber: order.orderNumber,
          customerName: order.customerName,
          productName: order.productName,
          quantity: order.quantity,
          dueDate: order.dueDate,
          priority: order.priority,
          status: order.status,
          progress: order.progress,
          estimatedHours: order.estimatedHours,
          assignedMachine: order.assignedMachine,
          notes: order.notes
        }));
        setOrders(convertedOrders);
      } catch (err) {
        setError('Failed to fetch production orders');
        console.error('Error fetching orders:', err);
      } finally {
        setLoading(false);
      }
    };

    fetchOrders();
  }, []);

  const getPriorityColor = (priority: string) => {
    const colors = priorityColors[priority as keyof typeof priorityColors];
    return colors ? `${colors.bg} ${colors.text}` : 'bg-gray-100 text-gray-800';
  };

  const getStatusColor = (status: string) => {
    const colors = statusColors[status as keyof typeof statusColors];
    return colors ? `${colors.bg} ${colors.text}` : 'bg-gray-100 text-gray-800';
  };

  const getProgressColor = (progress: number, status: string) => {
    if (status === 'completed') return 'bg-green-500';
    if (status === 'delayed') return 'bg-red-500';
    if (progress >= 80) return 'bg-green-500';
    if (progress >= 60) return 'bg-yellow-500';
    if (progress >= 40) return 'bg-blue-500';
    return 'bg-gray-500';
  };

  const filteredOrders = orders.filter(order => {
    const statusMatch = filterStatus === 'all' || order.status === filterStatus;
    const priorityMatch = filterPriority === 'all' || order.priority === filterPriority;
    return statusMatch && priorityMatch;
  });

  const formatDate = (dateString: string) => {
    return new Date(dateString).toLocaleDateString();
  };

  const getDaysUntilDue = (dueDate: string) => {
    const due = new Date(dueDate);
    const today = new Date();
    const diffTime = due.getTime() - today.getTime();
    const diffDays = Math.ceil(diffTime / (1000 * 60 * 60 * 24));
    return diffDays;
  };
  return (
    <div className="min-h-screen bg-gray-50">
      <Navigation title="Production Orders" />
      
      <div className="max-w-7xl mx-auto p-6">
        {/* Header with Create Button */}
        <div className="mb-8 flex justify-between items-center">          <div>
            <h1 className="text-3xl font-bold text-gray-900 mb-2">Tire Production Orders</h1>
            <p className="text-gray-600">Manage and track tire production orders</p>
          </div><div className="flex gap-3">
            <button
              onClick={() => window.location.reload()}
              className="bg-gray-600 text-white px-4 py-2 rounded-lg text-sm font-medium hover:bg-gray-700 focus:outline-none focus:ring-2 focus:ring-gray-500 focus:ring-offset-2 transition duration-200 flex items-center"
            >
              <svg className="w-4 h-4 mr-2" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M4 4v5h.582m15.356 2A8.001 8.001 0 004.582 9m0 0H9m11 11v-5h-.581m0 0a8.003 8.003 0 01-15.357-2m15.357 2H15" />
              </svg>
              Refresh
            </button>
            <Link
              to="/scheduling/orders/create"
              className="bg-blue-600 text-white px-4 py-2 rounded-lg text-sm font-medium hover:bg-blue-700 focus:outline-none focus:ring-2 focus:ring-blue-500 focus:ring-offset-2 transition duration-200 flex items-center"
            >
              <svg className="w-4 h-4 mr-2" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 4v16m8-8H4" />
              </svg>
              Create Order
            </Link>
          </div></div>

        {/* Loading State */}
        {loading && (
          <div className="text-center py-12">
            <div className="inline-block animate-spin rounded-full h-8 w-8 border-b-2 border-blue-600"></div>
            <p className="mt-2 text-gray-600">Loading orders...</p>
          </div>
        )}

        {/* Error State */}
        {error && (
          <div className="bg-red-50 border border-red-200 rounded-lg p-4 mb-6">
            <div className="flex">
              <svg className="w-5 h-5 text-red-400 mr-2" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 8v4m0 4h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z" />
              </svg>
              <div>
                <p className="text-red-800">{error}</p>
                <button 
                  onClick={() => window.location.reload()} 
                  className="text-red-600 hover:text-red-800 text-sm underline mt-1"
                >
                  Try again
                </button>
              </div>
            </div>
          </div>
        )}

        {/* Content only shows when not loading and no error */}
        {!loading && !error && (
          <>
        {/* Filters */}
        <div className="bg-white rounded-lg shadow p-6 mb-6">
          <div className="flex flex-wrap gap-4">
            <div>
              <label className="block text-sm font-medium text-gray-700 mb-2">Status</label>
              <select
                value={filterStatus}
                onChange={(e) => setFilterStatus(e.target.value)}
                className="border border-gray-300 rounded-md px-3 py-2 bg-white"              >
                <option value="all">All Statuses</option>
                <option value="pending">Pending</option>
                <option value="in-progress">In Progress</option>
                <option value="completed">Completed</option>
                <option value="delayed">Delayed</option>
                <option value="cancelled">Cancelled</option>
              </select>
            </div>
            
            <div>
              <label className="block text-sm font-medium text-gray-700 mb-2">Priority</label>
              <select
                value={filterPriority}
                onChange={(e) => setFilterPriority(e.target.value)}
                className="border border-gray-300 rounded-md px-3 py-2 bg-white"
              >
                <option value="all">All Priorities</option>
                <option value="high">High</option>
                <option value="medium">Medium</option>
                <option value="low">Low</option>
              </select>
            </div>
          </div>
        </div>

        {/* Orders Summary */}
        <div className="grid grid-cols-1 md:grid-cols-4 gap-6 mb-6">
          <div className="bg-white p-6 rounded-lg shadow">
            <h3 className="text-sm font-medium text-gray-500 mb-2">Total Orders</h3>
            <p className="text-3xl font-bold text-gray-900">{orders.length}</p>
          </div>
          <div className="bg-white p-6 rounded-lg shadow">
            <h3 className="text-sm font-medium text-gray-500 mb-2">In Progress</h3>
            <p className="text-3xl font-bold text-blue-600">
              {orders.filter(o => o.status === 'in-progress').length}
            </p>
          </div>
          <div className="bg-white p-6 rounded-lg shadow">
            <h3 className="text-sm font-medium text-gray-500 mb-2">Completed</h3>
            <p className="text-3xl font-bold text-green-600">
              {orders.filter(o => o.status === 'completed').length}
            </p>
          </div>
          <div className="bg-white p-6 rounded-lg shadow">
            <h3 className="text-sm font-medium text-gray-500 mb-2">Delayed</h3>
            <p className="text-3xl font-bold text-red-600">
              {orders.filter(o => o.status === 'delayed').length}
            </p>
          </div>
        </div>

        {/* Orders Table */}
        <div className="bg-white rounded-lg shadow overflow-hidden">
          <div className="overflow-x-auto">
            <table className="min-w-full divide-y divide-gray-200">
              <thead className="bg-gray-50">
                <tr>
                  <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">
                    Order
                  </th>
                  <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">
                    Customer
                  </th>                  <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">
                    Tire Model
                  </th>
                  <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">
                    Qty (Tires)
                  </th>
                  <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">
                    Due Date
                  </th>
                  <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">
                    Priority
                  </th>
                  <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">
                    Status
                  </th>
                  <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">
                    Progress
                  </th>
                </tr>
              </thead>
              <tbody className="bg-white divide-y divide-gray-200">
                {filteredOrders.map((order) => (
                  <tr key={order.id} className="hover:bg-gray-50">
                    <td className="px-6 py-4 whitespace-nowrap">
                      <div>
                        <div className="text-sm font-medium text-gray-900">{order.orderNumber}</div>
                        <div className="text-sm text-gray-500">{order.estimatedHours}h estimated</div>
                      </div>
                    </td>
                    <td className="px-6 py-4 whitespace-nowrap text-sm text-gray-900">
                      {order.customerName}
                    </td>
                    <td className="px-6 py-4 whitespace-nowrap">
                      <div>
                        <div className="text-sm font-medium text-gray-900">{order.productName}</div>
                        {order.assignedMachine && (
                          <div className="text-sm text-gray-500">{order.assignedMachine}</div>
                        )}
                      </div>
                    </td>
                    <td className="px-6 py-4 whitespace-nowrap text-sm text-gray-900">
                      {order.quantity}
                    </td>
                    <td className="px-6 py-4 whitespace-nowrap">
                      <div>
                        <div className="text-sm text-gray-900">{formatDate(order.dueDate)}</div>
                        <div className={`text-sm ${getDaysUntilDue(order.dueDate) < 0 ? 'text-red-600' : 
                          getDaysUntilDue(order.dueDate) <= 3 ? 'text-yellow-600' : 'text-gray-500'}`}>
                          {getDaysUntilDue(order.dueDate) < 0 
                            ? `${Math.abs(getDaysUntilDue(order.dueDate))} days overdue`
                            : `${getDaysUntilDue(order.dueDate)} days left`
                          }
                        </div>
                      </div>
                    </td>
                    <td className="px-6 py-4 whitespace-nowrap">
                      <span className={`inline-flex px-2 py-1 text-xs font-semibold rounded-full ${getPriorityColor(order.priority)}`}>
                        {order.priority}
                      </span>
                    </td>
                    <td className="px-6 py-4 whitespace-nowrap">
                      <span className={`inline-flex px-2 py-1 text-xs font-semibold rounded-full ${getStatusColor(order.status)}`}>
                        {order.status}
                      </span>
                    </td>
                    <td className="px-6 py-4 whitespace-nowrap">
                      <div className="flex items-center">
                        <div className="flex-1">
                          <div className="flex items-center justify-between text-sm text-gray-600 mb-1">
                            <span>{order.progress}%</span>
                          </div>
                          <div className="w-full bg-gray-200 rounded-full h-2">
                            <div 
                              className={`h-2 rounded-full ${getProgressColor(order.progress, order.status)}`}
                              style={{ width: `${order.progress}%` }}
                            ></div>
                          </div>
                        </div>
                      </div>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>        </div>        {filteredOrders.length === 0 && !loading && !error && (
          <div className="text-center py-12">
            {orders.length === 0 ? (
              <div>
                <svg className="w-16 h-16 text-gray-300 mx-auto mb-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                  <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={1} d="M9 12h6m-6 4h6m2 5H7a2 2 0 01-2-2V5a2 2 0 012-2h5.586a1 1 0 01.707.293l5.414 5.414a1 1 0 01.293.707V19a2 2 0 01-2 2z" />
                </svg>                <p className="text-xl text-gray-500 mb-2">No tire production orders yet</p>
                <p className="text-gray-400 mb-4">Get started by creating your first tire production order</p>
                <Link
                  to="/scheduling/orders/create"
                  className="inline-flex items-center bg-blue-600 text-white px-4 py-2 rounded-lg text-sm font-medium hover:bg-blue-700 transition duration-200"
                >
                  <svg className="w-4 h-4 mr-2" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 4v16m8-8H4" />
                  </svg>
                  Create First Tire Order
                </Link>
              </div>
            ) : (
              <p className="text-gray-500">No orders match the selected filters.</p>
            )}
          </div>
        )}
        </>
        )}
      </div>
    </div>
  );
};

export default ProductionOrders;
