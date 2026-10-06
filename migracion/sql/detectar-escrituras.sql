SET NOCOUNT ON;
WITH limpio AS (
    SELECT p.name,
           -- Se quitan primero las escrituras a tablas TEMPORALES, que no
           -- cuentan: viven solo durante la llamada. Es la fuente principal de
           -- falsos positivos, porque los sp_n_ arman sus resultados en #tmp.
           REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(
               UPPER(REPLACE(REPLACE(m.definition, CHAR(9), ' '), CHAR(10), ' ')),
               'INSERT INTO #', '~'), 'INSERT  INTO #', '~'),
               'DELETE #', '~'), 'DELETE FROM #', '~'),
               'UPDATE #', '~'), 'SELECT * INTO #', '~') AS d
    FROM sys.sql_modules m
    JOIN sys.procedures p ON p.object_id = m.object_id
)
SELECT name + '|' + CAST(CASE WHEN
        d LIKE '%INSERT %' OR d LIKE '%UPDATE %' OR d LIKE '%DELETE %'
     OR d LIKE '%MERGE %'  OR d LIKE '%TRUNCATE TABLE%'
    THEN 1 ELSE 0 END AS char(1))
FROM limpio;
