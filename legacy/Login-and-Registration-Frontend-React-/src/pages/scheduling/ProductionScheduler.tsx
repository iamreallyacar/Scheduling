import React, { useState, useEffect } from 'react';
import Navigation from '../../components/Navigation';
import { apiService } from '../../services/api';
import {
	DndContext,
	closestCenter,
	KeyboardSensor,
	PointerSensor,
	useSensor,
	useSensors,
} from '@dnd-kit/core';
import type { DragEndEvent } from '@dnd-kit/core';
import {
	arrayMove,
	SortableContext,
	sortableKeyboardCoordinates,
	horizontalListSortingStrategy,
	useSortable,
} from '@dnd-kit/sortable';
import { CSS } from '@dnd-kit/utilities';

interface ProductionOrder {
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
}

interface ProductionJob {
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
	// Additional fields for display
	productName?: string;
	orderId?: string;
	priority?: 'high' | 'medium' | 'low';
}

interface Machine {
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
	jobs: ProductionJob[];
}

interface SortableJobProps {
	job: ProductionJob;
	onJobClick: (job: ProductionJob) => void;
}

function SortableJob({ job, onJobClick }: SortableJobProps) {
	const {
		attributes,
		listeners,
		setNodeRef,
		transform,
		transition,
		isDragging,
	} = useSortable({ id: job.id });

	const style = {
		transform: CSS.Transform.toString(transform),
		transition,
		opacity: isDragging ? 0.5 : 1,
	};

	const getPriorityColor = (priority: string) => {
		switch (priority) {
			case 'high': return 'bg-red-500';
			case 'medium': return 'bg-yellow-500';
			case 'low': return 'bg-green-500';
			default: return 'bg-gray-500';
		}
	};

	const getStatusColor = (status: string) => {
		switch (status) {
			case 'scheduled': return 'bg-blue-100 text-blue-800';
			case 'in-progress': return 'bg-yellow-100 text-yellow-800';
			case 'completed': return 'bg-green-100 text-green-800';
			case 'delayed': return 'bg-red-100 text-red-800';
			default: return 'bg-gray-100 text-gray-800';
		}
	};

	const getJobWidth = (duration: number) => {
		// Each hour = 60px, minimum 120px
		return Math.max(120, duration * 60);
	};

	return (
		<div
			ref={setNodeRef}
			style={{
				...style,
				width: `${getJobWidth(job.duration)}px`,
			}}
			{...attributes}
			{...listeners}
			className="bg-white border border-gray-200 rounded-lg p-3 cursor-move hover:shadow-md transition-shadow"
			onClick={() => onJobClick(job)}
		>
			<div className="flex items-center justify-between mb-2">
				<span className="text-sm font-medium text-gray-900">{job.productName || job.jobName}</span>
				<div className={`w-3 h-3 rounded-full ${getPriorityColor(job.priority || 'medium')}`} />
			</div>
			
			<div className="space-y-1">
				<p className="text-xs text-gray-600">Order: {job.orderId || job.productionOrderId}</p>
				<p className="text-xs text-gray-600">Duration: {job.duration}h</p>
				<span className={`inline-block px-2 py-1 rounded-full text-xs font-medium ${getStatusColor(job.status)}`}>
					{job.status}
				</span>
			</div>
			
			{job.operator && (
				<p className="text-xs text-gray-500 mt-2">Operator: {job.operator}</p>
			)}
		</div>
	);
}

const ProductionScheduler: React.FC = () => {
	const sensors = useSensors(
		useSensor(PointerSensor),
		useSensor(KeyboardSensor, {
			coordinateGetter: sortableKeyboardCoordinates,
		})
	);

	const getPriorityColor = (priority: string) => {
		switch (priority) {
			case 'high': return 'bg-red-500';
			case 'medium': return 'bg-yellow-500';
			case 'low': return 'bg-green-500';
			default: return 'bg-gray-500';
		}
	};

	//const getStatusColor = (status: string) => {
	//	switch (status) {
	//		case 'completed': return 'bg-green-100 text-green-800';
	//		case 'in-progress': return 'bg-blue-100 text-blue-800';
	//		case 'delayed': return 'bg-red-100 text-red-800';
	//		case 'scheduled': return 'bg-gray-100 text-gray-800';
	//		default: return 'bg-gray-100 text-gray-800';
	//	}
	//};

	const [machines, setMachines] = useState<Machine[]>([]);
	const [unscheduledOrders, setUnscheduledOrders] = useState<ProductionOrder[]>([]);
	const [selectedJob, setSelectedJob] = useState<ProductionJob | null>(null);
	const [showJobModal, setShowJobModal] = useState(false);
	const [loading, setLoading] = useState(true);
	const [error, setError] = useState<string | null>(null);

	useEffect(() => {		const fetchData = async () => {
			try {
				setLoading(true);
				const [fetchedMachines, orders] = await Promise.all([
					apiService.getMachines(),
					apiService.getProductionOrders()
				]);

				// Filter unscheduled orders (pending status and no assigned machine)
				const unscheduled = orders.filter(order => 
					order.status === 'pending' && !order.assignedMachine
				);
				setUnscheduledOrders(unscheduled);
				
				// Convert machines to scheduler format with jobs from orders
				const schedulerMachines = fetchedMachines.map(machine => {
					const machineJobs = orders.flatMap(order => 
						order.productionJobs
							.filter(job => job.machineId === machine.id)
							.map(job => ({
								...job,
								productName: order.productName,
								orderId: order.orderNumber,
								priority: order.priority as 'high' | 'medium' | 'low'
							}))
					);

					return {
						...machine,
						jobs: machineJobs
					};
				});

				setMachines(schedulerMachines);
				setUnscheduledOrders(orders.filter(order => !order.assignedMachine));
			} catch (err) {
				setError('Failed to load production data');
				console.error('Error fetching data:', err);
			} finally {
				setLoading(false);
			}
		};
		fetchData();
	}, []);

	const handleDragEnd = (event: DragEndEvent) => {
		const { active, over } = event;

		if (!over) return;

		const activeId = active.id as number;
		const overId = over.id as string | number;

		// Find the machine and job
		const sourceMachine = machines.find(m => m.jobs.some(j => j.id === activeId));
		const targetMachine = machines.find(m => m.id === overId || m.jobs.some(j => j.id === overId));

		if (!sourceMachine || !targetMachine) return;

		setMachines(prev => {
			const newMachines = [...prev];
			const sourceIndex = newMachines.findIndex(m => m.id === sourceMachine.id);
			const targetIndex = newMachines.findIndex(m => m.id === targetMachine.id);
			
			const jobIndex = sourceMachine.jobs.findIndex(j => j.id === activeId);
			const job = sourceMachine.jobs[jobIndex];

			// Remove from source
			newMachines[sourceIndex] = {
				...sourceMachine,
				jobs: sourceMachine.jobs.filter(j => j.id !== activeId)
			};

			// Add to target
			if (sourceMachine.id === targetMachine.id) {
				// Reordering within same machine
				const targetJobIndex = targetMachine.jobs.findIndex(j => j.id === overId);
				const reorderedJobs = arrayMove(sourceMachine.jobs, jobIndex, targetJobIndex);
				newMachines[sourceIndex] = {
					...sourceMachine,
					jobs: reorderedJobs
				};
			} else {
				// Moving to different machine - update machineId
				newMachines[targetIndex] = {
					...targetMachine,
					jobs: [...targetMachine.jobs, { ...job, machineId: targetMachine.id }]
				};
			}

			return newMachines;
		});
	};

	const handleJobClick = (job: ProductionJob) => {
		setSelectedJob(job);
		setShowJobModal(true);
	};

	if (loading) {
		return (
			<div className="min-h-screen bg-gray-50">
				<Navigation title="Production Scheduler" />
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
				<Navigation title="Production Scheduler" />
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
			<Navigation title="Production Scheduler" />
			
			<div className="max-w-7xl mx-auto p-6">
				{/* Header */}
				<div className="mb-8">
					<h1 className="text-3xl font-bold text-gray-900 mb-2">Tire Production Scheduler</h1>
					<p className="text-gray-600">Drag and drop tire jobs to reschedule production across machines</p>
				</div>				{/* Scheduler */}
				<DndContext
					sensors={sensors}
					collisionDetection={closestCenter}
					onDragEnd={handleDragEnd}
				>
					{/* Unscheduled Orders Section */}
					{unscheduledOrders.length > 0 && (
						<div className="mb-8">
							<div className="bg-white rounded-lg shadow p-6">
								<div className="flex items-center justify-between mb-4">
									<h2 className="text-xl font-semibold text-gray-900">Unscheduled Orders</h2>
									<span className="bg-yellow-100 text-yellow-800 px-3 py-1 rounded-full text-sm font-medium">
										{unscheduledOrders.length} orders pending
									</span>
								</div>
								<p className="text-gray-600 mb-4">
									These orders are ready for scheduling. Drag them to a machine to assign production.
								</p>
								<div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
									{unscheduledOrders.map((order) => (
										<div key={order.id} className="bg-gray-50 border border-gray-200 rounded-lg p-4">
											<div className="flex items-start justify-between mb-2">
												<div>
													<h3 className="font-medium text-gray-900">{order.productName}</h3>
													<p className="text-sm text-gray-600">Order: {order.orderNumber}</p>
												</div>
												<div className={`w-3 h-3 rounded-full ${getPriorityColor(order.priority)}`} />
											</div>
											<div className="space-y-1 text-sm text-gray-600">
												<p>Customer: {order.customerName}</p>
												<p>Quantity: {order.quantity} tires</p>
												<p>Est. Hours: {order.estimatedHours}</p>
												<p>Due: {new Date(order.dueDate).toLocaleDateString()}</p>
												{order.daysUntilDue <= 7 && (
													<p className="text-red-600 font-medium">
														Due in {order.daysUntilDue} days
													</p>
												)}
											</div>
											<div className="mt-3 flex items-center justify-between">
												<span className={`px-2 py-1 rounded-full text-xs font-medium bg-yellow-100 text-yellow-800`}>
													{order.status}
												</span>
												<button className="text-blue-600 hover:text-blue-800 text-sm font-medium">
													Schedule →
												</button>
											</div>
										</div>
									))}
								</div>
							</div>
						</div>
					)}

					<div className="space-y-6">
						{machines.map((machine) => (
							<div key={machine.id} className="bg-white rounded-lg shadow p-6">
								<div className="flex items-center justify-between mb-4">
									<div>
										<h3 className="text-lg font-semibold text-gray-900">{machine.name}</h3>
										<p className="text-sm text-gray-500">{machine.type}</p>
									</div>
									<span className="text-sm text-gray-500">{machine.jobs.length} jobs</span>
								</div>

								<div className="min-h-[100px] border-2 border-dashed border-gray-200 rounded-lg p-4">
									{machine.jobs.length === 0 ? (
										<div className="flex items-center justify-center h-20 text-gray-400">
											No jobs scheduled
										</div>
									) : (
										<SortableContext
											items={machine.jobs.map(j => j.id)}
											strategy={horizontalListSortingStrategy}
										>
											<div className="flex gap-4 overflow-x-auto">
												{machine.jobs.map((job) => (
													<SortableJob
														key={job.id}
														job={job}
														onJobClick={handleJobClick}
													/>
												))}
											</div>
										</SortableContext>
									)}
								</div>
							</div>
						))}
					</div>
				</DndContext>

				{/* Job Details Modal */}
				{showJobModal && selectedJob && (
					<div className="fixed inset-0 bg-black bg-opacity-50 flex items-center justify-center z-50">
						<div className="bg-white rounded-lg p-6 max-w-md w-full mx-4">
							<div className="flex justify-between items-start mb-4">
								<h3 className="text-lg font-semibold text-gray-900">Job Details</h3>
								<button
									onClick={() => setShowJobModal(false)}
									className="text-gray-400 hover:text-gray-600"
								>
									×
								</button>
							</div>
							<div className="space-y-3">
								<div>
									<label className="text-sm font-medium text-gray-500">Tire Model</label>
									<p className="text-gray-900">{selectedJob.productName || selectedJob.jobName}</p>
								</div>
								<div>
									<label className="text-sm font-medium text-gray-500">Order ID</label>
									<p className="text-gray-900">{selectedJob.orderId || selectedJob.productionOrderId}</p>
								</div>
								<div>
									<label className="text-sm font-medium text-gray-500">Duration</label>
									<p className="text-gray-900">{selectedJob.duration} hours</p>
								</div>
								<div>
									<label className="text-sm font-medium text-gray-500">Priority</label>
									<p className="text-gray-900 capitalize">{selectedJob.priority || 'medium'}</p>
								</div>
								<div>
									<label className="text-sm font-medium text-gray-500">Status</label>
									<p className="text-gray-900 capitalize">{selectedJob.status}</p>
								</div>
								{selectedJob.operator && (
									<div>
										<label className="text-sm font-medium text-gray-500">Operator</label>
										<p className="text-gray-900">{selectedJob.operator}</p>
									</div>
								)}
							</div>
							
							<div className="mt-6 flex justify-end">
								<button
									onClick={() => setShowJobModal(false)}
									className="px-4 py-2 bg-blue-600 text-white rounded-md hover:bg-blue-700"
								>
									Close
								</button>
							</div>
						</div>
					</div>
				)}
			</div>
		</div>
	);
};

export default ProductionScheduler;
