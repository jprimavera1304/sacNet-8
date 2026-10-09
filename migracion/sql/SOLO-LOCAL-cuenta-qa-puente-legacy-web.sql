/*
  CUENTA DE PRUEBA QA_LEGWEB  (SOLO LOCAL, ADITIVA Y REVERSIBLE)

  Para que DEMO no sirve: DEMO tiene TODOS los procesos de Mac31 encendidos a
  proposito, asi que con esa cuenta nunca se puede comprobar que algo SE
  ESCONDE sin permiso. Hace falta una cuenta con permisos PARCIALES.

  Lo que se le da en Mac31 (MacZ / Zaragoza), a proposito desparejo:
    CONSULTA DE COMPRAS        btnModificar (3068)  -> compras.editar       SI
    CONSULTA DE COMPRAS        btnCancelar  (3067)  -> compras.cancelar     NO (negado explicito)
    CONSULTA DE GARANTIAS      btnBuscar    (5142)  -> garantias.ver        SI
    CONSULTA DE GARANTIAS      btnModificar (4067)  -> garantias.editar     NO (ni fila)
    CORTES                     btnVer       (5352)  -> cortes.ver           SI
    CORTES                     btnNuevo     (5351)  -> cortes.crear         NO (ni fila)
    FECHA DE OPERACION         btnGuardarFechaVentas (5070) -> constantes.fechaventas.editar SI
    FECHA DE OPERACION         btnGuardarFechaPagos  (5071) -> constantes.fechapagos.editar  NO (negado explicito)

  Asi se distinguen los TRES estados que el puente tiene que saber leer:
  concedido (IDStatus=1), negado a mano (IDStatus=2) y sin fila (que tambien es
  negado, pero por otro camino del SQL).

  REVERSION: al final de este archivo, comentada.
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;
-- UsuarioWeb tiene un indice sobre la columna calculada UsuarioNorm, y eso
-- obliga a QUOTED_IDENTIFIER ON para poder insertar.
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
BEGIN TRANSACTION;

DECLARE @IDUsuario int;

-- 1) El usuario en Mac31. Idempotente: si ya existe, se reutiliza.
SELECT @IDUsuario = IDUsuario FROM dbo.Usuarios WHERE Usuario = 'QA_LEGWEB';

IF @IDUsuario IS NULL
BEGIN
    -- IDUsuario es IDENTITY: lo pone la base, no se elige.
    INSERT INTO dbo.Usuarios (IDStatus, Usuario, Contrasena, Nombre, IDPerfil, contrasenaDesc)
    VALUES (1, 'QA_LEGWEB', 'QA2026', 'QA PUENTE LEGACY-WEB', NULL, 'QA2026');
    SET @IDUsuario = CONVERT(int, SCOPE_IDENTITY());
END

-- 2) Los permisos desparejos en Mac31.
DECLARE @Procesos TABLE (IDProceso int, Estado int);
INSERT INTO @Procesos (IDProceso, Estado) VALUES
    (3068, 1),   -- compras.editar               concedido
    (3067, 2),   -- compras.cancelar             negado a mano
    (5142, 1),   -- garantias.ver                concedido
    (5352, 1),   -- cortes.ver                   concedido
    (5070, 1),   -- constantes.fechaventas.editar concedido
    (5071, 2);   -- constantes.fechapagos.editar  negado a mano

DELETE ufp
FROM dbo.[Usuario Forma Procesos] ufp
WHERE ufp.IDUsuario = @IDUsuario
  AND ufp.IDProceso IN (SELECT IDProceso FROM @Procesos);

INSERT INTO dbo.[Usuario Forma Procesos] (IDUsuario, IDProceso, IDStatus, IDUsuarioAlta, FechaAlta)
SELECT @IDUsuario, p.IDProceso, p.Estado, @IDUsuario, GETDATE()
FROM @Procesos p;

-- 3) El usuario del web, con el mismo nombre (asi los amarra
--    ResolveLegacyUserIdAsync) y con la contrasena de DEMO para no inventar un
--    hash a mano. Rol QA_LEGWEB: un rol propio y vacio, para que lo unico que
--    tenga venga de Mac31 y de sus overrides.
IF NOT EXISTS (SELECT 1 FROM dbo.UsuarioWeb WHERE UsuarioNorm = 'QA_LEGWEB')
BEGIN
    -- UsuarioNorm es columna CALCULADA: no se escribe, sale de Usuario.
    INSERT INTO dbo.UsuarioWeb (Id, Usuario, ContrasenaHash, Rol, EmpresaId, DebeCambiarContrasena, Estado, FechaCreacion, FechaActualizacion)
    SELECT NEWID(), 'QA_LEGWEB', ContrasenaHash, 'QA_LEGWEB', EmpresaId, 0, 1, SYSUTCDATETIME(), SYSUTCDATETIME()
    FROM dbo.UsuarioWeb WHERE UsuarioNorm = 'DEMO';
END

-- 4) El rol del web, vacio a proposito, mas la ENTRADA a los tres modulos.
--    Sin la entrada el usuario no probaria nada: contestaria 403 en todo y no
--    se sabria si es por falta de entrada o por falta del permiso fino.
IF NOT EXISTS (SELECT 1 FROM dbo.WRol WHERE EmpresaId = 1 AND Codigo = 'QA_LEGWEB')
    INSERT INTO dbo.WRol (EmpresaId, Codigo, Nombre) VALUES (1, 'QA_LEGWEB', 'QA puente legacy-web');

COMMIT TRANSACTION;

SELECT 'QA_LEGWEB listo' AS Resultado, IDUsuario FROM dbo.Usuarios WHERE Usuario = 'QA_LEGWEB';

/*
  REVERSION (correr tal cual deja la base como estaba):

  DECLARE @U int = (SELECT IDUsuario FROM dbo.Usuarios WHERE Usuario = 'QA_LEGWEB');
  DELETE FROM dbo.[Usuario Forma Procesos] WHERE IDUsuario = @U;
  DELETE FROM dbo.WUsuarioPermiso WHERE UsuarioWebId IN (SELECT Id FROM dbo.UsuarioWeb WHERE UsuarioNorm = 'QA_LEGWEB');
  DELETE FROM dbo.UsuarioWeb WHERE UsuarioNorm = 'QA_LEGWEB';
  DELETE FROM dbo.WRolPermiso WHERE RolId IN (SELECT RolId FROM dbo.WRol WHERE EmpresaId = 1 AND Codigo = 'QA_LEGWEB');
  DELETE FROM dbo.WRol WHERE EmpresaId = 1 AND Codigo = 'QA_LEGWEB';
  DELETE FROM dbo.Usuarios WHERE Usuario = 'QA_LEGWEB';
*/
