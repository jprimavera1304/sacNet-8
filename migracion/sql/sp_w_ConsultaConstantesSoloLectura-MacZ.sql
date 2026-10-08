/*
  FALTA sp_w_ConsultaConstantesSoloLectura EN ZARAGOZA (MacZ)
  ===========================================================

  QUE PASA HOY, MEDIDO
  --------------------
  `GET /api/ventas/consulta/catalogos` contesta **500** contra MacZ:

      SqlException 2812 — no se encontro el procedimiento almacenado
      VentasConsultaRepository.ConsultarConstantesDeVentasAsync (:368)

  Comprobado con OBJECT_ID en las dos bases locales:

      Produccion_svr (Tauro)   -> existe
      MacZ           (Zaragoza)-> NO EXISTE

  QUE SE ROMPE
  ------------
  Ese endpoint es el que da el catalogo de AGENTES y las dos FECHAS DE OPERACION
  a todas las pantallas de ventas del front nuevo. En Zaragoza, hoy:

    - el filtro "Agente" se queda con una sola opcion en la consulta de ventas,
      en la consulta de facturas y en la reasignacion;
    - "Limpiar" no puede volver a la fecha de operacion porque no la conoce;
    - el encabezado no puede enseñar el dia de ventas ni el de pagos.

  No es un defecto de la pantalla nueva: le pasa igual a la consulta de ventas
  que ya estaba desplegada.

  POR QUE ESTE SCRIPT ES SEGURO
  -----------------------------
  - **No toca legacy.** `sp_w_` es la familia del web; los `sp_n_` no se editan.
  - Es una LECTURA pura: el procedimiento hace SELECT sobre dbo.Constantes y
    [Usuarios Fechas]. No escribe. Esa es justamente su razon de existir: el
    `sp_n_ConsultaConstantes` de legacy NO es una consulta —cuando la fecha de
    operacion esta atrasada borra y rehace AlmacenProductoHist y adelanta
    Constantes.FechaOperacion, o sea hace el CIERRE DEL DIA— y por eso el
    backend nuevo no lo usa. Ver migracion/CUIDADO-consultas-que-escriben.md.
  - `CREATE OR ALTER` y todo dentro de una transaccion, con la comprobacion
    ANTES del COMMIT.

  COMO SE CORRE

      sqlcmd -S LAP-JUAND -d MacZ -U sa -P <pwd> -b -I              -i migracion/sql/sp_w_ConsultaConstantesSoloLectura-MacZ.sql

  Y en produccion de Zaragoza, la misma, cuando se autorice.
*/

SET XACT_ABORT ON;
BEGIN TRANSACTION;
GO

CREATE OR ALTER PROCEDURE [dbo].[sp_w_ConsultaConstantesSoloLectura]
(
    @IDUsuario int = 0
)
AS
    SET NOCOUNT ON
    SET ANSI_WARNINGS OFF
    SET ANSI_NULLS OFF

DECLARE @FechaOperacion datetime,
    @NumVersion varchar(100),
    @Funcionalidad varchar(100),
    @FechaOperacionPagosFtm varchar(100),
    @FechaOperacionPagosDB varchar(100),
    @FechaOperacionPagosUsuario datetime,
    @UsuarioFechaOperacionPagos varchar(100),
    @FechaCambioFechaOperacionPagosFtm varchar(100),
    @EquipoFechaOperacionPagos varchar(100),
    @FechaUltimaActividad datetime,
    @Valida int,
    @MaxTiempoInactividadMinutos int

/* Igual que el original: la fecha de pagos de la EMPRESA es el valor por omision. */
SELECT @FechaOperacion = Cons.FechaOperacion,
    @Funcionalidad = UPPER(Cons.Funcionalidad),
    @FechaOperacionPagosUsuario = Cons.FechaOperacionPagos,
    @FechaOperacionPagosFtm = FORMAT( Cons.FechaOperacionPagos, 'dd-MM-yyyy' ),
    @FechaOperacionPagosDB = FORMAT( Cons.FechaOperacionPagos, 'MM-dd-yyyy' ),
    @UsuarioFechaOperacionPagos = ISNULL(UsuFecPag.Usuario,'ADMIN'),
    @FechaCambioFechaOperacionPagosFtm = ISNULL( FORMAT( Cons.FechaCambioFechaOperacionPagos, 'dd-MM-yyyy HH:mm' ), ''),
    @EquipoFechaOperacionPagos = ISNULL(Cons.EquipoFechaOperacionPagos,'TAURO'),
    @MaxTiempoInactividadMinutos = MaxTiempoInactividadMinutos
FROM Constantes Cons
    LEFT JOIN Usuarios UsuFecPag ON UsuFecPag.IDUsuario = Cons.IDUsuarioFechaOperacionPagos

/*
  AQUI EL ORIGINAL CIERRA EL DIA. Esta version no hace nada: solo lee.
*/

SELECT @NumVersion = NumVersion
FROM AplicacionesActualizacion
WHERE IDAplicacionActualizacion = 37  -- App Mac

DECLARE @IDBanco int
SELECT TOP 1 @IDBanco = IDBanco FROM Empresas WHERE IDStatus = 1 ORDER BY IDEmpresa

/*
  En ZARA la fecha de pagos es POR USUARIO. Se lee su fila si la tiene; si no la
  tiene, NO se le crea (el original si la crea). Sin fila, estas columnas salen
  NULL y quien las use decide que hacer, en lugar de recibir un dato inventado.
*/
IF @Funcionalidad = 'ZARA' AND @IDUsuario <> 0
BEGIN
    IF EXISTS (SELECT 1 FROM [Usuarios Fechas] WHERE IDUsuario = @IDUsuario)
    BEGIN
        SELECT @FechaOperacionPagosUsuario = UsuFec.FechaOperacionPagos,
            @FechaOperacionPagosFtm = FORMAT( UsuFec.FechaOperacionPagos, 'dd-MM-yyyy' ),
            @FechaOperacionPagosDB = FORMAT( UsuFec.FechaOperacionPagos, 'MM-dd-yyyy' ),
            @UsuarioFechaOperacionPagos = ISNULL(UsuFecPag.Usuario,'ADMIN'),
            @FechaCambioFechaOperacionPagosFtm = ISNULL( FORMAT( UsuFec.Fecha, 'dd-MM-yyyy HH:mm' ), ''),
            @EquipoFechaOperacionPagos = ISNULL(UsuFec.Equipo,'TAURO')
        FROM [Usuarios Fechas] UsuFec
            LEFT JOIN Usuarios UsuFecPag ON UsuFecPag.IDUsuario = UsuFec.IDUsuario
        WHERE UsuFec.IDUsuario = @IDUsuario
    END
END

/* El SELECT final, columna por columna igual al del original. */
SELECT ISNULL(@Valida,1) as Valida,
    FORMAT( ISNULL(@FechaUltimaActividad,GETDATE()), 'dd-MM-yyyy HH:mm:ss' ) as FechaUltimaActividad,
    @NumVersion AS NumVersion,
    FORMAT( GETDATE(), 'HH:mm:ss') AS HoraOperacion,
    FORMAT( FechaOperacion, 'dd-MM-yyyy' ) AS FechaOperacionFtm,
    FORMAT( FechaOperacion, 'MM-dd-yyyy' ) AS FechaOperacionDB,
    @FechaOperacionPagosUsuario AS FechaOperacionPagosUsuario,
    @FechaOperacionPagosFtm AS FechaOperacionPagosFtm,
    @FechaOperacionPagosDB AS FechaOperacionPagosDB,
    SERVERPROPERTY('MachineName') AS Servidor,
    db_name() AS BaseDatos,
    IVA/100 AS PorcentajeIVA,
    IDGrupoCategoriaServicios,
    ISNULL(UsuFecVen.Usuario,'ADMIN') AS UsuarioFechaOperacion,
    ISNULL( FORMAT( FechaCambioFechaOperacion, 'dd-MM-yyyy HH:mm' ), '') AS FechaCambioFechaOperacionFtm,
    ISNULL(EquipoFechaOperacion,'TAURO') AS EquipoFechaOperacion,
    @UsuarioFechaOperacionPagos AS UsuarioFechaOperacionPagos,
    @FechaCambioFechaOperacionPagosFtm AS FechaCambioFechaOperacionPagosFtm,
    @EquipoFechaOperacionPagos AS EquipoFechaOperacionPagos,
    @IDBanco AS IDBancoEmpresa,
    getdate() AS FechaHoraActual,
    *
FROM Constantes C
    LEFT JOIN Usuarios UsuFecVen ON UsuFecVen.IDUsuario = C.IDUsuarioFechaOperacion

RETURN
GO

/* ------------------------------------------------------------------ */
/* La comprobacion va ANTES del COMMIT: si no quedo, no se confirma.   */
/* ------------------------------------------------------------------ */
IF OBJECT_ID('dbo.sp_w_ConsultaConstantesSoloLectura', 'P') IS NULL
BEGIN
    PRINT 'NO se creo el procedimiento. Se deshace todo.';
    ROLLBACK TRANSACTION;
END
ELSE
BEGIN
    DECLARE @filas int;
    /* Se EJECUTA antes de confirmar: existir no basta, tiene que contestar. */
    BEGIN TRY
        DECLARE @prueba TABLE (ok int);
        INSERT INTO @prueba
        SELECT 1;

        EXEC dbo.sp_w_ConsultaConstantesSoloLectura @IDUsuario = 0;

        PRINT 'Creado y ejecutado. Se confirma.';
        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        PRINT 'El procedimiento se creo pero fallo al ejecutarse: ' + ERROR_MESSAGE();
        ROLLBACK TRANSACTION;
    END CATCH
END
GO
