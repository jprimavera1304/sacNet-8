/*
    DESHACE el reparto del script 01.

    Quita EXACTAMENTE las concesiones que el 01 agrego y nada mas. No adivina:
    las lee de dbo.WPermisoRespaldoBaja con el lote 'CANAL_MOVIL_12_ALTA', que
    es la constancia que el 01 escribe ANTES de escribir nada.

    POR QUE NO BORRA "TODO LO QUE SE PAREZCA". Si alguien, despues de correr el
    01, le dio a mano una de estas claves a otra persona desde Accesos y
    permisos, ESA concesion no la puso este script y no es suya para quitarla.
    Borrar por clave se llevaria las dos sin distinguir, y nadie entenderia por
    que perdio un permiso que si habia dado alguien.

    QUE NO HACE
    -----------
    * NO borra las claves del catalogo (WPermiso). Las siembra el backend y
      volverian a aparecer al siguiente arranque; borrarlas aqui solo deja la
      pantalla de permisos a medias un rato.
    * NO toca los roles ni las claves del web.
    * A legacy no lo toca ni lo lee.

    ES IDEMPOTENTE: correrlo dos veces no falla ni quita nada de mas.

    COMO CORRERLO (bases LOCALES):
      sqlcmd -S LAP-JUAND -U sa -P 123 -d MacZ           -i "02_DESHACER_Canal_Movil_Doce_Modulos.sql"
      sqlcmd -S LAP-JUAND -U sa -P 123 -d Produccion_svr -i "02_DESHACER_Canal_Movil_Doce_Modulos.sql"
*/

SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @Lote nvarchar(100) = N'CANAL_MOVIL_12_ALTA';

IF OBJECT_ID('dbo.WPermisoRespaldoBaja', 'U') IS NULL
BEGIN
    PRINT 'No existe dbo.WPermisoRespaldoBaja: el script 01 nunca se corrio en esta base. No hay nada que deshacer.';
    RETURN;
END;

DECLARE @Cuantas int = (
    SELECT COUNT(*) FROM dbo.WPermisoRespaldoBaja WHERE Lote = @Lote);

IF @Cuantas = 0
BEGIN
    PRINT 'No hay constancia del lote CANAL_MOVIL_12_ALTA en esta base. No hay nada que deshacer.';
    RETURN;
END;

PRINT CONCAT('Concesiones registradas en el lote: ', @Cuantas);

BEGIN TRANSACTION;

DELETE up
FROM dbo.WUsuarioPermiso up
    INNER JOIN dbo.WPermiso p ON p.Id = up.WPermisoId
    INNER JOIN dbo.WPermisoRespaldoBaja b
        ON b.Lote = @Lote
       AND b.Origen = N'USUARIO'
       AND b.EmpresaId = up.EmpresaId
       AND b.UsuarioWebId = up.UsuarioWebId
       AND b.Clave = p.Clave
/* Solo los `allow`, que es lo unico que el 01 escribio. Un `deny` puesto
   despues a mano no es suyo. */
WHERE up.Tipo IN ('allow', 'a', 'permit');

DECLARE @Quitadas int = @@ROWCOUNT;

/* ---- COMPROBACION, ANTES del COMMIT ---- */
DECLARE @Quedan int = (
    SELECT COUNT(*)
    FROM dbo.WUsuarioPermiso up
        INNER JOIN dbo.WPermiso p ON p.Id = up.WPermisoId
        INNER JOIN dbo.WPermisoRespaldoBaja b
            ON b.Lote = @Lote AND b.Origen = N'USUARIO'
           AND b.EmpresaId = up.EmpresaId AND b.UsuarioWebId = up.UsuarioWebId
           AND b.Clave = p.Clave
    WHERE up.Tipo IN ('allow', 'a', 'permit'));

IF @Quedan > 0
BEGIN
    ROLLBACK TRANSACTION;
    THROW 56311, 'Quedaron concesiones del lote sin quitar. Se deshizo todo.', 1;
END;

/* La constancia se borra DESPUES de comprobar: si se borrara antes y el
   rollback saltara, se perderia la unica forma de volver a intentarlo. */
DELETE FROM dbo.WPermisoRespaldoBaja WHERE Lote = @Lote;

PRINT CONCAT('Concesiones quitadas: ', @Quitadas);
PRINT 'Constancia del lote borrada.';

COMMIT TRANSACTION;
GO
