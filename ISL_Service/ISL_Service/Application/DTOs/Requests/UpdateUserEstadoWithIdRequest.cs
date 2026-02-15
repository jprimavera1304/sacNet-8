using System.ComponentModel.DataAnnotations;

namespace ISL_Service.Application.DTOs.Requests;

public class UpdateUserEstadoWithIdRequest : UpdateUserEstadoRequest
{
    [Required]
    public Guid Id { get; set; }
}
