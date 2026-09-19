using System.Data;
using System.Globalization;
using System.Reflection;

namespace ISL_Service.Utils
{
    public static class Funciones
    {

        #region "DataTableToList"
        public static List<T> DataTableToList<T>(DataTable dt)
        {
            List<T> data = new List<T>();
            foreach (DataRow row in dt.Rows)
            {
                T item = GetItem<T>(row);
                data.Add(item);
            }
            return data;
        }
        #endregion


        #region "GetItem"
        private static T GetItem<T>(DataRow dr)
        {
            Type temp = typeof(T);
            T obj = Activator.CreateInstance<T>();

            foreach (DataColumn column in dr.Table.Columns)
            {
                foreach (PropertyInfo pro in temp.GetProperties())
                {
                    if (pro.Name == column.ColumnName)
                    {
                        if (dr[column.ColumnName] != DBNull.Value)
                        {
                            var rawValue = dr[column.ColumnName];
                            var converted = ConvertToPropertyType(rawValue, pro.PropertyType);
                            pro.SetValue(obj, converted, null);
                        }
                    }
                    else
                        continue;
                }
            }
            return obj;
        }
        #endregion

        #region "ConvertToPropertyType"
        private static object? ConvertToPropertyType(object value, Type targetType)
        {
            if (value == null) return null;

            var nonNullableType = Nullable.GetUnderlyingType(targetType) ?? targetType;
            if (nonNullableType.IsInstanceOfType(value)) return value;

            if (nonNullableType == typeof(string))
                return Convert.ToString(value, CultureInfo.InvariantCulture);

            if (nonNullableType == typeof(Guid))
                return value is Guid g ? g : Guid.Parse(Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty);

            if (nonNullableType == typeof(DateTime))
                return value is DateTime dt ? dt : Convert.ToDateTime(value, CultureInfo.InvariantCulture);

            if (nonNullableType.IsEnum)
            {
                if (value is string s) return Enum.Parse(nonNullableType, s, ignoreCase: true);
                return Enum.ToObject(nonNullableType, value);
            }

            return Convert.ChangeType(value, nonNullableType, CultureInfo.InvariantCulture);
        }
        #endregion

        #region "DataTableToRows"
        /// <summary>
        /// Convierte un DataTable en renglones sueltos con las llaves en
        /// camelCase, tal cual salen del SP.
        ///
        /// Existe para los result sets de legacy que traen DECENAS de columnas
        /// con nombre ya definido (la rejilla de asistencias son 41: fechaDia1,
        /// diaEntrada1, diaSalida1 ... por siete dias). Declarar un DTO con 41
        /// propiedades no agrega ninguna seguridad: si el SP cambia una columna,
        /// DataTableToList la ignora en silencio y el campo llega vacio sin que
        /// nadie se entere. Pasando el renglon completo, lo que el SP dejo de
        /// mandar simplemente no aparece, y eso si se nota.
        ///
        /// Ademas respeta los NULL: la copia de Zaragoza de
        /// sp_n_ConsultaEmpleadosPeriodoAsistencia devuelve 'asistencias' en NULL
        /// para algunos empleados (le falta un ISNULL que la de Tauro si tiene),
        /// y eso tiene que llegar al front como null, no como cero. Un cero
        /// seria una mentira: cero asistencias y "no se pudo calcular" no son lo
        /// mismo.
        /// </summary>
        public static List<Dictionary<string, object?>> DataTableToRows(DataTable dt)
        {
            var rows = new List<Dictionary<string, object?>>(dt.Rows.Count);
            var nombres = new string[dt.Columns.Count];
            for (int i = 0; i < dt.Columns.Count; i++)
                nombres[i] = ACamelCase(dt.Columns[i].ColumnName);

            foreach (DataRow dr in dt.Rows)
            {
                var item = new Dictionary<string, object?>(dt.Columns.Count, StringComparer.Ordinal);
                for (int i = 0; i < dt.Columns.Count; i++)
                {
                    var raw = dr[i];
                    item[nombres[i]] = raw == DBNull.Value ? null : raw;
                }
                rows.Add(item);
            }
            return rows;
        }

        /// <summary>
        /// Solo baja la primera letra. No se parte por palabras a proposito:
        /// legacy ya devuelve las columnas de estos SP en camelCase
        /// (idEmpleado, fechaDia1, minsRetardo) y lo unico que hace falta es
        /// normalizar las pocas que vienen en Pascal (PagoInmediato). Un
        /// convertidor "listo" convertiria IDEmpleado en iDEmpleado y rompería
        /// el contrato con el front.
        /// </summary>
        private static string ACamelCase(string nombre)
        {
            if (string.IsNullOrEmpty(nombre)) return nombre;
            if (!char.IsUpper(nombre[0])) return nombre;

            // "IDEmpleado" -> "idEmpleado": si arrancan dos mayusculas, se bajan
            // las dos, que es como lo escribe el resto de la API.
            if (nombre.Length > 1 && char.IsUpper(nombre[1]))
                return char.ToLowerInvariant(nombre[0]) + char.ToLowerInvariant(nombre[1]) + nombre.Substring(2);

            return char.ToLowerInvariant(nombre[0]) + nombre.Substring(1);
        }
        #endregion

    }
}
