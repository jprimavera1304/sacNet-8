namespace ISL_Service.Application.Exceptions;

public class NotFoundException : AppException
{
    public NotFoundException(string message, string? detail = null) : base(message, detail) { }
    public override string Code => "NO_ENCONTRADO";
    public override int StatusCode => 404;
}