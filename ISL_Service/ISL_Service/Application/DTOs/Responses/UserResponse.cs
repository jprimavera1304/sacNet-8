namespace ISL_Service.Application.DTOs.Responses;

public record UserResponse(Guid Id, string Email, string Role, DateTime CreatedAt);