namespace Login_and_Registration_Backend_.NET_.Models
{
    public class ServiceResult<T>
    {
        public bool IsSuccess { get; private set; }
        public T? Data { get; private set; }
        public string ErrorMessage { get; private set; } = string.Empty;
        public IEnumerable<string> Errors { get; private set; } = new List<string>();

        private ServiceResult() { }

        public static ServiceResult<T> Success(T data)
        {
            return new ServiceResult<T>
            {
                IsSuccess = true,
                Data = data
            };
        }

        public static ServiceResult<T> Failure(string errorMessage, IEnumerable<string>? errors = null)
        {
            return new ServiceResult<T>
            {
                IsSuccess = false,
                ErrorMessage = errorMessage,
                Errors = errors ?? new List<string>()
            };
        }
    }

    public class ServiceResult
    {
        public bool IsSuccess { get; private set; }
        public string ErrorMessage { get; private set; } = string.Empty;
        public IEnumerable<string> Errors { get; private set; } = new List<string>();

        private ServiceResult() { }

        public static ServiceResult Success()
        {
            return new ServiceResult
            {
                IsSuccess = true
            };
        }

        public static ServiceResult Failure(string errorMessage, IEnumerable<string>? errors = null)
        {
            return new ServiceResult
            {
                IsSuccess = false,
                ErrorMessage = errorMessage,
                Errors = errors ?? new List<string>()
            };
        }
    }
}
