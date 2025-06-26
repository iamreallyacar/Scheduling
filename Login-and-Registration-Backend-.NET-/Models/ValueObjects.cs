namespace Login_and_Registration_Backend_.NET_.Models
{
    /// <summary>
    /// Value object representing a duration in hours with proper validation
    /// </summary>
    public record Duration
    {
        public decimal Hours { get; init; }

        public Duration(decimal hours)
        {
            if (hours < 0)
                throw new ArgumentException("Duration cannot be negative", nameof(hours));
            
            Hours = hours;
        }

        public static implicit operator Duration(decimal hours) => new(hours);
        public static implicit operator decimal(Duration duration) => duration.Hours;

        public override string ToString() => $"{Hours:F2} hours";
    }
}
