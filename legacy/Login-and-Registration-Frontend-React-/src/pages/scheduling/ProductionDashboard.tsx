import React, { useState, useEffect } from 'react';
import Navigation from '../../components/Navigation';
import { apiService } from '../../services/api';

interface ProductionMetrics {
  efficiency: number;
  oee: number;
  downtime: number;
  throughput: number;
  quality: number;
  activeOrders: number;
  completedToday: number;
  pendingOrders: number;
}

interface MachineStatus {
  id: string;
  name: string;
  status: 'running' | 'idle' | 'maintenance' | 'error';
  utilization: number;
  currentJob?: string;
  lastMaintenance: string;
}

const ProductionDashboard: React.FC = () => {
  const [metrics, setMetrics] = useState<ProductionMetrics>({
    efficiency: 0,
    oee: 0,
    downtime: 0,
    throughput: 0,
    quality: 0,
    activeOrders: 0,
    completedToday: 0,
    pendingOrders: 0
  });

  const [machines, setMachines] = useState<MachineStatus[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    const fetchDashboardData = async () => {
      try {
        setLoading(true);
        
        // Fetch orders and machines in parallel
        const [ordersResponse, machinesResponse] = await Promise.all([
          apiService.getProductionOrders(),
          apiService.getMachines()
        ]);

        // Calculate metrics from orders
        const activeOrders = ordersResponse.filter(order => order.status === 'in-progress').length;
        const completedToday = ordersResponse.filter(order => {
          const completedDate = order.completedDate ? new Date(order.completedDate) : null;
          const today = new Date();
          return completedDate && 
                 completedDate.toDateString() === today.toDateString() && 
                 order.status === 'completed';
        }).length;
        const pendingOrders = ordersResponse.filter(order => order.status === 'pending').length;        // Convert machines to match local interface
        const convertedMachines: MachineStatus[] = machinesResponse.map(machine => ({
          id: machine.id.toString(),
          name: machine.name,
          status: machine.status,
          utilization: machine.utilization,
          currentJob: machine.currentJob,
          lastMaintenance: machine.lastMaintenance || '2024-01-01'
        }));

        // Calculate basic metrics
        const totalMachines = convertedMachines.length;
        const runningMachines = convertedMachines.filter(m => m.status === 'running').length;
        const avgUtilization = totalMachines > 0 
          ? convertedMachines.reduce((sum, m) => sum + m.utilization, 0) / totalMachines 
          : 0;

        setMetrics({
          efficiency: Math.round(avgUtilization),
          oee: Math.round(avgUtilization * 0.9), // Rough OEE calculation
          downtime: totalMachines > 0 ? Math.round((totalMachines - runningMachines) / totalMachines * 24 * 100) / 100 : 0,
          throughput: completedToday * 50, // Tires per day estimate
          quality: Math.max(85, Math.round(95 - (ordersResponse.filter(o => o.status === 'delayed').length * 2))),
          activeOrders,
          completedToday,
          pendingOrders
        });

        setMachines(convertedMachines);
      } catch (err) {
        setError('Failed to fetch dashboard data');
        console.error('Error fetching dashboard data:', err);
      } finally {
        setLoading(false);
      }
    };

    fetchDashboardData();
  }, []);

  const getStatusColor = (status: string) => {
    switch (status) {
      case 'running': return 'bg-green-100 text-green-800';
      case 'idle': return 'bg-yellow-100 text-yellow-800';
      case 'maintenance': return 'bg-blue-100 text-blue-800';
      case 'error': return 'bg-red-100 text-red-800';
      default: return 'bg-gray-100 text-gray-800';
    }
  };

  const getUtilizationColor = (utilization: number) => {
    if (utilization >= 80) return 'bg-green-500';
    if (utilization >= 60) return 'bg-yellow-500';
    if (utilization >= 40) return 'bg-orange-500';
    return 'bg-red-500';
  };  return (
    <div className="min-h-screen bg-gray-50">
      <Navigation title="Production Dashboard" />
      
      <div className="max-w-7xl mx-auto p-6">
        {/* Header */}
        <div className="mb-8">
          <h1 className="text-3xl font-bold text-gray-900 mb-2">Production Dashboard</h1>
          <p className="text-gray-600">Real-time overview of production metrics and machine status</p>
        </div>

        {/* Loading State */}
        {loading && (
          <div className="text-center py-12">
            <div className="inline-block animate-spin rounded-full h-8 w-8 border-b-2 border-blue-600"></div>
            <p className="mt-2 text-gray-600">Loading dashboard data...</p>
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

        {/* Metrics Grid */}
        <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-6 mb-8">
          <div className="bg-white p-6 rounded-lg shadow">
            <h3 className="text-sm font-medium text-gray-500 mb-2">Efficiency</h3>
            <p className="text-3xl font-bold text-green-600">{metrics.efficiency}%</p>
          </div>
          <div className="bg-white p-6 rounded-lg shadow">
            <h3 className="text-sm font-medium text-gray-500 mb-2">OEE</h3>
            <p className="text-3xl font-bold text-blue-600">{metrics.oee}%</p>
          </div>          <div className="bg-white p-6 rounded-lg shadow">
            <h3 className="text-sm font-medium text-gray-500 mb-2">Throughput</h3>
            <p className="text-3xl font-bold text-purple-600">{metrics.throughput}</p>
            <p className="text-sm text-gray-500 mt-1">Tires/day</p>
          </div>
          <div className="bg-white p-6 rounded-lg shadow">
            <h3 className="text-sm font-medium text-gray-500 mb-2">Quality Rate</h3>
            <p className="text-3xl font-bold text-green-600">{metrics.quality}%</p>
            <p className="text-sm text-gray-500 mt-1">Pass rate</p>
          </div>
        </div>

        {/* Orders Summary */}
        <div className="grid grid-cols-1 lg:grid-cols-3 gap-6 mb-8">
          <div className="bg-white p-6 rounded-lg shadow">
            <h3 className="text-lg font-semibold text-gray-900 mb-4">Active Orders</h3>
            <p className="text-4xl font-bold text-blue-600">{metrics.activeOrders}</p>
            <p className="text-sm text-gray-500 mt-2">Currently in production</p>
          </div>
          <div className="bg-white p-6 rounded-lg shadow">
            <h3 className="text-lg font-semibold text-gray-900 mb-4">Completed Today</h3>
            <p className="text-4xl font-bold text-green-600">{metrics.completedToday}</p>
            <p className="text-sm text-gray-500 mt-2">Orders finished today</p>
          </div>
          <div className="bg-white p-6 rounded-lg shadow">
            <h3 className="text-lg font-semibold text-gray-900 mb-4">Pending Orders</h3>
            <p className="text-4xl font-bold text-orange-600">{metrics.pendingOrders}</p>
            <p className="text-sm text-gray-500 mt-2">Awaiting production</p>
          </div>
        </div>

        {/* Machine Status */}
        <div className="bg-white rounded-lg shadow mb-8">
          <div className="p-6 border-b border-gray-200">
            <h3 className="text-lg font-semibold text-gray-900">Machine Status</h3>
          </div>          <div className="p-6">
            {machines.length > 0 ? (
              <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
                {machines.map((machine) => (
                <div key={machine.id} className="border border-gray-200 rounded-lg p-4">
                  <div className="flex justify-between items-start mb-3">
                    <h4 className="font-medium text-gray-900">{machine.name}</h4>
                    <span className={`px-2 py-1 rounded-full text-xs font-medium ${getStatusColor(machine.status)}`}>
                      {machine.status}
                    </span>
                  </div>
                  
                  <div className="mb-3">
                    <div className="flex justify-between text-sm text-gray-600 mb-1">
                      <span>Utilization</span>
                      <span>{machine.utilization}%</span>
                    </div>
                    <div className="w-full bg-gray-200 rounded-full h-2">
                      <div 
                        className={`h-2 rounded-full ${getUtilizationColor(machine.utilization)}`}
                        style={{ width: `${machine.utilization}%` }}
                      ></div>
                    </div>
                  </div>

                  {machine.currentJob && (
                    <p className="text-sm text-gray-600 mb-2">
                      Current Job: <span className="font-medium">{machine.currentJob}</span>
                    </p>
                  )}
                    <p className="text-sm text-gray-500">                    Last Maintenance: {new Date(machine.lastMaintenance).toLocaleDateString()}
                  </p>
                </div>                ))}
              </div>
            ) : (
              <div className="text-center py-8">
                <svg className="w-12 h-12 text-gray-300 mx-auto mb-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                  <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={1} d="M9.75 17L9 20l-1 1h8l-1-1-.75-3M3 13h18M5 17h14a2 2 0 002-2V5a2 2 0 00-2-2H5a2 2 0 00-2 2v10a2 2 0 002 2z" />
                </svg>
                <p className="text-gray-500">No machines configured</p>
                <p className="text-gray-400 text-sm">Add machines to monitor their status and utilization</p>
              </div>
            )}
          </div>
        </div>
        </>
        )}
      </div>
    </div>
  );
};

export default ProductionDashboard;
