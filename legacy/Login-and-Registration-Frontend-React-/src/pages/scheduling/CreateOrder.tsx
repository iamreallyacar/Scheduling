import React, { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import Navigation from '../../components/Navigation';
import { priorityColors } from '../../styles/theme';
import { apiService, type CreateProductionOrderRequest } from '../../services/api';

interface CreateOrderForm {
  customerName: string;
  productName: string;
  quantity: number;
  dueDate: string;
  priority: 'high' | 'medium' | 'low';
  estimatedHours: number;
  notes: string;
}

const CreateOrder: React.FC = () => {
  const navigate = useNavigate();
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [formData, setFormData] = useState<CreateOrderForm>({
    customerName: '',
    productName: '',
    quantity: 1,
    dueDate: '',
    priority: 'medium',
    estimatedHours: 0,
    notes: ''
  });

  const handleInputChange = (e: React.ChangeEvent<HTMLInputElement | HTMLSelectElement | HTMLTextAreaElement>) => {
    const { name, value, type } = e.target;
    setFormData(prev => ({
      ...prev,
      [name]: type === 'number' ? Number(value) : value
    }));
  };  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setIsSubmitting(true);
    setError(null);

    try {
      // Create the request object matching the API interface
      const orderData: CreateProductionOrderRequest = {
        customerName: formData.customerName,
        productName: formData.productName,
        quantity: formData.quantity,
        dueDate: formData.dueDate,
        priority: formData.priority,
        estimatedHours: formData.estimatedHours,
        notes: formData.notes || undefined
      };

      console.log('Sending order data:', orderData);
      await apiService.createProductionOrder(orderData);

      // Success - navigate back to orders page
      navigate('/scheduling/orders', {
        state: { message: 'Order created successfully!' }
      });
    } catch (err) {
      console.error('Error creating order:', err);
      setError(err instanceof Error ? err.message : 'An error occurred while creating the order');
    } finally {
      setIsSubmitting(false);
    }
  };

  const handleCancel = () => {
    navigate('/scheduling/orders');
  };

  // Set minimum date to today
  const today = new Date().toISOString().split('T')[0];

  return (
    <div className="min-h-screen bg-gray-50">
      <Navigation title="Create Production Order" backPath="/scheduling/orders" />
      
      <main className="max-w-4xl mx-auto py-6 px-4 sm:px-6 lg:px-8">
        <div className="bg-white rounded-lg shadow">
          <div className="px-6 py-4 border-b border-gray-200">
            <h2 className="text-xl font-semibold text-gray-900">New Production Order</h2>
            <p className="text-sm text-gray-600 mt-1">
              Create a new production order for your manufacturing schedule
            </p>
          </div>

          <form onSubmit={handleSubmit} className="p-6 space-y-6">
            {error && (
              <div className="bg-red-50 border border-red-200 rounded-md p-4">
                <div className="flex">
                  <div className="flex-shrink-0">
                    <svg className="h-5 w-5 text-red-400" viewBox="0 0 20 20" fill="currentColor">
                      <path fillRule="evenodd" d="M10 18a8 8 0 100-16 8 8 0 000 16zM8.707 7.293a1 1 0 00-1.414 1.414L8.586 10l-1.293 1.293a1 1 0 101.414 1.414L10 11.414l1.293 1.293a1 1 0 001.414-1.414L11.414 10l1.293-1.293a1 1 0 00-1.414-1.414L10 8.586 8.707 7.293z" clipRule="evenodd" />
                    </svg>
                  </div>
                  <div className="ml-3">
                    <p className="text-sm text-red-800">{error}</p>
                  </div>
                </div>
              </div>
            )}

            <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
              {/* Customer Name */}
              <div>
                <label htmlFor="customerName" className="block text-sm font-medium text-gray-700 mb-2">
                  Customer Name *
                </label>
                <input
                  type="text"
                  id="customerName"
                  name="customerName"
                  required
                  value={formData.customerName}
                  onChange={handleInputChange}
                  className="w-full px-3 py-2 border border-gray-300 rounded-md shadow-sm focus:outline-none focus:ring-blue-500 focus:border-blue-500"
                  placeholder="Enter customer name"
                />
              </div>              {/* Tire Model */}
              <div>
                <label htmlFor="productName" className="block text-sm font-medium text-gray-700 mb-2">
                  Tire Model *
                </label>
                <input
                  type="text"
                  id="productName"
                  name="productName"
                  required
                  value={formData.productName}
                  onChange={handleInputChange}
                  className="w-full px-3 py-2 border border-gray-300 rounded-md shadow-sm focus:outline-none focus:ring-blue-500 focus:border-blue-500"
                  placeholder="e.g., Michelin Pilot Sport 4S, Goodyear Eagle F1"
                />
              </div>

              {/* Quantity */}
              <div>
                <label htmlFor="quantity" className="block text-sm font-medium text-gray-700 mb-2">
                  Quantity (Tires) *
                </label>
                <input
                  type="number"
                  id="quantity"
                  name="quantity"
                  required
                  min="1"
                  value={formData.quantity}
                  onChange={handleInputChange}                  className="w-full px-3 py-2 border border-gray-300 rounded-md shadow-sm focus:outline-none focus:ring-blue-500 focus:border-blue-500"
                  placeholder="Number of tires to produce"
                />
              </div>

              {/* Due Date */}
              <div>
                <label htmlFor="dueDate" className="block text-sm font-medium text-gray-700 mb-2">
                  Due Date *
                </label>
                <input
                  type="date"
                  id="dueDate"
                  name="dueDate"
                  required
                  min={today}
                  value={formData.dueDate}
                  onChange={handleInputChange}
                  className="w-full px-3 py-2 border border-gray-300 rounded-md shadow-sm focus:outline-none focus:ring-blue-500 focus:border-blue-500"
                />
              </div>

              {/* Priority */}
              <div>
                <label htmlFor="priority" className="block text-sm font-medium text-gray-700 mb-2">
                  Priority *
                </label>
                <select
                  id="priority"
                  name="priority"
                  required
                  value={formData.priority}
                  onChange={handleInputChange}
                  className="w-full px-3 py-2 border border-gray-300 rounded-md shadow-sm focus:outline-none focus:ring-blue-500 focus:border-blue-500"
                >
                  <option value="low">Low Priority</option>
                  <option value="medium">Medium Priority</option>
                  <option value="high">High Priority</option>
                </select>
                <div className="mt-1 flex items-center">
                  <div className={`w-3 h-3 rounded-full ${priorityColors[formData.priority].dot} mr-2`}></div>
                  <span className="text-xs text-gray-500">Priority indicator</span>
                </div>
              </div>              {/* Estimated Production Time */}
              <div>
                <label htmlFor="estimatedHours" className="block text-sm font-medium text-gray-700 mb-2">
                  Estimated Production Time (Hours)
                </label>
                <input
                  type="number"
                  id="estimatedHours"
                  name="estimatedHours"
                  min="0"
                  step="0.5"
                  value={formData.estimatedHours}
                  onChange={handleInputChange}                  className="w-full px-3 py-2 border border-gray-300 rounded-md shadow-sm focus:outline-none focus:ring-blue-500 focus:border-blue-500"
                  placeholder="Total hours needed for tire production"
                />
              </div>
            </div>

            {/* Notes */}
            <div>
              <label htmlFor="notes" className="block text-sm font-medium text-gray-700 mb-2">
                Notes
              </label>
              <textarea
                id="notes"
                name="notes"
                rows={3}
                value={formData.notes}
                onChange={handleInputChange}
                className="w-full px-3 py-2 border border-gray-300 rounded-md shadow-sm focus:outline-none focus:ring-blue-500 focus:border-blue-500"
                placeholder="Enter any additional notes or specifications..."
              />
            </div>

            {/* Form Actions */}
            <div className="flex justify-end space-x-4 pt-6 border-t border-gray-200">
              <button
                type="button"
                onClick={handleCancel}
                className="px-4 py-2 border border-gray-300 rounded-md shadow-sm text-sm font-medium text-gray-700 bg-white hover:bg-gray-50 focus:outline-none focus:ring-2 focus:ring-blue-500 focus:ring-offset-2"
              >
                Cancel
              </button>
              <button
                type="submit"
                disabled={isSubmitting}
                className="px-4 py-2 border border-transparent rounded-md shadow-sm text-sm font-medium text-white bg-blue-600 hover:bg-blue-700 focus:outline-none focus:ring-2 focus:ring-blue-500 focus:ring-offset-2 disabled:opacity-50 disabled:cursor-not-allowed"
              >
                {isSubmitting ? 'Creating...' : 'Create Order'}
              </button>
            </div>
          </form>
        </div>
      </main>
    </div>
  );
};

export default CreateOrder;
