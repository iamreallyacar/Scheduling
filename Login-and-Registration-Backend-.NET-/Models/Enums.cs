namespace Login_and_Registration_Backend_.NET_.Models
{
    public enum Priority
    {
        Low = 1,
        Medium = 2,
        High = 3
    }

    public enum OrderStatus
    {
        Pending,
        InProgress,
        Completed,
        Delayed,
        Cancelled
    }

    public enum JobStatus
    {
        Scheduled,
        InProgress,
        Completed,
        Delayed
    }

    public enum MachineStatus
    {
        Running,
        Idle,
        Maintenance,
        Error
    }
}
