using System.Data;
using System.Reflection;

namespace KindleMate2.Infrastructure.Helpers {
    public class DataTableHelper {
        public static DataTable ToDataTable<T>(IList<T> data) {
            var table = new DataTable(typeof(T).Name);

            if (data.Count == 0) {
                return table;
            }

            // Get all public properties of T
            var props = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance);

            // Create columns
            foreach (PropertyInfo prop in props) {
                Type propType = Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType;
                table.Columns.Add(prop.Name, propType);
            }

            // Add rows
            foreach (T item in data) {
                var values = new object[props.Length];
                for (var i = 0; i < props.Length; i++) {
                    values[i] = props[i].GetValue(item) ?? DBNull.Value;
                }
                table.Rows.Add(values);
            }

            return table;
        }
    }
}