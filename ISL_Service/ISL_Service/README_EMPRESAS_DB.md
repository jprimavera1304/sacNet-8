# Guia Global: Consolidacion de Empresas (`EmpresaWeb`)

Este documento estandariza cambios de empresas en BD para evitar errores por llaves foraneas.

## Objetivo

- Dejar una sola empresa activa (por ejemplo, Zaragoza o Aviacion).
- Reasignar todas las referencias `EmpresaId` de forma global.
- Manejar casos donde se requiere cambiar el `Id` de `EmpresaWeb` (columna `IDENTITY`).

## Reglas de seguridad

1. Ejecuta siempre en ventana de mantenimiento.
2. Haz backup antes de correr scripts.
3. Usa transacción (`BEGIN TRAN`) y valida antes de `COMMIT`.
4. Si algo falla: `ROLLBACK`.

## 1) Inventario: tablas que referencian `EmpresaWeb`

```sql
SELECT
    fk.name AS FK,
    OBJECT_SCHEMA_NAME(fk.parent_object_id) AS EsquemaHija,
    OBJECT_NAME(fk.parent_object_id) AS TablaHija
FROM sys.foreign_keys fk
WHERE fk.referenced_object_id = OBJECT_ID('dbo.EmpresaWeb')
ORDER BY EsquemaHija, TablaHija;
```

## 2) Inventario: tablas con columna `EmpresaId`

```sql
SELECT
    t.name AS Tabla,
    c.name AS Columna
FROM sys.columns c
JOIN sys.tables t ON t.object_id = c.object_id
WHERE c.name = 'EmpresaId'
ORDER BY t.name;
```

## 3) Script global: mover referencias `EmpresaId` (todas las tablas)

Este script actualiza automaticamente todas las tablas que tengan columna `EmpresaId`.

```sql
DECLARE @OldEmpresaId INT = 3; -- origen
DECLARE @NewEmpresaId INT = 1; -- destino

DECLARE @sql NVARCHAR(MAX) = N'';

SELECT @sql = @sql + N'
IF OBJECT_ID(''' + QUOTENAME(s.name) + N'.' + QUOTENAME(t.name) + N''',''U'') IS NOT NULL
BEGIN
    UPDATE ' + QUOTENAME(s.name) + N'.' + QUOTENAME(t.name) + N'
    SET EmpresaId = ' + CAST(@NewEmpresaId AS NVARCHAR(20)) + N'
    WHERE EmpresaId = ' + CAST(@OldEmpresaId AS NVARCHAR(20)) + N';
END;
'
FROM sys.tables t
JOIN sys.schemas s ON s.schema_id = t.schema_id
JOIN sys.columns c ON c.object_id = t.object_id
WHERE c.name = 'EmpresaId';

PRINT @sql;
EXEC sp_executesql @sql;
```

## 4) Caso A: dejar solo una empresa (ejemplo: solo Zaragoza Id = 3)

```sql
BEGIN TRY
    BEGIN TRAN;

    DECLARE @KeepEmpresaId INT = 3;
    DECLARE @sql NVARCHAR(MAX) = N'';

    -- Reasignar todas las referencias hacia la empresa que se conserva
    SELECT @sql = @sql + N'
    UPDATE ' + QUOTENAME(s.name) + N'.' + QUOTENAME(t.name) + N'
    SET EmpresaId = ' + CAST(@KeepEmpresaId AS NVARCHAR(20)) + N'
    WHERE EmpresaId <> ' + CAST(@KeepEmpresaId AS NVARCHAR(20)) + N';
    '
    FROM sys.tables t
    JOIN sys.schemas s ON s.schema_id = t.schema_id
    JOIN sys.columns c ON c.object_id = t.object_id
    WHERE c.name = 'EmpresaId';

    EXEC sp_executesql @sql;

    -- Eliminar empresas sobrantes
    DELETE FROM dbo.EmpresaWeb
    WHERE Id <> @KeepEmpresaId;

    COMMIT;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK;
    THROW;
END CATCH;
```

## 5) Caso B: convertir Zaragoza (3) en Id = 1

Como `EmpresaWeb.Id` es `IDENTITY`, no se hace `UPDATE` directo.

```sql
BEGIN TRY
    BEGIN TRAN;

    DECLARE @OldId INT = 3; -- Zaragoza actual
    DECLARE @NewId INT = 1; -- Zaragoza destino
    DECLARE @sql NVARCHAR(MAX) = N'';

    -- 1) Mover cualquier referencia 1 -> 3 para liberar el Id 1
    SELECT @sql = @sql + N'
    UPDATE ' + QUOTENAME(s.name) + N'.' + QUOTENAME(t.name) + N'
    SET EmpresaId = ' + CAST(@OldId AS NVARCHAR(20)) + N'
    WHERE EmpresaId = ' + CAST(@NewId AS NVARCHAR(20)) + N';
    '
    FROM sys.tables t
    JOIN sys.schemas s ON s.schema_id = t.schema_id
    JOIN sys.columns c ON c.object_id = t.object_id
    WHERE c.name = 'EmpresaId';

    EXEC sp_executesql @sql;

    -- 2) Borrar empresa Id = 1 (si existe)
    DELETE FROM dbo.EmpresaWeb WHERE Id = @NewId;

    -- 3) Insertar copia de Id = 3 como Id = 1
    SET IDENTITY_INSERT dbo.EmpresaWeb ON;

    INSERT INTO dbo.EmpresaWeb (Id, Clave, Nombre, Estado, FechaCreacion, FechaActualizacion)
    SELECT @NewId, Clave, Nombre, Estado, FechaCreacion, FechaActualizacion
    FROM dbo.EmpresaWeb
    WHERE Id = @OldId;

    SET IDENTITY_INSERT dbo.EmpresaWeb OFF;

    -- 4) Mover todas las referencias 3 -> 1
    SET @sql = N'';
    SELECT @sql = @sql + N'
    UPDATE ' + QUOTENAME(s.name) + N'.' + QUOTENAME(t.name) + N'
    SET EmpresaId = ' + CAST(@NewId AS NVARCHAR(20)) + N'
    WHERE EmpresaId = ' + CAST(@OldId AS NVARCHAR(20)) + N';
    '
    FROM sys.tables t
    JOIN sys.schemas s ON s.schema_id = t.schema_id
    JOIN sys.columns c ON c.object_id = t.object_id
    WHERE c.name = 'EmpresaId';

    EXEC sp_executesql @sql;

    -- 5) Borrar registro viejo Id = 3
    DELETE FROM dbo.EmpresaWeb WHERE Id = @OldId;

    COMMIT;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK;
    THROW;
END CATCH;
```

## 6) Validaciones post cambio

```sql
-- Empresas finales
SELECT * FROM dbo.EmpresaWeb ORDER BY Id;

-- Conteo por empresa en todas las tablas con EmpresaId (revision rapida manual por tabla clave)
SELECT EmpresaId, COUNT(*) AS Total
FROM dbo.UsuarioWeb
GROUP BY EmpresaId
ORDER BY EmpresaId;
```

## 7) Nota operativa

Si existe lógica de negocio por `Clave` o por dominio en frontend/backend, valida configuraciones después del cambio:

- `AllowedOrigins`
- `CompanyId` o `companyKey` en API
- `API_BASE_URL` y dominios por empresa en frontend

## 8) Script puntual: subir a `SuperAdmin` al usuario JUAN

```sql
UPDATE dbo.UsuarioWeb
SET Rol = 'SuperAdmin'
WHERE Usuario = 'JUAN'
  AND EmpresaId = 1;
```

Opcional para validar:

```sql
SELECT Usuario, Rol, EmpresaId
FROM dbo.UsuarioWeb
WHERE Usuario = 'JUAN';
```

## 9) Script puntual: agregar empresa

```sql
INSERT INTO dbo.EmpresaWeb (Clave, Nombre, Estado, FechaCreacion, FechaActualizacion)
VALUES (N'aviacion', N'Aviacion', 1, SYSUTCDATETIME(), SYSUTCDATETIME());
GO
```

Opcional para validar:

```sql
SELECT Id, Clave, Nombre, Estado, FechaCreacion, FechaActualizacion
FROM dbo.EmpresaWeb
ORDER BY Id;
```
