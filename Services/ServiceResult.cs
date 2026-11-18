namespace PureAvocadoes.Services
{
    public enum ServiceOutcome
    {
        Success,
        NotFound,
        ValidationError,
        Forbidden,
        Conflict
    }

    /// Shared outcome wrapper returned by services so MVC and API controllers
    /// map validation, authorization, conflict, and missing-resource states consistently.
    public class ServiceResult
    {
        public ServiceOutcome Outcome { get; protected init; }

        public bool Succeeded => Outcome == ServiceOutcome.Success;

        public string? ErrorMessage { get; protected init; }

        public string? ErrorField { get; protected init; }

        public IReadOnlyDictionary<string, string[]> Errors { get; protected init; } =
            new Dictionary<string, string[]>();

        public static ServiceResult Success() => new()
        {
            Outcome = ServiceOutcome.Success
        };

        public static ServiceResult Fail(
            ServiceOutcome outcome,
            string errorMessage,
            string? errorField = null,
            IReadOnlyDictionary<string, string[]>? errors = null) => new()
            {
                Outcome = outcome,
                ErrorMessage = errorMessage,
                ErrorField = errorField,
                Errors = errors ?? new Dictionary<string, string[]>()
            };
    }

    /// <inheritdoc />
    public class ServiceResult<T> : ServiceResult
    {
        public T? Data { get; private init; }

        public static ServiceResult<T> Success(T data) => new()
        {
            Outcome = ServiceOutcome.Success,
            Data = data
        };

        public new static ServiceResult<T> Fail(
            ServiceOutcome outcome,
            string errorMessage,
            string? errorField = null,
            IReadOnlyDictionary<string, string[]>? errors = null) => new()
            {
                Outcome = outcome,
                ErrorMessage = errorMessage,
                ErrorField = errorField,
                Errors = errors ?? new Dictionary<string, string[]>()
            };
    }
}
