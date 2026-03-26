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

    }
}
