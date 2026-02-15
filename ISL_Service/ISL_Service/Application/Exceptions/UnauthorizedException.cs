namespace ISL_Service.Application.Exceptions;

public class UnauthorizedException : AppException
{
    public UnauthorizedException(string message, string? detail = null) : base(message, detail) { }
    public override string Code => "NO_AUTORIZADO";
    public override int StatusCode => 401;
}