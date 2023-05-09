using System.ComponentModel;
using System.Data;
using System.Reflection;
using System.Text;

namespace Demapio;

/// <summary>
/// Extension methods for Demapio
/// </summary>
public static class Demapio
{
    /// <summary>
    /// Poor man's Dapper, here so we don't bring library dependencies with us.
    /// </summary>
    public static object? SimpleSelect(this IDbConnection conn, string queryText, object? parameters)
    {
        if (conn.State != ConnectionState.Open) conn.Open();

        using var cmd = conn.CreateCommand();
        cmd.CommandType = CommandType.Text;
        cmd.CommandText = queryText;

        if (parameters != null)
        {
            var props = TypeDescriptor.GetProperties(parameters);
            foreach (PropertyDescriptor prop in props)
            {
                var val = prop.GetValue(parameters);
                cmd.Parameters.Add(MapParameter(cmd, prop.Name, val));
            }
        }

        var result = cmd.ExecuteScalar();
        conn.Close();
        return result;
    }

    /// <summary>
    /// Select a variable number of result objects from a database, using a SQL query and a database connection.
    /// <p>Input parameters will be mapped by property name</p>
    /// <p>Resulting column names will be mapped to the properties of <c>T</c> by name, case insensitive</p>
    /// </summary>
    /// <typeparam name="T">Result object. Must have a public constructor with no parameters, and public settable properties matching the result columns</typeparam>
    private static IEnumerable<T> ParameterSelect<T>(this IDbConnection conn, string queryText, object? parameters) where T : new()
    {
        if (conn.State != ConnectionState.Open) conn.Open();

        using var cmd = conn.CreateCommand();
        cmd.CommandType = CommandType.Text;
        cmd.CommandText = queryText;

        if (parameters != null)
        {
            var props = TypeDescriptor.GetProperties(parameters);
            foreach (PropertyDescriptor prop in props)
            {
                var val = prop.GetValue(parameters);
                if (val is null) continue;
                cmd.Parameters.Add(MapParameter(cmd, prop.Name, val));
            }
        }

        var setters = new Dictionary<string, PropertyInfo>();
        using var reader = cmd.ExecuteReader();
        var result = new List<T>();

        while (reader.Read())
        {
            if (setters.Count < 1) CacheWritableProperties<T>(setters);
            var count = reader.FieldCount;
            var item = new T();
            for (int i = 0; i < count; i++)
            {
                var column = NormaliseName(reader.GetName(i));
                if (!setters.TryGetValue(column, out var setter)) continue; // not writable column

                try
                {
                    setter?.SetValue(item, reader.GetValue(i));
                }
                catch
                {
                    /* ignore? */
                }
            }

            result.Add(item);
        }

        conn.Close();
        return result;
    }

    private static IDbDataParameter MapParameter(IDbCommand cmd, string propName, object? val)
    {
        var result = cmd.CreateParameter();
        result.Value = val;
        result.ParameterName = propName;
        result.SourceColumn = propName;
        
        result.DbType = DbType.Object; // For Npgsql, this means "you work it out"
        result.Direction = ParameterDirection.Input;
        return result;
    }
    
    private static string NormaliseName(string name)
    {
        var sb = new StringBuilder();
        foreach (char c in name)
        {
            if (char.IsLetterOrDigit(c)) sb.Append(char.ToLowerInvariant(c));
        }

        return sb.Length < 1 ? name : sb.ToString();
    }

    private static void CacheWritableProperties<T>(Dictionary<string, PropertyInfo> setters)
    {
        var props = typeof(T).GetProperties().Where(p => p.CanWrite).ToArray();
        foreach (var prop in props)
        {
            setters.TryAdd(NormaliseName(prop.Name), prop);
        }
    }
}