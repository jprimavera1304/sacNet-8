-- sp_w_ConsultarTarimas: lista tarimas con filtros opcionales.
-- Usa [Catalogo TiposUsados] para el nombre del tipo de casco.
CREATE OR ALTER PROCEDURE dbo.sp_w_ConsultarTarimas
(
    @IdStatus INT = NULL,         -- NULL = todos, 1 activo, 2 cancelado
    @Busqueda NVARCHAR(150) = NULL -- busca por nombre (LIKE)
)
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        t.IdTarima,
        t.NombreTarima,
        t.IdTipoCasco,
        tu.[Tipo de Usado] AS TipoCascoNombre,
        t.NumeroCascosBase,
        t.Observaciones,
        t.IdStatus,
        t.UsuarioCreacion,
        t.FechaCreacion,
        t.UsuarioModificacion,
        t.FechaModificacion,
        t.UsuarioCancelacion,
        t.FechaCancelacion
    FROM dbo.WTarima t
    LEFT JOIN [Catalogo TiposUsados] tu
        ON tu.IdTipoUsado = t.IdTipoCasco AND tu.IDStatus = 1
    WHERE
        (@IdStatus IS NULL OR t.IdStatus = @IdStatus)
        AND (@Busqueda IS NULL OR t.NombreTarima LIKE '%' + @Busqueda + '%')
    ORDER BY t.NombreTarima;
END
