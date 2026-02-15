namespace fatortak.Dtos.Shared
{
    public class ServiceResult<T>
    {
        public bool Success { get; set; }
        public T Data { get; set; }
        public string ErrorMessage { get; set; }
        public IEnumerable<string> Errors { get; set; }

        public static ServiceResult<T> SuccessResult(T data) => new()
        {
            Success = true,
            Data = data
        };

        public static ServiceResult<T> Failure(string errorMessage) => new()
        {
            Success = false,
            ErrorMessage = errorMessage
        };

        public static ServiceResult<T> ValidationError(IEnumerable<string> errors) => new()
        {
            Success = false,
            Errors = errors
        };
    }
}
