import React, { useState, useEffect } from 'react';
import Navigation from '../../components/Navigation';
import { apiService } from '../../services/api';
import type { Machine } from '../../services/api';

interface Operator {
	id: string;
	name: string;
	email: string;
	skills: string[];
	shift: 'morning' | 'afternoon' | 'night';
	status: 'available' | 'busy' | 'break' | 'offline';
	currentMachine?: string;
}

const ResourceManagement: React.FC = () => {
	const [machines, setMachines] = useState<Machine[]>([]);
	const [loading, setLoading] = useState(true);
	const [error, setError] = useState<string | null>(null);

	useEffect(() => {
		const fetchMachines = async () => {
			try {
				setLoading(true);
				const fetchedMachines = await apiService.getMachines();
				setMachines(fetchedMachines);
			} catch (err) {
				setError('Failed to load machines');
				console.error('Error fetching machines:', err);
			} finally {
				setLoading(false);
			}
		};

		fetchMachines();
	}, []);

	const [operators] = useState<Operator[]>([
		{
			id: 'op-001',
			name: 'John Smith',
			email: 'john.smith@company.com',
			skills: ['Tire Molding', 'Quality Control', 'Machine Setup'],
			shift: 'morning',
			status: 'busy',
			currentMachine: 'Tire Molding Press 1'
		},
		{
			id: 'op-002',
			name: 'Sarah Johnson',
			email: 'sarah.johnson@company.com',
			skills: ['Tire Building', 'Compound Mixing', 'Maintenance'],
			shift: 'morning',
			status: 'available'
		},
		{
			id: 'op-003',
			name: 'Mike Davis',
			email: 'mike.davis@company.com',
			skills: ['Tread Extrusion', 'Tire Assembly', 'Safety'],
			shift: 'afternoon',
			status: 'break'
		},
		{
			id: 'op-004',
			name: 'Lisa Chen',
			email: 'lisa.chen@company.com',
			skills: ['Tire Molding', 'Quality Inspection', 'Process Control'],
			shift: 'night',
			status: 'offline'
		}
	]);

	const [activeTab, setActiveTab] = useState<'machines' | 'operators'>('machines');

	const getStatusColor = (status: string) => {
		switch (status) {
			case 'running':
			case 'available': return 'bg-green-100 text-green-800';
			case 'idle':
			case 'busy': return 'bg-blue-100 text-blue-800';
			case 'maintenance': return 'bg-yellow-100 text-yellow-800';
			case 'error':
			case 'offline': return 'bg-red-100 text-red-800';
			case 'break': return 'bg-orange-100 text-orange-800';
			default: return 'bg-gray-100 text-gray-800';
		}
	};

	const getUtilizationColor = (utilization: number) => {
		if (utilization >= 80) return 'bg-green-500';
		if (utilization >= 60) return 'bg-yellow-500';
		if (utilization >= 40) return 'bg-orange-500';
		return 'bg-red-500';
	};

	const getShiftColor = (shift: string) => {
		switch (shift) {
			case 'morning': return 'bg-blue-100 text-blue-800';
			case 'afternoon': return 'bg-green-100 text-green-800';
			case 'night': return 'bg-purple-100 text-purple-800';
			default: return 'bg-gray-100 text-gray-800';
		}
	};

	const formatDate = (dateString?: string) => {
		if (!dateString) return 'N/A';
		return new Date(dateString).toLocaleDateString();
	};

	const getDaysUntilMaintenance = (nextMaintenance?: string) => {
		if (!nextMaintenance) return 0;
		const maintenance = new Date(nextMaintenance);
		const today = new Date();
		const diffTime = maintenance.getTime() - today.getTime();
		const diffDays = Math.ceil(diffTime / (1000 * 60 * 60 * 24));
		return diffDays;
	};

	if (loading) {
		return (
			<div className="min-h-screen bg-gray-50">
				<Navigation title="Resource Management" />
				<div className="container mx-auto px-6 py-8">
					<div className="flex items-center justify-center h-64">
						<div className="animate-spin rounded-full h-32 w-32 border-b-2 border-blue-600"></div>
					</div>
				</div>
			</div>
		);
	}

	if (error) {
		return (
			<div className="min-h-screen bg-gray-50">
				<Navigation title="Resource Management" />
				<div className="container mx-auto px-6 py-8">
					<div className="bg-red-50 border border-red-200 rounded-md p-4">
						<div className="flex">
							<div className="ml-3">
								<h3 className="text-sm font-medium text-red-800">Error</h3>
								<div className="mt-2 text-sm text-red-700">
									<p>{error}</p>
								</div>
							</div>
						</div>
					</div>
				</div>
			</div>
		);
	}

	return (
		<div className="min-h-screen bg-gray-50">
			<Navigation title="Resource Management" />
			
			<div className="max-w-7xl mx-auto p-6">
				{/* Header */}
				<div className="mb-8">
					<h1 className="text-3xl font-bold text-gray-900 mb-2">Manufacturing Equipment</h1>
					<p className="text-gray-600">Manage tire production machines, operators, and manufacturing resources</p>
				</div>

				{/* Tab Navigation */}
				<div className="mb-6">
					<nav className="flex space-x-8">
						<button
							onClick={() => setActiveTab('machines')}
							className={`py-2 px-1 border-b-2 font-medium text-sm ${
								activeTab === 'machines'
									? 'border-blue-500 text-blue-600'
									: 'border-transparent text-gray-500 hover:text-gray-700 hover:border-gray-300'
							}`}
						>
							Machines ({machines.length})
						</button>
						<button
							onClick={() => setActiveTab('operators')}
							className={`py-2 px-1 border-b-2 font-medium text-sm ${
								activeTab === 'operators'
									? 'border-blue-500 text-blue-600'
									: 'border-transparent text-gray-500 hover:text-gray-700 hover:border-gray-300'
							}`}
						>
							Operators ({operators.length})
						</button>
					</nav>
				</div>

				{/* Machines Tab */}
				{activeTab === 'machines' && (
					<div>
						{/* Machine Summary */}
						<div className="grid grid-cols-1 md:grid-cols-4 gap-6 mb-6">
							<div className="bg-white p-6 rounded-lg shadow">
								<h3 className="text-sm font-medium text-gray-500 mb-2">Total Machines</h3>
								<p className="text-3xl font-bold text-gray-900">{machines.length}</p>
							</div>
							<div className="bg-white p-6 rounded-lg shadow">
								<h3 className="text-sm font-medium text-gray-500 mb-2">Available</h3>
								<p className="text-3xl font-bold text-green-600">
									{machines.filter(m => m.status === 'idle').length}
								</p>
							</div>
							<div className="bg-white p-6 rounded-lg shadow">
								<h3 className="text-sm font-medium text-gray-500 mb-2">In Use</h3>
								<p className="text-3xl font-bold text-blue-600">
									{machines.filter(m => m.status === 'running').length}
								</p>
							</div>
							<div className="bg-white p-6 rounded-lg shadow">
								<h3 className="text-sm font-medium text-gray-500 mb-2">Maintenance</h3>
								<p className="text-3xl font-bold text-yellow-600">
									{machines.filter(m => m.status === 'maintenance' || m.status === 'error').length}
								</p>
							</div>
						</div>

						{/* Machines Grid */}
						<div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
							{machines.map((machine) => (
								<div key={machine.id} className="bg-white rounded-lg shadow p-6">
									<div className="flex justify-between items-start mb-4">
										<div>
											<h3 className="text-lg font-semibold text-gray-900">{machine.name}</h3>
											<p className="text-sm text-gray-500">{machine.type}</p>
										</div>
										<span className={`px-3 py-1 rounded-full text-sm font-medium ${getStatusColor(machine.status)}`}>
											{machine.status}
										</span>
									</div>

									{/* Utilization */}
									<div className="mb-4">
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

									{/* Current Job */}
									{machine.currentJob && (
										<div className="mb-4">
											<p className="text-sm text-gray-600">Current Job:</p>
											<p className="font-medium text-gray-900">{machine.currentJob}</p>
										</div>
									)}

									{/* Maintenance */}
									<div className="mb-4">
										<p className="text-sm text-gray-600 mb-2">Maintenance Schedule:</p>
										<div className="text-sm space-y-1">
											<p>Last: {formatDate(machine.lastMaintenance)}</p>
											<p className={`${getDaysUntilMaintenance(machine.nextMaintenance) <= 7 ? 'text-red-600 font-medium' : 'text-gray-700'}`}>
												Next: {formatDate(machine.nextMaintenance)} 
												{machine.nextMaintenance && ` (${getDaysUntilMaintenance(machine.nextMaintenance)} days)`}
											</p>
										</div>
									</div>

									{/* Notes */}
									{machine.notes && (
										<div>
											<p className="text-sm text-gray-600 mb-2">Notes:</p>
											<p className="text-sm text-gray-900">{machine.notes}</p>
										</div>
									)}
								</div>
							))}
						</div>
					</div>
				)}

				{/* Operators Tab */}
				{activeTab === 'operators' && (
					<div>
						{/* Operator Summary */}
						<div className="grid grid-cols-1 md:grid-cols-4 gap-6 mb-6">
							<div className="bg-white p-6 rounded-lg shadow">
								<h3 className="text-sm font-medium text-gray-500 mb-2">Total Operators</h3>
								<p className="text-3xl font-bold text-gray-900">{operators.length}</p>
							</div>
							<div className="bg-white p-6 rounded-lg shadow">
								<h3 className="text-sm font-medium text-gray-500 mb-2">Available</h3>
								<p className="text-3xl font-bold text-green-600">
									{operators.filter(o => o.status === 'available').length}
								</p>
							</div>
							<div className="bg-white p-6 rounded-lg shadow">
								<h3 className="text-sm font-medium text-gray-500 mb-2">Working</h3>
								<p className="text-3xl font-bold text-blue-600">
									{operators.filter(o => o.status === 'busy').length}
								</p>
							</div>
							<div className="bg-white p-6 rounded-lg shadow">
								<h3 className="text-sm font-medium text-gray-500 mb-2">On Break</h3>
								<p className="text-3xl font-bold text-orange-600">
									{operators.filter(o => o.status === 'break').length}
								</p>
							</div>
						</div>

						{/* Operators Grid */}
						<div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
							{operators.map((operator) => (
								<div key={operator.id} className="bg-white rounded-lg shadow p-6">
									<div className="flex justify-between items-start mb-4">
										<div>
											<h3 className="text-lg font-semibold text-gray-900">{operator.name}</h3>
											<p className="text-sm text-gray-500">{operator.email}</p>
										</div>
										<div className="flex flex-col items-end space-y-1">
											<span className={`px-3 py-1 rounded-full text-sm font-medium ${getStatusColor(operator.status)}`}>
												{operator.status}
											</span>
											<span className={`px-2 py-1 rounded-full text-xs font-medium ${getShiftColor(operator.shift)}`}>
												{operator.shift} shift
											</span>
										</div>
									</div>

									{/* Current Assignment */}
									{operator.currentMachine && (
										<div className="mb-4">
											<p className="text-sm text-gray-600">Currently Operating:</p>
											<p className="font-medium text-gray-900">{operator.currentMachine}</p>
										</div>
									)}

									{/* Skills */}
									<div>
										<p className="text-sm text-gray-600 mb-2">Skills:</p>
										<div className="flex flex-wrap gap-2">
											{operator.skills.map((skill) => (
												<span
													key={skill}
													className="px-2 py-1 bg-blue-100 text-blue-800 text-xs rounded-full"
												>
													{skill}
												</span>
											))}
										</div>
									</div>
								</div>
							))}
						</div>
					</div>
				)}
			</div>
		</div>
	);
};

export default ResourceManagement;
