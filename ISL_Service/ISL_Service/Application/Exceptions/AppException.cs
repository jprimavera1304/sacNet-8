namespace ISL_Service.Application.Exceptions;

public abstract class AppException : Exception
{
    protected AppException(string message, string? detail = null) : base(message)
    {
        Detail = detail;
    }

    public string? Detail { get; }
    public abstract string Code { get; }
    public abstract int StatusCode { get; }
}