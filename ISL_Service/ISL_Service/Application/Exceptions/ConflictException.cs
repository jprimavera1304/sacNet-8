namespace ISL_Service.Application.Exceptions;

public class ConflictException : AppException
{
    public ConflictException(string message, string? detail = null) : base(message, detail) { }
    public override string Code => "CONFLICTO";
    public override int StatusCode => 409;
}