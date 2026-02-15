namespace ISL_Service.Domain.Entities;

public class EmpresaWeb
{
    public int Id { get; set; }
    public string Clave { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public int Estado { get; set; }
}
