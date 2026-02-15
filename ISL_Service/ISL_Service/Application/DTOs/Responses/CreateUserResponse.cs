namespace ISL_Service.Application.DTOs.Responses;

public class CreateUserResponse
{
    public UserResponse User { get; set; } = default!;
    public string PasswordTemporal { get; set; } = default!;
}
