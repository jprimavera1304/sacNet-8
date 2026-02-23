-- sp_w_CancelarTarima: cambia el estatus de una tarima (1 = Activo, 2 = Cancelado).
IF OBJECT_ID('dbo.sp_w_CancelarTarima', 'P') IS NOT NULL
    DROP PROCEDURE dbo.sp_w_CancelarTarima;
GO
CREATE PROCEDURE dbo.sp_w_CancelarTarima
    @IdTarima  INT,
    @IdStatus  INT
AS
SET NOCOUNT ON;

IF @IdTarima IS NULL OR @IdTarima <= 0
BEGIN
    RAISERROR('IdTarima es requerido.', 16, 1);
    RETURN;
END
IF @IdStatus NOT IN (1, 2)
BEGIN
    RAISERROR('IdStatus debe ser 1 (Activo) o 2 (Cancelado).', 16, 1);
    RETURN;
END

IF NOT EXISTS (SELECT 1 FROM dbo.WTarima WHERE IdTarima = @IdTarima)
BEGIN
    RAISERROR('La tarima no existe.', 16, 1);
    RETURN;
END

UPDATE dbo.WTarima
SET IdStatus = @IdStatus,
    UsuarioModificacion = NULL,
    FechaModificacion   = GETUTCDATE()
WHERE IdTarima = @IdTarima;
GO
