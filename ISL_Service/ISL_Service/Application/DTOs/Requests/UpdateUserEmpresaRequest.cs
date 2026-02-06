using System.ComponentModel.DataAnnotations;

namespace ISL_Service.Application.DTOs.Requests;

public class UpdateUserEmpresaRequest
{
    [Required]
    public int EmpresaId { get; set; }
}
