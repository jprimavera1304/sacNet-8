SET NOCOUNT ON;
-- Foto para gasolinas y cierre de contrato.
-- Aqui NO van IDUsuario ni Equipo: en esta base dbo.UsuarioWeb no tiene columna
-- que ligue con dbo.Usuarios de Mac31, asi que cualquier token emitido aqui
-- lleva idUsuario = 0, y el controlador de Legacy RECHAZA IDUsuario = 0 en estos
-- endpoints (GasolinasController.cs:39). O sea que la firma no se puede hacer
-- coincidir en esta maquina; lo que se compara es el dato.
PRINT '## Gasolina (las de la prueba, sin la firma)';
SELECT CAST(IDGasolina AS varchar(10)) + '|' + CAST(IDAuto AS varchar(10)) + '|' + CAST(IDRepartidor AS varchar(10)) + '|' + NumeroRecibo
     + '|' + CONVERT(varchar(30), FechaRecibo, 120) + '|' + CAST(KmInicial AS varchar(10)) + '|' + CAST(KmFinal AS varchar(10))
     + '|' + CAST(KmRecorridos AS varchar(10)) + '|' + CAST(PrecioLitro AS varchar(30)) + '|' + CAST(TotalRecibo AS varchar(30))
     + '|' + CAST(ISNULL(LitrosReales,-1) AS varchar(30)) + '|' + CAST(ISNULL(KmLitro,-1) AS varchar(30))
     + '|cancelada=' + CASE WHEN FechaCancelacion IS NULL THEN 'no' ELSE 'si' END
FROM dbo.Gasolina WHERE NumeroRecibo LIKE '9999%' OR IDGasolina IN (37, 38) ORDER BY IDGasolina;

PRINT '## MC_Contratos (cierre)';
SELECT CAST(IDContrato AS varchar(10)) + '|cerrado=' + CAST(ISNULL(ContratoCerrado,-1) AS varchar(10))
FROM dbo.MC_Contratos ORDER BY IDContrato;

PRINT '## Totales';
SELECT 'Gasolina=' + CAST(COUNT(*) AS varchar(10)) FROM dbo.Gasolina;
SELECT 'MC_Contratos=' + CAST(COUNT(*) AS varchar(10)) FROM dbo.MC_Contratos;
