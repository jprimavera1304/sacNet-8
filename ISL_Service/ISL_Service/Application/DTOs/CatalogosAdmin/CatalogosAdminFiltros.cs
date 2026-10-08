namespace ISL_Service.Application.DTOs.CatalogosAdmin;

/*
  LOS FILTROS DE LAS SIETE CONSULTAS QUE FALTABAN.

  El modulo de Catalogos del front nuevo necesita LEER los catalogos que hasta
  ahora solo se podian ESCRIBIR: CatalogosAdminController tenia el alta, la
  modificacion y las dos bajas de almacenes, agentes, tipos de gasto, autos,
  grupos de categoria, categorias y tipos de usado, pero ningun GET de listado.
  Una pantalla de catalogo que puede dar de alta y no puede enseñar lo que hay
  no es media pantalla: no es ninguna.

  Los nombres de los campos son los del sp_n_Consulta* correspondiente, para que
  al leer el repositorio no haya que traducir dos veces.

  CONVENCION DE @IDStatus, la misma de todo Mac31:
      0 = todos, 1 = activos, 2 = inactivos.
  En los SP que usan fn_RegresaTablaStatus(0) eso sale solo; en
  sp_n_ConsultaAlmacenes el 0 simplemente no agrega el AND. El resultado es el
  mismo y por eso el contrato puede ser uno.

  @Identico: 0 = "empieza con" (el LIKE 'texto%' de legacy), 1 = exacto. El
  front manda 0 siempre, igual que Mac31; se expone porque los SP lo tienen y
  porque un buscador de "dame exactamente este" lo va a querer.
*/

/// <summary>Filtro de la consulta de almacenes. sp_n_ConsultaAlmacenes.</summary>
public class AlmacenesFiltro
{
    public int IdAlmacen { get; set; }
    public int IdStatus { get; set; }
    public string? Almacen { get; set; }
    public bool Identico { get; set; }
}

/// <summary>Filtro de la consulta de agentes. sp_n_ConsultaAgentes.</summary>
public class AgentesFiltro
{
    public int IdAgente { get; set; }
    public int IdStatus { get; set; }
    /// <summary>El numero del agente, que en Mac31 es la columna Agente. 0 = todos.</summary>
    public int NumeroAgente { get; set; }
    public string? NombreAgente { get; set; }
    public bool Identico { get; set; }
}

/// <summary>Filtro de la consulta de tipos de gasto. sp_n_ConsultaTiposGastos.</summary>
public class TiposGastoFiltro
{
    public int IdTipoGasto { get; set; }
    public int IdStatus { get; set; }
    public string? TipoGasto { get; set; }
    public bool Identico { get; set; }
}

/// <summary>
/// Filtro de la consulta de autos. sp_n_ConsultaAutos.
///
/// NO TIENE @Identico: el SP solo hace LIKE sobre el nombre. Se deja como esta
/// en vez de inventar el parametro, que es lo que haria creer que el backend
/// sabe filtrar exacto cuando el procedimiento no puede.
/// </summary>
public class AutosFiltro
{
    public int IdAuto { get; set; }
    public int IdStatus { get; set; }
    public string? Nombre { get; set; }
}

/// <summary>Filtro de la consulta de grupos de categoria (en Mac31 la UI les dice "categorias"). sp_n_ConsultaGrupoCategorias.</summary>
public class GruposCategoriaFiltro
{
    /// <summary>Lista de ids separados por coma, como la espera el SP. Vacio = todos.</summary>
    public string? IdsGrupoCategoria { get; set; }
    public int IdStatus { get; set; }
    public string? GrupoCategoria { get; set; }
    public bool Identico { get; set; }
}

/// <summary>Filtro de la consulta de categorias de Mac31 (la UI les dice "sub categorias"). sp_n_ConsultaCategorias.</summary>
public class CategoriasMacFiltro
{
    public string? IdsCategoria { get; set; }
    /// <summary>Filtra las subcategorias de uno o varios grupos. Vacio = de todos.</summary>
    public string? IdsGrupoCategoria { get; set; }
    public int IdStatus { get; set; }
    public string? Categoria { get; set; }
    public bool Identico { get; set; }
}

/// <summary>Filtro de la consulta de tipos de usado (cascos). sp_n_ConsultaTiposUsados.</summary>
public class TiposUsadoFiltro
{
    public int IdTipoUsado { get; set; }
    public int IdStatus { get; set; }
    public string? TipoUsado { get; set; }
    public bool Identico { get; set; }
}

/// <summary>Filtro de la consulta de empresas de Mac31. sp_n_ConsultaEmpresas.</summary>
public class EmpresasMacFiltro
{
    public int IdEmpresa { get; set; }
    public int IdStatus { get; set; }
    public string? RazonSocial { get; set; }
    public bool Identico { get; set; }
}

/// <summary>Una marca y si entra o no en la subcategoria.</summary>
public class MarcaDeLaCategoria
{
    public int IdMarca { get; set; }
    public bool Marcado { get; set; }
}

/// <summary>
/// La relacion COMPLETA de una subcategoria con las marcas, para guardarla de un
/// golpe. Ver el comentario del endpoint.
/// </summary>
public class RelacionCategoriaMarcasLoteRequest
{
    public List<MarcaDeLaCategoria> Marcas { get; set; } = new();
}
