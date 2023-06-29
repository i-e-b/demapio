using System.ComponentModel;
using System.Data;
using System.Reflection;
using System.Text;

namespace Demapio;

/// <summary>
/// Extension methods for Demapio, the tiny ORM
/// </summary>
public static class Demapio
{
    /// <summary>
    /// Repeat a single command with multiple parameter objects.
    /// <p>This does not return values. It should be used for batch inserts etc.</p>
    /// </summary>
    public static void RepeatCommand(this IDbConnection conn, string queryText, params object[] parameterObjects)
    {
        foreach (var obj in parameterObjects)
        {
            conn.QueryValue(queryText, obj);
        }
    }

    /// <summary>
    /// Run a SQL command or query, returning a single value.
    /// </summary>
    public static object? QueryValue(this IDbConnection conn, string queryText, object? parameters = null)
    {
        if (conn.State != ConnectionState.Open) conn.Open();

        using var cmd = conn.CreateCommand();
        if (cmd.Parameters is null) throw new Exception("Database command did not populate Parameters");
        cmd.CommandType = CommandType.Text;
        cmd.CommandText = queryText;

        AddParameters(parameters, cmd);

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
    public static IEnumerable<T> SelectType<T>(this IDbConnection conn, string queryText, object? parameters) where T : new()
    {
        var shouldClose = false;
        if (conn.State != ConnectionState.Open)
        {
            shouldClose = true;
            conn.Open();
        }

        using var cmd = conn.CreateCommand();
        if (cmd.Parameters is null) throw new Exception("Database command did not populate Parameters");
        cmd.CommandType = CommandType.Text;
        cmd.CommandText = queryText;

        AddParameters(parameters, cmd);

        var setters = new Dictionary<string, PropertyInfo>();
        using var reader = cmd.ExecuteReader();
        var result = new List<T>();

        while (reader?.Read() == true)
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
                    setter?.SetValue(item, reader.GetValue(i)!);
                }
                catch
                {
                    /* ignore? */
                }
            }

            result.Add(item);
        }

        if (shouldClose) conn.Close();
        return result;
    }

    private static void AddParameters(object? parameters, IDbCommand cmd)
    {
        if (parameters is null || cmd.Parameters is null) return;

        if (parameters is IDictionary<string, object?> dict)
        {
            foreach (var (name, val) in dict)
            {
                if (name is null) continue;
                cmd.Parameters.Add(
                    val is null
                        ? NullParameter(cmd, name)
                        : MapParameter(cmd, name, val));
            }
        }
        else
        {
            var props = TypeDescriptor.GetProperties(parameters);
            foreach (PropertyDescriptor prop in props)
            {
                var val = prop.GetValue(parameters);
                cmd.Parameters.Add(
                    val is null
                        ? NullParameter(cmd, prop.Name)
                        : MapParameter(cmd, prop.Name, val));
            }
        }
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
    
    private static IDbDataParameter NullParameter(IDbCommand cmd, string propName)
    {
        var result = cmd.CreateParameter();
        result.Value = DBNull.Value;
        result.ParameterName = propName;
        result.SourceColumn = propName;
        
        result.DbType = DbType.Object; // For Npgsql, this means "you work it out"
        result.Direction = ParameterDirection.Input;
        return result;
    }
    
    private static string NormaliseName(string? name)
    {
        if (name is null) return "";
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