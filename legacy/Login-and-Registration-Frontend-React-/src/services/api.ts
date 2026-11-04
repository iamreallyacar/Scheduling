import axios from 'axios';
import { config } from '../config';

// Create axios instance with default config
const api = axios.create({
  baseURL: config.apiBaseUrl,
  headers: {
    'Content-Type': 'application/json',
  },
});

// Request interceptor to add auth token
api.interceptors.request.use((config) => {
  const token = localStorage.getItem('token');
  if (token) {
    config.headers.Authorization = `Bearer ${token}`;
  }
  return config;
});

// Response interceptor to handle auth errors and parsing issues
api.interceptors.response.use(
  (response) => response,
  (error) => {
    if (error.response?.status === 401) {
      localStorage.removeItem('token');
      localStorage.removeItem('user');
      window.location.href = '/login';
    }
    
    // Handle JSON parsing errors
    if (error.message?.includes('Unexpected end of JSON input')) {
      console.error('JSON parsing error. Response:', error.response);
      error.message = 'Invalid response from server. Please try again.';
    }
    
    return Promise.reject(error);
  }
);

export interface User {
  id: string;
  username: string;
  email: string;
}

export interface LoginRequest {
  username: string;
  password: string;
}

export interface RegisterRequest {
  username: string;
  email: string;
  password: string;
}

export interface AuthResponse {
  user: User;
  token: string;
}

export interface ProductionOrder {
  id: number;
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
  createdDate: string;
  startDate?: string;
  completedDate?: string;
  createdBy?: string;
  daysUntilDue: number;
  productionJobs: ProductionJob[];
}

export interface ProductionJob {
  id: number;
  productionOrderId: number;
  jobName: string;
  machineId: number;
  machineName: string;
  duration: number;
  status: 'scheduled' | 'in-progress' | 'completed' | 'delayed';
  scheduledStartTime?: string;
  scheduledEndTime?: string;
  actualStartTime?: string;
  actualEndTime?: string;
  operator?: string;
  notes?: string;
  sortOrder: number;
}

export interface Machine {
  id: number;
  name: string;
  type: string;
  status: 'running' | 'idle' | 'maintenance' | 'error';
  utilization: number;
  lastMaintenance?: string;
  nextMaintenance?: string;
  notes?: string;
  isActive: boolean;
  currentJob?: string;
}

export interface CreateProductionOrderRequest {
  customerName: string;
  productName: string;
  quantity: number;
  dueDate: string;
  priority: 'high' | 'medium' | 'low';
  estimatedHours: number;
  notes?: string;
}

export interface UpdateProductionOrderRequest {
  customerName?: string;
  productName?: string;
  quantity?: number;
  dueDate?: string;
  priority?: 'high' | 'medium' | 'low';
  status?: 'pending' | 'in-progress' | 'completed' | 'delayed' | 'cancelled';
  progress?: number;
  estimatedHours?: number;
  assignedMachine?: string;
  notes?: string;
}

class ApiService {
  // Auth methods
  async login(credentials: LoginRequest): Promise<AuthResponse> {
    const response = await api.post('/auth/login', credentials);
    return response.data;
  }

  async register(userData: RegisterRequest): Promise<{ message: string }> {
    const response = await api.post('/auth/register', userData);
    return response.data;
  }

  async getProfile(): Promise<{ user: User }> {
    const response = await api.get('/auth/profile');
    return response.data;
  }

  async testConnection(): Promise<string> {
    const response = await api.get('/auth/test');
    return response.data;
  }

  getGoogleLoginUrl(): string {
    return `${config.apiBaseUrl}/auth/google-login`;
  }

  // Production Orders methods
  async getProductionOrders(): Promise<ProductionOrder[]> {
    const response = await api.get('/productionorders');
    return response.data;
  }

  async getProductionOrder(id: number): Promise<ProductionOrder> {
    const response = await api.get(`/productionorders/${id}`);
    return response.data;
  }
  async createProductionOrder(orderData: CreateProductionOrderRequest): Promise<ProductionOrder> {
    try {
      const response = await api.post('/productionorders', orderData);
      return response.data;
    } catch (error: any) {
      // Handle empty response or JSON parsing errors
      if (error.response?.status && error.response.status >= 200 && error.response.status < 300) {
        // Successful status but no valid JSON response
        throw new Error('Order may have been created but server response was invalid');
      }
      
      if (error.message?.includes('Unexpected end of JSON input')) {
        throw new Error('Server returned an invalid response. Please check if the order was created.');
      }
      
      // Re-throw the original error for other cases
      throw error;
    }
  }

  async updateProductionOrder(id: number, updateData: UpdateProductionOrderRequest): Promise<void> {
    await api.put(`/productionorders/${id}`, updateData);
  }

  async deleteProductionOrder(id: number): Promise<void> {
    await api.delete(`/productionorders/${id}`);
  }

  async getProductionOrderStatistics(): Promise<any> {
    const response = await api.get('/productionorders/statistics');
    return response.data;
  }

  // Machines methods
  async getMachines(): Promise<Machine[]> {
    const response = await api.get('/machines');
    return response.data;
  }

  async getMachine(id: number): Promise<Machine> {
    const response = await api.get(`/machines/${id}`);
    return response.data;
  }

  async updateMachineStatus(id: number, status: string, utilization: number, notes?: string): Promise<void> {
    await api.put(`/machines/${id}/status`, { status, utilization, notes });
  }

  async getMachineStatistics(): Promise<any> {
    const response = await api.get('/machines/statistics');
    return response.data;
  }
}

export const apiService = new ApiService();
export default api;
