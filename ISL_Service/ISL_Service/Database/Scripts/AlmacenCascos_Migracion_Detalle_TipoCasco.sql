/*
Migracion Almacen Cascos (modelo por numeroTarima + idTipoCasco)
- Permite detalle sin IdTarima (nullable)
- Garantiza unicidad por movimiento/tarima logica/tipo de casco
*/

SET NOCOUNT ON;

IF OBJECT_ID('dbo.WMovimientoCascoDetalle', 'U') IS NULL
BEGIN
    RAISERROR('No existe la tabla dbo.WMovimientoCascoDetalle.', 16, 1);
    RETURN;
END

IF EXISTS (
    SELECT 1
    FROM sys.columns
    WHERE object_id = OBJECT_ID('dbo.WMovimientoCascoDetalle')
      AND name = 'IdTarima'
      AND is_nullable = 0
)
BEGIN
    ALTER TABLE dbo.WMovimientoCascoDetalle
    ALTER COLUMN IdTarima INT NULL;
END

IF EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE object_id = OBJECT_ID('dbo.WMovimientoCascoDetalle')
      AND name = 'UX_WMovCascoDet_Mov_Tarima_Numero'
)
BEGIN
    DROP INDEX UX_WMovCascoDet_Mov_Tarima_Numero
    ON dbo.WMovimientoCascoDetalle;
END

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE object_id = OBJECT_ID('dbo.WMovimientoCascoDetalle')
      AND name = 'UX_WMovCascoDet_Mov_Numero_Tipo'
)
BEGIN
    CREATE UNIQUE INDEX UX_WMovCascoDet_Mov_Numero_Tipo
    ON dbo.WMovimientoCascoDetalle (IdMovimiento, NumeroTarima, IdTipoCasco);
END

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE object_id = OBJECT_ID('dbo.WMovimientoCascoDetalle')
      AND name = 'IX_WMovCascoDet_Movimiento_Numero'
)
BEGIN
    CREATE INDEX IX_WMovCascoDet_Movimiento_Numero
    ON dbo.WMovimientoCascoDetalle (IdMovimiento, NumeroTarima);
END
