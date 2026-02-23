-- sp_w_InsertarTarima: inserta una tarima activa.
-- Valida duplicado (NombreTarima + IdTipoCasco) case-insensitive y trim en activos.
-- Estatus: 1 = Activo, 2 = Cancelado.
IF OBJECT_ID('dbo.sp_w_InsertarTarima', 'P') IS NOT NULL
    DROP PROCEDURE dbo.sp_w_InsertarTarima;
GO
CREATE PROCEDURE dbo.sp_w_InsertarTarima
    @NombreTarima       NVARCHAR(150),
    @IdTipoCasco        INT,
    @NumeroCascosBase   INT,
    @Observaciones      NVARCHAR(500) = NULL,
    @UsuarioCreacion    NVARCHAR(150) = NULL,
    @IdTarima           INT OUTPUT
AS
SET NOCOUNT ON;

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

IF EXISTS (
    SELECT 1 FROM dbo.WTarima
    WHERE IdStatus = 1
      AND LTRIM(RTRIM(UPPER(NombreTarima))) = LTRIM(RTRIM(UPPER(@NombreTarima)))
      AND IdTipoCasco = @IdTipoCasco
)
BEGIN
    RAISERROR('Ya existe una tarima activa con el mismo nombre y tipo de casco.', 16, 1);
    RETURN;
END

DECLARE @Fecha DATETIME = GETUTCDATE();

INSERT INTO dbo.WTarima (NombreTarima, IdTipoCasco, NumeroCascosBase, Observaciones, IdStatus, UsuarioCreacion, FechaCreacion, UsuarioModificacion, FechaModificacion)
VALUES (@NombreTarima, @IdTipoCasco, @NumeroCascosBase, @Observaciones, 1, @UsuarioCreacion, @Fecha, NULL, NULL);

SET @IdTarima = SCOPE_IDENTITY();
GO
