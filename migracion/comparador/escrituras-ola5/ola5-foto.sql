SET NOCOUNT ON;
-- Foto del estado de la base para las escrituras de la ola 5.
-- Lo que importa de una escritura no es la respuesta HTTP: es que quede el mismo
-- renglon. Se sacan los campos que escriben los SP, no los ids (el id nuevo
-- depende del MAX de la tabla y es igual en los dos lados porque las dos
-- corridas arrancan de la misma instantanea).

PRINT '## MC_TiposObra';
SELECT CAST(IDStatus AS varchar(10)) + '|' + TipoObra + '|' + CAST(ISNULL(IDUsuario,-1) AS varchar(10)) + '|' + ISNULL(Equipo,'<null>')
FROM dbo.MC_TiposObra ORDER BY IDTipoObra;

PRINT '## MC_TiposContratacion';
SELECT CAST(IDStatus AS varchar(10)) + '|' + TipoContratacion + '|' + CAST(ISNULL(IDUsuario,-1) AS varchar(10)) + '|' + ISNULL(Equipo,'<null>')
FROM dbo.MC_TiposContratacion ORDER BY IDTipoContratacion;

PRINT '## MC_TiposDocumento';
SELECT CAST(IDStatus AS varchar(10)) + '|' + TipoDocumento + '|' + CAST(ISNULL(IDUsuario,-1) AS varchar(10)) + '|' + ISNULL(Equipo,'<null>')
FROM dbo.MC_TiposDocumento ORDER BY IDTipoDocumento;

PRINT '## MC_TiposRecurso';
SELECT CAST(IDStatus AS varchar(10)) + '|' + TipoRecurso + '|' + CAST(ISNULL(IDUsuario,-1) AS varchar(10)) + '|' + ISNULL(Equipo,'<null>')
FROM dbo.MC_TiposRecurso ORDER BY IDTipoRecurso;

PRINT '## MC_Puestos';
SELECT CAST(IDStatus AS varchar(10)) + '|' + Puesto + '|' + CAST(EsRepresentanteLegal AS varchar(10)) + '|' + CAST(EsRepresentanteTecnico AS varchar(10)) + '|' + CAST(ISNULL(IDUsuario,-1) AS varchar(10)) + '|' + ISNULL(Equipo,'<null>')
FROM dbo.MC_Puestos ORDER BY IDPuesto;

PRINT '## MC_Residentes';
SELECT CAST(IDStatus AS varchar(10)) + '|' + Residente + '|' + CAST(IDPuesto AS varchar(10)) + '|' + CAST(EsSupervision AS varchar(10)) + '|' + CAST(EsObra AS varchar(10)) + '|' + CAST(ISNULL(IDUsuario,-1) AS varchar(10)) + '|' + ISNULL(Equipo,'<null>')
FROM dbo.MC_Residentes ORDER BY IDResidente;

PRINT '## MC_UnidadesMedida';
SELECT CAST(IDStatus AS varchar(10)) + '|' + UnidadMedida + '|' + CAST(ISNULL(IDUsuario,-1) AS varchar(10)) + '|' + ISNULL(Equipo,'<null>')
FROM dbo.MC_UnidadesMedida ORDER BY IDUnidadMedida;

PRINT '## MC_ContratosGrupos';
SELECT CAST(IDContrato AS varchar(10)) + '|' + CAST(IDStatus AS varchar(10)) + '|' + ISNULL(Clave,'<null>') + '|' + ISNULL(Grupo,'<null>') + '|' + CAST(ISNULL(Orden,-1) AS varchar(10)) + '|' + CAST(ISNULL(IDUsuario,-1) AS varchar(10)) + '|' + ISNULL(Equipo,'<null>')
FROM dbo.MC_ContratosGrupos ORDER BY IDContratoGrupo;

PRINT '## Gasolina (las de la prueba)';
SELECT CAST(IDAuto AS varchar(10)) + '|' + CAST(IDRepartidor AS varchar(10)) + '|' + NumeroRecibo + '|' + CONVERT(varchar(30), FechaRecibo, 120) + '|' + CAST(KmInicial AS varchar(10)) + '|' + CAST(KmFinal AS varchar(10)) + '|' + CAST(PrecioLitro AS varchar(30)) + '|' + CAST(TotalRecibo AS varchar(30)) + '|' + CAST(ISNULL(IDUsuario,-1) AS varchar(10)) + '|' + ISNULL(Equipo,'<null>') + '|' + CAST(ISNULL(IDUsuarioCancelacion,-1) AS varchar(10)) + '|' + ISNULL(EquipoCancelacion,'<null>')
FROM dbo.Gasolina WHERE NumeroRecibo LIKE 'OLA5%' ORDER BY IDGasolina;

PRINT '## Totales';
SELECT 'MC_TiposObra=' + CAST(COUNT(*) AS varchar(10)) FROM dbo.MC_TiposObra;
SELECT 'MC_Puestos=' + CAST(COUNT(*) AS varchar(10)) FROM dbo.MC_Puestos;
SELECT 'MC_Residentes=' + CAST(COUNT(*) AS varchar(10)) FROM dbo.MC_Residentes;
SELECT 'MC_UnidadesMedida=' + CAST(COUNT(*) AS varchar(10)) FROM dbo.MC_UnidadesMedida;
SELECT 'MC_ContratosGrupos=' + CAST(COUNT(*) AS varchar(10)) FROM dbo.MC_ContratosGrupos;
SELECT 'Gasolina=' + CAST(COUNT(*) AS varchar(10)) FROM dbo.Gasolina;
