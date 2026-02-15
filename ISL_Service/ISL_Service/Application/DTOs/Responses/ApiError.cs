namespace ISL_Service.Application.DTOs.Responses;

public class ApiError
{
    public string Message { get; set; } = string.Empty;
    public string? Detail { get; set; }
}
