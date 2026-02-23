-- sp_w_ActualizarTarima: actualiza una tarima. Solo si esta Activa (IdStatus = 1).
-- Valida duplicado (NombreTarima + IdTipoCasco) excluyendo el registro actual.
IF OBJECT_ID('dbo.sp_w_ActualizarTarima', 'P') IS NOT NULL
    DROP PROCEDURE dbo.sp_w_ActualizarTarima;
GO
CREATE PROCEDURE dbo.sp_w_ActualizarTarima
    @IdTarima            INT,
    @NombreTarima        NVARCHAR(150),
    @IdTipoCasco         INT,
    @NumeroCascosBase    INT,
    @Observaciones       NVARCHAR(500) = NULL,
    @UsuarioModificacion NVARCHAR(150) = NULL
AS
SET NOCOUNT ON;

IF @IdTarima IS NULL OR @IdTarima <= 0
BEGIN
    RAISERROR('IdTarima es requerido.', 16, 1);
    RETURN;
END
IF NULLIF(LTRIM(RTRIM(@NombreTarima)), N'') IS NULL
BEGIN
    RAISERROR('NombreTarima es requerido.', 16, 1);
    RETURN;
END
IF @IdTipoCasco IS NULL OR @IdTipoCasco <= 0
BEGIN
    RAISERROR('IdTipoCasco es requerido y debe ser mayor a 0.', 16, 1);
    RETURN;
END
IF @NumeroCascosBase IS NULL OR @NumeroCascosBase <= 0 OR @NumeroCascosBase > 99999
BEGIN
    RAISERROR('NumeroCascosBase debe estar entre 1 y 99999.', 16, 1);
    RETURN;
END

IF NOT EXISTS (SELECT 1 FROM dbo.WTarima WHERE IdTarima = @IdTarima)
BEGIN
    RAISERROR('La tarima no existe.', 16, 1);
    RETURN;
END
IF EXISTS (SELECT 1 FROM dbo.WTarima WHERE IdTarima = @IdTarima AND IdStatus <> 1)
BEGIN
    RAISERROR('Solo se puede actualizar una tarima activa.', 16, 1);
    RETURN;
END

IF EXISTS (
    SELECT 1 FROM dbo.WTarima
    WHERE IdStatus = 1
      AND IdTarima <> @IdTarima
      AND LTRIM(RTRIM(UPPER(NombreTarima))) = LTRIM(RTRIM(UPPER(@NombreTarima)))
      AND IdTipoCasco = @IdTipoCasco
)
BEGIN
    RAISERROR('Ya existe una tarima activa con el mismo nombre y tipo de casco.', 16, 1);
    RETURN;
END

UPDATE dbo.WTarima
SET NombreTarima        = @NombreTarima,
    IdTipoCasco         = @IdTipoCasco,
    NumeroCascosBase    = @NumeroCascosBase,
    Observaciones       = @Observaciones,
    UsuarioModificacion = @UsuarioModificacion,
    FechaModificacion   = GETUTCDATE()
WHERE IdTarima = @IdTarima;
GO
