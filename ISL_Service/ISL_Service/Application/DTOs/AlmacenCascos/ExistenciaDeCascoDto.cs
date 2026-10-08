namespace ISL_Service.Application.DTOs.AlmacenCascos;

/// <summary>
/// Un renglon del panel "CASCOS ACTUAL" de Mac31: un tipo de casco y cuantos
/// hay en existencia.
///
/// DE DONDE SALE, Y POR QUE NO DE api/catalogos/tipos-casco
/// --------------------------------------------------------
/// `GET api/catalogos/tipos-casco` consulta `[Catalogo TiposUsados]` directo y
/// proyecta SOLO el id y la descripcion: no trae existencia, y no la puede
/// traer, porque la existencia no vive en esa tabla — la pone el JOIN con
/// `EmpresaUsado` de la EMPRESA en curso. Un casco tiene una existencia por
/// empresa, no una sola.
///
/// Por eso aqui se llama a `sp_n_ConsultaTiposUsados`, que es el procedimiento
/// que usa Mac31 para pintar ese mismo panel (`ConsultarUsadosMovimientos`:
/// `ConsultaTipoUsados()` -> `api/cattipousado/consultar`). Se leyo su cuerpo
/// antes de exponerlo: es una lectura pura —una tabla temporal, un `INSERT`
/// dentro de ella y un `SELECT`— y no escribe en ninguna tabla del negocio, al
/// contrario que `sp_n_ConsultaConstantes`, que con la fecha de operacion
/// atrasada hace el CIERRE DEL DIA y por eso NO se expone en ningun endpoint de
/// consulta (ver la nota de la clase CortesController).
///
/// El `sp_n_` no se toca: solo se llama.
/// </summary>
public class ExistenciaDeCascoDto
{
    public int IdTipoCasco { get; set; }

    /// <summary>El nombre largo: "11141608 MINI CHICO (1)".</summary>
    public string Descripcion { get; set; } = string.Empty;

    /// <summary>
    /// El nombre corto: "MINI CHICO(1)".
    ///
    /// Va aparte porque Mac31 ensena UNO DE LOS DOS segun la empresa: el panel
    /// pinta `TipoUsadoCorto` cuando la funcionalidad es TAU y `TipoUsado` en el
    /// resto. Se mandan los dos y que el front elija: decidirlo aqui obligaria
    /// al backend a saber de empresas, que es justo lo que las capacidades
    /// vinieron a quitar.
    /// </summary>
    public string DescripcionCorta { get; set; } = string.Empty;

    public int Existencia { get; set; }

    /// <summary>
    /// El orden del catalogo. Mac31 lista los cascos por `Orden` y no por
    /// nombre: del mas chico al mas grande, que es como los nombra la gente.
    /// Ordenado por nombre, "MINI CHICO (1)" cae despues de "GRANDE (5)".
    /// </summary>
    public int Orden { get; set; }
}
