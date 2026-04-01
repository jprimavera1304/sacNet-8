# -*- coding: utf-8 -*-
from pathlib import Path

root = Path(__file__).resolve().parent
pairs = [
    ("Token invalido.", "Token inválido."),
    ("Token invalido:", "Token inválido:"),
    ("Modulo invalido.", "Módulo inválido."),
    ("Payload invalido.", "Payload inválido."),
    ("categoriaId invalido.", "categoriaId inválido."),
    ("diaJuego invalido.", "diaJuego inválido."),
    ("profesorTitularId invalido.", "profesorTitularId inválido."),
    ("profesorAuxiliarId invalido.", "profesorAuxiliarId inválido."),
    ("diaJuegoPredeterminado invalido.", "diaJuegoPredeterminado inválido."),
    ("responsableCobroId invalido.", "responsableCobroId inválido."),
    ("estatusChequeNuevo invalido.", "estatusChequeNuevo inválido."),
    ("canchas contiene elementos invalidos.", "canchas contiene elementos inválidos."),
    ("partidos contiene elementos invalidos.", "partidos contiene elementos inválidos."),
    ("IdStatus invalido.", "IdStatus inválido."),
    ("Permisos invalidos:", "Permisos inválidos:"),
    ("Codigo de rol invalido.", "Código de rol inválido."),
    ("permisos semilla para los modulos solicitados.", "permisos semilla para los módulos solicitados."),
    ("Permisos en modulos inactivos:", "Permisos en módulos inactivos:"),
    ("La clave debe tener formato modulo.accion", "La clave debe tener formato módulo.acción"),
    ("tiene formato invalido.", "tiene formato inválido."),
    ("Usuario invalido.", "Usuario inválido."),
    ("Rol invalido.", "Rol inválido."),
    ("IDPersona invalido.", "IDPersona inválido."),
    ("Ticket invalido.", "Ticket inválido."),
    ("QR invalido en", "QR inválido en"),
    ("no existe en catalogo de roles.", "no existe en catálogo de roles."),
    ("Token invalido: falta", "Token inválido: falta"),
    ("Ver modulo", "Ver módulo"),
    ("Categorias -", "Categorías -"),
    ("Ver Administracion", "Ver Administración"),
    ("Editar Catalogo", "Editar Catálogo"),
    ("Generacion Rol Torneo", "Generación Rol Torneo"),
]
for path in root.rglob("*.cs"):
    if path.name.startswith("_"):
        continue
    text = path.read_text(encoding="utf-8")
    orig = text
    for a, b in pairs:
        text = text.replace(a, b)
    if text != orig:
        path.write_text(text, encoding="utf-8", newline="\n")
        print("updated", path.relative_to(root))

Path(__file__).unlink(missing_ok=True)
print("done")
