-- Tabla dbo.WTarima para el modulo de Tarimas.
-- Estatus: 1 = Activo, 2 = Cancelado.
IF NOT EXISTS (SELECT * FROM sys.tables t
               JOIN sys.schemas s ON s.schema_id = t.schema_id
               WHERE s.name = N'dbo' AND t.name = N'WTarima')
BEGIN
    CREATE TABLE dbo.WTarima (
        IdTarima           INT IDENTITY(1,1) NOT NULL,
        NombreTarima       NVARCHAR(150)     NOT NULL,
        IdTipoCasco        INT               NOT NULL,
        NumeroCascosBase   INT               NOT NULL,
        Observaciones      NVARCHAR(500)     NULL,
        IdStatus           INT               NOT NULL DEFAULT 1,
        UsuarioCreacion    NVARCHAR(150)     NULL,
        FechaCreacion      DATETIME          NULL,
        UsuarioModificacion NVARCHAR(150)    NULL,
        FechaModificacion  DATETIME          NULL,
        CONSTRAINT PK_WTarima PRIMARY KEY CLUSTERED (IdTarima),
        CONSTRAINT CK_WTarima_IdStatus CHECK (IdStatus IN (1, 2)),
        CONSTRAINT CK_WTarima_NumeroCascosBase CHECK (NumeroCascosBase > 0 AND NumeroCascosBase <= 99999)
    );

    -- Indice unico filtrado: no duplicar (NombreTarima, IdTipoCasco) cuando esta activa.
    CREATE UNIQUE NONCLUSTERED INDEX IX_WTarima_Nombre_IdTipoCasco_Activo
        ON dbo.WTarima (NombreTarima, IdTipoCasco)
        WHERE IdStatus = 1;
END
GO
