using System.ComponentModel.DataAnnotations;

namespace ISL_Service.Application.DTOs.Requests;

public class UpdateUserWithIdRequest : UpdateUserRequest
{
    [Required]
    public Guid Id { get; set; }
}
