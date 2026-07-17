namespace ISL_Service.Application.DTOs.Requests;

public class LoginRequest
{
    public string Usuario { get; set; } = string.Empty;
    public string Contrasena { get; set; } = string.Empty;
    // Solo la app movil lo manda en true -> recibe refresh token. El web no lo
    // manda (default false) -> se comporta exactamente como hoy.
    public bool IncluirRefresh { get; set; }
}

public class RefreshRequest
{
    public string RefreshToken { get; set; } = string.Empty;
}
