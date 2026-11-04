/*
    CANAL MOVIL PARA LOS DOCE MODULOS QUE ENTRABAN CON LA LLAVE DEL WEB.

    Le da la clave NUEVA del telefono a quien HOY tiene su equivalente del web
    Y ENTRA A LA APP MOVIL. Asi, el dia que se despliegue el cambio, nadie se
    queda sin el mosaico que viene usando.

    NO SE CORRE EN PRODUCCION SIN AUTORIZACION. Esta escrito para las bases
    LOCALES y sirve igual en las dos SIN editarlo (medido: las doce formas de
    Mac31 que consulta existen con el mismo nombre en MacZ y en Produccion_svr).

    EL PROBLEMA QUE CIERRA
    ----------------------
    Doce pantallas del telefono pedian como permiso de ENTRADA la clave del WEB
    (`compras.ver`, `cobranzas.ver`, `transferencias.ver`, `usados.ver`,
    `cobranza.ver`, `descuentos.ajustes.ver`, `gasolinas.ver`...). Mientras sea
    asi no hay dos canales: dar compras en la computadora la enciende tambien en
    el bolsillo, y quitarla del telefono se la quita de la computadora. No son
    dos permisos parecidos, es EL MISMO.

    Ya existen las claves del telefono (las siembra el backend al arrancar) y la
    app acepta la del telefono O la del web mientras se reparten, asi que el
    despliegue por si solo no apaga nada. Lo que falta es REPARTIR las nuevas, y
    es lo unico que hace este script: sin eso, el canal del telefono existe pero
    no gobierna a nadie y la del web no se podra quitar nunca de la pareja.

    POR QUE SE EXIGE `app_movil.acceso` ("EN EL TELEFONO")
    -----------------------------------------------------
    Copiar la concesion a TODO el que tenga la clave del web repartiria acceso
    al telefono a gente de oficina que nunca entra a la app. El telefono se
    presta y la computadora de la oficina no: ese es justamente el motivo de que
    los dos canales existan. Asi que solo se le da a quien ya puede entrar a la
    app —por su rol o por un permiso propio—, que es exactamente la gente que
    HOY ve estos mosaicos.

    (Precedente: Querrys/48 copio la concesion por el mismo motivo —el telefono
    empezaba a pedir una clave nueva para pantallas que la gente YA USABA—, y
    Querrys/49 NO la copio porque alla la pantalla la ESTRENABA el telefono.
    Estos doce son el caso de Querrys/48.)

    *** LO QUE MAS IMPORTA DE ESTE SCRIPT: DONDE VIVE "LA CLAVE DEL WEB" ***
    ------------------------------------------------------------------------
    En WPermiso/WRolPermiso/WUsuarioPermiso NO ESTA. Se midio en las dos bases
    locales: CERO filas de `compras.ver`, `cobranzas.ver`, `gasolinas.ver`,
    `proveedores.ver`, `usados.ver` y `cobranza.ver`, ni por rol ni por usuario.

    Y no es que nadie las tenga: es que esas doce familias las gobierna MAC31.
    El backend las CALCULA en cada lectura a partir de los botones que la
    persona tiene en `dbo.[Usuario Forma Procesos]` para la forma de Mac31
    correspondiente (ver las `FamiliaLegacy` de
    `PermissionService.Familias.*.cs` y `ClaveDeLectura` en
    `PermissionService.cs`). Un script que buscara la clave del web en las
    tablas W* no encontraria a NADIE y diria, en verde, que no habia nada que
    repartir — que es la peor forma de fallar que tiene esto.

    Por eso aqui se pregunta por LOS DOS CAMINOS:
      a) la clave esta en WRolPermiso o como `allow` en WUsuarioPermiso, y
      b) la persona tiene al menos un boton ENCENDIDO (IDStatus = 1) en la
         forma de Mac31 de ese modulo.

    A LEGACY SOLO SE LE LEE. Se leen `dbo.n_Formas`, `dbo.n_Procesos`,
    `dbo.[Usuario Forma Procesos]` y `dbo.Usuarios` con SELECT y nada mas. No se
    escribe ni una fila, no se ejecuta ningun `sp_n_`, y no se crea ni se altera
    ningun objeto de Mac31.

    SE EMPATA POR `n_Formas.Forma` Y NO POR `Descripcion`: la descripcion trae
    acentos ("CONSULTA DE GARANTÍAS", "CONSULTA DE VENTAS REASIGNACIÓN") y basta
    que el archivo se lea con otra pagina de codigos para que no empate ninguna
    y el script reparta de menos sin decir nada. `Forma` es ASCII y es el mismo
    valor que el backend usa en `FormaExacta`.

    QUE HACE, EXACTAMENTE
    ---------------------
    1. Avisa de las claves del telefono que todavia NO estan sembradas en esta
       base, y sigue con las que si. Si no esta ninguna, se detiene y lo dice:
       el backend no ha arrancado con el codigo nuevo.
    2. Calcula los permisos EFECTIVOS de cada usuario activo: los de su rol, mas
       sus `allow`, menos sus `deny`.
    3. Calcula quien tiene cada modulo EN MAC31, por sus botones.
    4. A cada usuario que pueda entrar a la app y tenga el modulo por
       cualquiera de los dos caminos, le agrega como `allow` la clave del
       telefono.
    5. Deja constancia en dbo.WPermisoRespaldoBaja (lote
       'CANAL_MOVIL_12_ALTA') para que el script 02 pueda quitar EXACTAMENTE
       eso y nada mas.

    QUE NO HACE
    -----------
    * NO toca los ROLES. Un rol es la definicion de un puesto y esto es una
      excepcion de transicion: metida en el rol se la quedaria tambien quien
      entre al puesto el año que viene, sin que nadie lo decidiera. Las parejas
      de rol se reparten a mano desde Accesos y permisos -> pestaña Movil.
    * NO quita nada. Las claves del web se quedan como estan: la app acepta las
      dos y quitar la del web ahora dejaria sin acceso a quien no reciba la
      nueva.
    * NO pisa un `deny`. Si alguien ya tiene una fila propia para esa clave
      —de cualquier tipo— se le deja como esta.
    * NO siembra permisos. Eso lo hace el backend
      (`CatalogoDePermisosHostedService`), y hacerlo tambien aqui seria una
      segunda lista que se desincroniza.
    * NO prende ni apaga modulos de ninguna empresa.

    ES IDEMPOTENTE: correrlo dos veces no duplica nada.
    SE DESHACE CON: 02_DESHACER_Canal_Movil_Doce_Modulos.sql

    COMO CORRERLO (bases LOCALES):
      sqlcmd -S LAP-JUAND -U sa -P 123 -d MacZ           -i "01_Canal_Movil_Doce_Modulos.sql"
      sqlcmd -S LAP-JUAND -U sa -P 123 -d Produccion_svr -i "01_Canal_Movil_Doce_Modulos.sql"
*/

SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @Lote nvarchar(100) = N'CANAL_MOVIL_12_ALTA';
DECLARE @Entrada nvarchar(200) = N'app_movil.acceso';

IF OBJECT_ID('dbo.WPermiso', 'U') IS NULL
    THROW 56301, 'No existe dbo.WPermiso en esta base. Ejecute primero los scripts base de permisos web.', 1;
IF OBJECT_ID('dbo.WRolPermiso', 'U') IS NULL
    THROW 56302, 'No existe dbo.WRolPermiso en esta base.', 1;
IF OBJECT_ID('dbo.WUsuarioPermiso', 'U') IS NULL
    THROW 56303, 'No existe dbo.WUsuarioPermiso en esta base.', 1;
IF OBJECT_ID('dbo.UsuarioWeb', 'U') IS NULL
    THROW 56304, 'No existe dbo.UsuarioWeb en esta base.', 1;
IF OBJECT_ID('dbo.WRol', 'U') IS NULL
    THROW 56305, 'No existe dbo.WRol en esta base.', 1;

/* ------------------------------------------------------------------ */
/* 0) LA TABLA DEL CAMBIO                                              */
/*                                                                      */
/*    Clave del telefono  <-  clave(s) del web  +  forma(s) de Mac31.   */
/*                                                                      */
/*    Un modulo puede tener VARIAS de cada una y hay que aceptarlas     */
/*    todas: concentrados se lee con tres claves del web, y cuelga de   */
/*    dos formas de Mac31 (sus concentrados y las hojas de cobro).      */
/*    Quedarse con una dejaria fuera justo a quien esto viene a         */
/*    proteger.                                                         */
/* ------------------------------------------------------------------ */
DECLARE @Modulos TABLE (
    Movil   nvarchar(200) NOT NULL PRIMARY KEY,
    Modulo  nvarchar(100) NOT NULL
);

INSERT INTO @Modulos (Movil, Modulo) VALUES
    (N'app_movil.proveedores.ver',          N'Proveedores'),
    (N'app_movil.cambiosvigencia.ver',      N'Cambios de vigencia'),
    (N'app_movil.concentrados.ver',         N'Concentrados'),
    (N'app_movil.garantias.ver',            N'Garantias'),
    (N'app_movil.reasignacion.ver',         N'Reasignar agente'),
    (N'app_movil.transferencias.ver',       N'Bancos transferencias'),
    (N'app_movil.descuentos.ajustes.ver',   N'Liquidaciones de descuento'),
    (N'app_movil.compras.ver',              N'Compras'),
    (N'app_movil.dinero_usados.ver',        N'Cobros ingresos'),
    (N'app_movil.liquidaciones_cobros.ver', N'Liquidaciones cobros'),
    (N'app_movil.cobranzas.ver',            N'Cobranza'),
    (N'app_movil.gasolinas.ver',            N'Gasolina');

/* Las claves del WEB, para el camino (a). */
DECLARE @ClavesWeb TABLE (
    Movil nvarchar(200) NOT NULL,
    Web   nvarchar(200) NOT NULL,
    PRIMARY KEY (Movil, Web)
);

INSERT INTO @ClavesWeb (Movil, Web) VALUES
    (N'app_movil.proveedores.ver',          N'proveedores.ver'),
    (N'app_movil.cambiosvigencia.ver',      N'cambiosvigencia.ver'),
    /* Las tres que acepta VentasCobranzaConsultaController para leer. */
    (N'app_movil.concentrados.ver',         N'cobranza.ver'),
    (N'app_movil.concentrados.ver',         N'cobranza.concentrados.ver'),
    (N'app_movil.concentrados.ver',         N'cobranza.concentrados.consultar'),
    (N'app_movil.garantias.ver',            N'garantias.ver'),
    (N'app_movil.reasignacion.ver',         N'cobros.reasignar'),
    (N'app_movil.transferencias.ver',       N'transferencias.ver'),
    /* `descuentos.ver` tambien vale para LEER ajustes (VentasDescuentosController). */
    (N'app_movil.descuentos.ajustes.ver',   N'descuentos.ajustes.ver'),
    (N'app_movil.descuentos.ajustes.ver',   N'descuentos.ver'),
    (N'app_movil.compras.ver',              N'compras.ver'),
    (N'app_movil.dinero_usados.ver',        N'usados.ver'),
    (N'app_movil.liquidaciones_cobros.ver', N'cobranza.ver'),
    /* `cobranzas.consultar` es btnBuscar de Mac31, y hay gente en MacZ que solo
       tiene esa (medido). Sin ella perderia el mosaico. */
    (N'app_movil.cobranzas.ver',            N'cobranzas.ver'),
    (N'app_movil.cobranzas.ver',            N'cobranzas.consultar'),
    (N'app_movil.gasolinas.ver',            N'gasolinas.ver');

/* Las formas de MAC31, para el camino (b). Son los mismos valores que el
   backend usa en `FormaExacta`, y se comprobo que existen en las dos bases. */
DECLARE @FormasLegacy TABLE (
    Movil nvarchar(200) NOT NULL,
    Forma nvarchar(200) NOT NULL,
    PRIMARY KEY (Movil, Forma)
);

INSERT INTO @FormasLegacy (Movil, Forma) VALUES
    /* 1018, no 4045: `MCConsultarProveedores` es la forma de OBRA y da
       `obra.proveedores.*`, que es otro modulo. */
    (N'app_movil.proveedores.ver',          N'ConsultarProveedores'),
    (N'app_movil.cambiosvigencia.ver',      N'ConsultarCambiosVigencia'),
    (N'app_movil.concentrados.ver',         N'ConsultarConcentrados'),
    /* Concentrados tambien se lee con `cobranza.ver`, que sale de las hojas de
       cobro. */
    (N'app_movil.concentrados.ver',         N'ConsultarCobros'),
    (N'app_movil.garantias.ver',            N'ConsultarGarantias'),
    (N'app_movil.reasignacion.ver',         N'VentasReasignacion'),
    (N'app_movil.transferencias.ver',       N'ConsultarBancosTransferencias'),
    (N'app_movil.transferencias.ver',       N'VerBancosTransferencias'),
    (N'app_movil.descuentos.ajustes.ver',   N'ConsultarLiquidacionesDescuentos'),
    (N'app_movil.compras.ver',              N'ConsultarCompras'),
    (N'app_movil.dinero_usados.ver',        N'ConsultarDineroUsadosCobros'),
    (N'app_movil.liquidaciones_cobros.ver', N'ConsultarCobros'),
    (N'app_movil.cobranzas.ver',            N'ConsultarCobranzas'),
    (N'app_movil.gasolinas.ver',            N'ConsultarGasolinas');

/* ------------------------------------------------------------------ */
/* 1) AVISO: que claves del telefono NO estan sembradas todavia        */
/* ------------------------------------------------------------------ */
DECLARE @Empresas TABLE (EmpresaId int PRIMARY KEY);
INSERT INTO @Empresas (EmpresaId)
SELECT DISTINCT EmpresaId FROM dbo.WPermiso;

IF EXISTS (
    SELECT 1
    FROM @Empresas e
    CROSS JOIN @Modulos m
    WHERE NOT EXISTS (SELECT 1 FROM dbo.WPermiso p
                      WHERE p.EmpresaId = e.EmpresaId AND p.Clave = m.Movil))
BEGIN
    PRINT '';
    PRINT '*** AVISO: faltan claves del telefono por sembrar en esta base. ***';
    PRINT 'Las siembra el BACKEND al arrancar (CatalogoDePermisosHostedService).';
    PRINT 'Si la lista de abajo sale con renglones, levante el backend con el';
    PRINT 'codigo nuevo y vuelva a correr este script: lo que falte aqui no se';
    PRINT 'reparte, y esa gente se quedaria sin su mosaico cuando se quite la';
    PRINT 'clave del web de la pareja.';
    PRINT '';

    SELECT 'FALTA SEMBRAR' AS Aviso, e.EmpresaId, m.Movil AS ClaveDelTelefono, m.Modulo
    FROM @Empresas e
    CROSS JOIN @Modulos m
    WHERE NOT EXISTS (SELECT 1 FROM dbo.WPermiso p
                      WHERE p.EmpresaId = e.EmpresaId AND p.Clave = m.Movil)
    ORDER BY e.EmpresaId, m.Movil;
END;

IF NOT EXISTS (SELECT 1 FROM dbo.WPermiso p
               WHERE p.Clave IN (SELECT Movil FROM @Modulos))
BEGIN
    PRINT 'No hay NI UNA clave del telefono sembrada en esta base: no hay nada que repartir.';
    PRINT 'Levante el backend con el codigo nuevo (siembra el catalogo al arrancar) y repita.';
    RETURN;
END;

IF NOT EXISTS (SELECT 1 FROM dbo.WPermiso WHERE Clave = @Entrada)
BEGIN
    PRINT 'Esta base no tiene app_movil.acceso: nadie entra a la app movil y no hay nada que repartir.';
    RETURN;
END;

/* Tabla de constancia: la misma que usan los scripts de Querrys/25. */
IF OBJECT_ID('dbo.WPermisoRespaldoBaja', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.WPermisoRespaldoBaja (
        Id            bigint IDENTITY(1,1) NOT NULL
            CONSTRAINT PK_WPermisoRespaldoBaja PRIMARY KEY,
        Lote          nvarchar(100)  NOT NULL,
        EmpresaId     int            NOT NULL,
        Clave         nvarchar(200)  NOT NULL,
        Nombre        nvarchar(200)  NULL,
        Descripcion   nvarchar(500)  NULL,
        Origen        nvarchar(20)   NOT NULL,
        RolCodigo     nvarchar(100)  NULL,
        UsuarioWebId  uniqueidentifier NULL,
        Tipo          nvarchar(50)   NULL,
        Motivo        nvarchar(500)  NULL,
        FechaRespaldo datetime2(3)   NOT NULL
            CONSTRAINT DF_WPermisoRespaldoBaja_Fecha DEFAULT SYSUTCDATETIME()
    );
    PRINT 'Se creo dbo.WPermisoRespaldoBaja (respaldo para poder deshacer).';
END;

/* ------------------------------------------------------------------ */
/* 2) LOS PERMISOS EFECTIVOS DE CADA USUARIO (camino a)                */
/*                                                                      */
/*    Es la misma cuenta que hace el backend: los del ROL, mas los      */
/*    `allow` propios, MENOS los `deny` propios. Mirar solo los `allow` */
/*    dejaria fuera a casi todo el mundo —la mayoria llega por el rol—  */
/*    y no respetar los `deny` le daria el telefono a quien se le quito */
/*    el modulo a mano.                                                 */
/*                                                                      */
/*    EL ROL SE COMPARA SIN GUIONES BAJOS Y EN MAYUSCULAS. UsuarioWeb.  */
/*    Rol guarda texto libre ("SuperAdmin") y WRol.Codigo guarda el     */
/*    codigo ("SUPER_ADMIN"): comparando tal cual no casaria ninguno.   */
/* ------------------------------------------------------------------ */
IF OBJECT_ID('tempdb..#Efectivo') IS NOT NULL DROP TABLE #Efectivo;
CREATE TABLE #Efectivo (
    EmpresaId    int NOT NULL,
    UsuarioWebId uniqueidentifier NOT NULL,
    Clave        nvarchar(200) NOT NULL,
    PRIMARY KEY (EmpresaId, UsuarioWebId, Clave)
);

/* 2a) Lo que da el ROL. Solo usuarios ACTIVOS (Estado = 1): a uno dado de baja
       no se le reparte nada. */
INSERT INTO #Efectivo (EmpresaId, UsuarioWebId, Clave)
SELECT DISTINCT u.EmpresaId, u.Id, p.Clave
FROM dbo.UsuarioWeb u
    INNER JOIN dbo.WRol r
        ON r.EmpresaId = u.EmpresaId
       AND REPLACE(UPPER(LTRIM(RTRIM(r.Codigo))), '_', '')
         = REPLACE(UPPER(LTRIM(RTRIM(ISNULL(u.Rol, '')))), '_', '')
    INNER JOIN dbo.WRolPermiso rp ON rp.EmpresaId = r.EmpresaId AND rp.WRolId = r.Id
    INNER JOIN dbo.WPermiso p     ON p.Id = rp.WPermisoId
WHERE u.Estado = 1;

/* 2b) Los `allow` propios. */
INSERT INTO #Efectivo (EmpresaId, UsuarioWebId, Clave)
SELECT DISTINCT u.EmpresaId, u.Id, p.Clave
FROM dbo.UsuarioWeb u
    INNER JOIN dbo.WUsuarioPermiso up ON up.EmpresaId = u.EmpresaId AND up.UsuarioWebId = u.Id
    INNER JOIN dbo.WPermiso p         ON p.Id = up.WPermisoId
WHERE u.Estado = 1
  AND up.Tipo IN ('allow', 'a', 'permit')
  AND NOT EXISTS (SELECT 1 FROM #Efectivo e
                  WHERE e.EmpresaId = u.EmpresaId AND e.UsuarioWebId = u.Id AND e.Clave = p.Clave);

/* 2c) Fuera los `deny` propios. */
DELETE e
FROM #Efectivo e
    INNER JOIN dbo.WUsuarioPermiso up
        ON up.EmpresaId = e.EmpresaId AND up.UsuarioWebId = e.UsuarioWebId
    INNER JOIN dbo.WPermiso p ON p.Id = up.WPermisoId AND p.Clave = e.Clave
WHERE up.Tipo IN ('deny', 'd', 'denied');

/* ------------------------------------------------------------------ */
/* 3) QUIEN TIENE EL MODULO EN MAC31 (camino b)                        */
/*                                                                      */
/*    SOLO LECTURA de legacy. "Tenerlo" = al menos un boton encendido   */
/*    (IDStatus = 1) en la forma de ese modulo, que es la misma         */
/*    condicion con la que el backend concede la clave de lectura.      */
/* ------------------------------------------------------------------ */
IF OBJECT_ID('tempdb..#PorMac31') IS NOT NULL DROP TABLE #PorMac31;
CREATE TABLE #PorMac31 (
    EmpresaId    int NOT NULL,
    UsuarioWebId uniqueidentifier NOT NULL,
    Movil        nvarchar(200) NOT NULL,
    Forma        nvarchar(200) NOT NULL,
    PRIMARY KEY (EmpresaId, UsuarioWebId, Movil, Forma)
);

IF OBJECT_ID('dbo.n_Formas', 'U') IS NULL
    OR OBJECT_ID('dbo.n_Procesos', 'U') IS NULL
    OR OBJECT_ID('dbo.[Usuario Forma Procesos]', 'U') IS NULL
    OR OBJECT_ID('dbo.Usuarios', 'U') IS NULL
BEGIN
    PRINT '';
    PRINT '*** AVISO: esta base no tiene las tablas de Mac31 (n_Formas /';
    PRINT 'n_Procesos / [Usuario Forma Procesos] / Usuarios). Estos doce';
    PRINT 'modulos los gobierna Mac31, asi que SIN ellas este script solo';
    PRINT 'puede mirar WRolPermiso y WUsuarioPermiso, donde casi nunca estan.';
    PRINT 'El reparto va a salir corto y hay que completarlo a mano desde';
    PRINT 'Accesos y permisos -> pestaña Movil.';
    PRINT '';
END
ELSE
BEGIN
    /*
      EL USUARIO DE MAC31 SE EMPATA POR NOMBRE, y si existe la columna
      UsuarioWeb.LegacyUserId manda ella. Es exactamente lo que hace
      `ResolveLegacyUserIdAsync`; la columna no esta en todas las bases, y por
      eso se pregunta antes de usarla en vez de darla por hecha.
    */
    IF OBJECT_ID('tempdb..#UsuarioLegacy') IS NOT NULL DROP TABLE #UsuarioLegacy;
    CREATE TABLE #UsuarioLegacy (
        EmpresaId    int NOT NULL,
        UsuarioWebId uniqueidentifier NOT NULL,
        IDUsuario    int NOT NULL,
        PRIMARY KEY (EmpresaId, UsuarioWebId)
    );

    /* Primero el empate por nombre, que es el que siempre vale. */
    INSERT INTO #UsuarioLegacy (EmpresaId, UsuarioWebId, IDUsuario)
    SELECT u.EmpresaId, u.Id, MIN(leg.IDUsuario)
    FROM dbo.UsuarioWeb u
        INNER JOIN dbo.Usuarios leg
            ON UPPER(LTRIM(RTRIM(leg.Usuario))) = UPPER(LTRIM(RTRIM(u.Usuario)))
    WHERE u.Estado = 1
    GROUP BY u.EmpresaId, u.Id;

    /* Y si existe la columna, ella MANDA sobre el nombre, igual que en
       `ResolveLegacyUserIdAsync`. Va en SQL dinamico porque la columna no esta
       en todas las bases y sin eso el lote no compila donde falta. */
    IF COL_LENGTH('dbo.UsuarioWeb', 'LegacyUserId') IS NOT NULL
    BEGIN
        EXEC sp_executesql N'
            MERGE #UsuarioLegacy AS d
            USING (SELECT u.EmpresaId, u.Id AS UsuarioWebId,
                          TRY_CONVERT(int, u.LegacyUserId) AS IDUsuario
                   FROM dbo.UsuarioWeb u
                   WHERE u.Estado = 1 AND TRY_CONVERT(int, u.LegacyUserId) IS NOT NULL) AS s
               ON s.EmpresaId = d.EmpresaId AND s.UsuarioWebId = d.UsuarioWebId
            WHEN MATCHED THEN UPDATE SET d.IDUsuario = s.IDUsuario
            WHEN NOT MATCHED BY TARGET THEN
                INSERT (EmpresaId, UsuarioWebId, IDUsuario)
                VALUES (s.EmpresaId, s.UsuarioWebId, s.IDUsuario);';
    END;

    INSERT INTO #PorMac31 (EmpresaId, UsuarioWebId, Movil, Forma)
    SELECT DISTINCT ul.EmpresaId, ul.UsuarioWebId, fl.Movil, f.Forma
    FROM #UsuarioLegacy ul
        INNER JOIN dbo.[Usuario Forma Procesos] ufp
            ON ufp.IDUsuario = ul.IDUsuario AND ufp.IDStatus = 1
        INNER JOIN dbo.n_Procesos pr
            ON pr.IDProceso = ufp.IDProceso AND ISNULL(pr.IDStatus, 1) = 1
        INNER JOIN dbo.n_Formas f   ON f.IDForma = pr.IDForma
        INNER JOIN @FormasLegacy fl ON fl.Forma = f.Forma;

    /* En variables y no dentro del PRINT: `PRINT CONCAT(..., (SELECT ...))` no
       se compila —"Subqueries are not allowed in this context"— y el lote se
       cae antes de repartir nada. */
    DECLARE @Empatados int = (SELECT COUNT(*) FROM #UsuarioLegacy);
    DECLARE @Pares int = (
        SELECT COUNT(*) FROM (
            SELECT DISTINCT UsuarioWebId, Movil FROM #PorMac31) x);

    PRINT CONCAT('Usuarios empatados con Mac31: ', @Empatados);
    PRINT CONCAT('Pares usuario x modulo encontrados en Mac31: ', @Pares);
END;

/* ------------------------------------------------------------------ */
/* 4) A QUIEN LE TOCA: entra a la app Y tiene el modulo                */
/* ------------------------------------------------------------------ */
IF OBJECT_ID('tempdb..#AlTelefono') IS NOT NULL DROP TABLE #AlTelefono;
CREATE TABLE #AlTelefono (
    EmpresaId    int NOT NULL,
    UsuarioWebId uniqueidentifier NOT NULL,
    Movil        nvarchar(200) NOT NULL,
    Modulo       nvarchar(100) NOT NULL,
    Porque       nvarchar(200) NOT NULL,
    PRIMARY KEY (EmpresaId, UsuarioWebId, Movil)
);

/* Los candidatos de los dos caminos, en una sola lista. Un usuario puede
   llegar por los dos: `MIN(Porque)` se queda con uno, que es solo para
   escribir el motivo. */
INSERT INTO #AlTelefono (EmpresaId, UsuarioWebId, Movil, Modulo, Porque)
SELECT c.EmpresaId, c.UsuarioWebId, c.Movil, m.Modulo, MIN(c.Porque)
FROM (
    /* (a) la clave del web, si por una vez estuviera en las tablas W*. */
    SELECT e.EmpresaId, e.UsuarioWebId, w.Movil, CONCAT(N'tiene ', w.Web) AS Porque
    FROM #Efectivo e
        INNER JOIN @ClavesWeb w ON w.Web = e.Clave

    UNION ALL

    /* (b) los botones de Mac31, que es de donde sale de verdad. */
    SELECT l.EmpresaId, l.UsuarioWebId, l.Movil,
           CONCAT(N'tiene la forma ', l.Forma, N' en Mac31') AS Porque
    FROM #PorMac31 l
) c
    INNER JOIN @Modulos m ON m.Movil = c.Movil
    /* La puerta de la app: sin esto no entra al telefono. */
    INNER JOIN #Efectivo entra
        ON entra.EmpresaId = c.EmpresaId
       AND entra.UsuarioWebId = c.UsuarioWebId
       AND entra.Clave = @Entrada
    /* Y que la clave del telefono exista en SU empresa. */
    INNER JOIN dbo.WPermiso destino
        ON destino.EmpresaId = c.EmpresaId AND destino.Clave = c.Movil
WHERE NOT EXISTS (
        /* Ya la tiene de forma efectiva. */
        SELECT 1 FROM #Efectivo ya
        WHERE ya.EmpresaId = c.EmpresaId AND ya.UsuarioWebId = c.UsuarioWebId
          AND ya.Clave = c.Movil)
  AND NOT EXISTS (
        /* Ni tiene ya una fila propia (de cualquier tipo) para esa clave: si
           alguien le puso un `deny` a mano, este script NO lo pisa. */
        SELECT 1 FROM dbo.WUsuarioPermiso x
        WHERE x.EmpresaId = c.EmpresaId AND x.UsuarioWebId = c.UsuarioWebId
          AND x.WPermisoId = destino.Id)
GROUP BY c.EmpresaId, c.UsuarioWebId, c.Movil, m.Modulo;

DECLARE @PorRepartir int = (SELECT COUNT(*) FROM #AlTelefono);
PRINT CONCAT('Concesiones por agregar: ', @PorRepartir);

IF @PorRepartir = 0
    PRINT 'No hay nada que agregar: o ya estaba repartido, o nadie que entre a la app tiene estos modulos.';

BEGIN TRANSACTION;

/* ------------------------------------------------------------------ */
/* 5) Constancia ANTES de escribir                                     */
/* ------------------------------------------------------------------ */
INSERT INTO dbo.WPermisoRespaldoBaja (Lote, EmpresaId, Clave, Origen, UsuarioWebId, Tipo, Motivo)
SELECT @Lote, t.EmpresaId, t.Movil, N'USUARIO', t.UsuarioWebId, 'allow',
       CONCAT(N'Alta automatica (', t.Modulo, N'): entra a la app y ', t.Porque)
FROM #AlTelefono t
WHERE NOT EXISTS (
    SELECT 1 FROM dbo.WPermisoRespaldoBaja b
    WHERE b.Lote = @Lote AND b.Origen = N'USUARIO'
      AND b.EmpresaId = t.EmpresaId AND b.Clave = t.Movil
      AND b.UsuarioWebId = t.UsuarioWebId);

/* ------------------------------------------------------------------ */
/* 6) Agregar. SE AGREGA, nunca se reemplaza ni se quita nada          */
/* ------------------------------------------------------------------ */
INSERT INTO dbo.WUsuarioPermiso (EmpresaId, UsuarioWebId, WPermisoId, Tipo, Motivo, FechaCreacion)
SELECT t.EmpresaId, t.UsuarioWebId, destino.Id, 'allow',
       CONCAT('Canal movil: ', t.Modulo, ' (', t.Porque, ')'),
       SYSUTCDATETIME()
FROM #AlTelefono t
    INNER JOIN dbo.WPermiso destino
        ON destino.EmpresaId = t.EmpresaId AND destino.Clave = t.Movil
WHERE NOT EXISTS (
    SELECT 1 FROM dbo.WUsuarioPermiso x
    WHERE x.EmpresaId = t.EmpresaId AND x.UsuarioWebId = t.UsuarioWebId
      AND x.WPermisoId = destino.Id);

/* ------------------------------------------------------------------ */
/* 7) COMPROBACION -- ANTES del COMMIT                                 */
/* ------------------------------------------------------------------ */

/* 7a) Lo que prometia el script: a nadie de la lista le falta su clave. */
DECLARE @Pendientes int = (
    SELECT COUNT(*)
    FROM #AlTelefono t
        INNER JOIN dbo.WPermiso destino
            ON destino.EmpresaId = t.EmpresaId AND destino.Clave = t.Movil
    WHERE NOT EXISTS (
        SELECT 1 FROM dbo.WUsuarioPermiso x
        WHERE x.EmpresaId = t.EmpresaId AND x.UsuarioWebId = t.UsuarioWebId
          AND x.WPermisoId = destino.Id AND x.Tipo IN ('allow', 'a', 'permit')));

IF @Pendientes > 0
BEGIN
    ROLLBACK TRANSACTION;
    THROW 56306, 'Quedo gente de la lista sin su clave del telefono. Se deshizo todo.', 1;
END;

/* 7b) Que la constancia cuadre con lo escrito: sin ella el script 02 no puede
       deshacer EXACTAMENTE esto, y quitar "lo que se parezca" es peor. */
DECLARE @Respaldadas int = (
    SELECT COUNT(*) FROM dbo.WPermisoRespaldoBaja WHERE Lote = @Lote);

IF @Respaldadas < @PorRepartir
BEGIN
    ROLLBACK TRANSACTION;
    THROW 56307, 'La constancia del respaldo no cuadra con lo que se iba a agregar. Se deshizo todo.', 1;
END;

/* 7c) Que no se haya repartido nada a quien NO entra a la app: es la unica
       regla que este script podria romper al revés. */
DECLARE @SinEntrada int = (
    SELECT COUNT(*)
    FROM dbo.WPermisoRespaldoBaja b
        INNER JOIN @Modulos m ON m.Movil = b.Clave
    WHERE b.Lote = @Lote
      AND NOT EXISTS (
          SELECT 1 FROM #Efectivo e
          WHERE e.EmpresaId = b.EmpresaId AND e.UsuarioWebId = b.UsuarioWebId
            AND e.Clave = @Entrada));

IF @SinEntrada > 0
BEGIN
    ROLLBACK TRANSACTION;
    THROW 56308, 'Se iba a repartir el canal movil a alguien que no entra a la app. Se deshizo todo.', 1;
END;

PRINT CONCAT('Concesiones agregadas: ', @PorRepartir);
PRINT CONCAT('Registradas en dbo.WPermisoRespaldoBaja (lote ', @Lote, '): ', @Respaldadas);

COMMIT TRANSACTION;
GO

/* -------------------- COMO QUEDO -------------------- */

/* Quien quedo con cada clave del telefono, y por que se le dio. */
SELECT b.EmpresaId,
       u.Usuario,
       u.Rol,
       b.Clave AS ClaveDelTelefono,
       b.Motivo
FROM dbo.WPermisoRespaldoBaja b
    LEFT JOIN dbo.UsuarioWeb u ON u.Id = b.UsuarioWebId AND u.EmpresaId = b.EmpresaId
WHERE b.Lote = N'CANAL_MOVIL_12_ALTA'
ORDER BY b.EmpresaId, u.Usuario, b.Clave;

/* Resumen por clave: cuanta gente la tiene ya, de cualquier origen. */
SELECT p.EmpresaId, p.Clave,
       (SELECT COUNT(*) FROM dbo.WUsuarioPermiso up
        WHERE up.WPermisoId = p.Id AND up.Tipo IN ('allow', 'a', 'permit')) AS UsuariosConAllow,
       (SELECT COUNT(*) FROM dbo.WRolPermiso rp WHERE rp.WPermisoId = p.Id) AS Roles
FROM dbo.WPermiso p
WHERE p.Clave IN (
    N'app_movil.proveedores.ver', N'app_movil.cambiosvigencia.ver',
    N'app_movil.concentrados.ver', N'app_movil.garantias.ver',
    N'app_movil.reasignacion.ver', N'app_movil.transferencias.ver',
    N'app_movil.descuentos.ajustes.ver', N'app_movil.compras.ver',
    N'app_movil.dinero_usados.ver', N'app_movil.liquidaciones_cobros.ver',
    N'app_movil.cobranzas.ver', N'app_movil.gasolinas.ver')
ORDER BY p.EmpresaId, p.Clave;
GO
