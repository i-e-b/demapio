using System.ComponentModel;
using System.Data;
using System.Diagnostics.CodeAnalysis;
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

                TrySetValue(setter, item, reader, i);
            }

            result.Add(item);
        }

        if (shouldClose) conn.Close();
        return result;
    }

    private static void TrySetValue<T>(PropertyInfo? setter, [DisallowNull] T item, IDataReader reader, int i) where T : new()
    {
        if (setter is null) return;
        var value = reader.GetValue(i);
        
        try
        {
            if (value is null || value == DBNull.Value)
            {
                setter.SetValue(item, null!);
                return;
            }

            var targetType = setter.PropertyType;

            if (targetType.IsInstanceOfType(value)) // Simple case: type can be converted
            {
                setter.SetValue(item, value);
                return;
            }

            if (!targetType.IsEnum)
            {
                setter.SetValue(item, Convert.ChangeType(value, targetType)!);
                return;
            }

            // cast enums to base type, or parse from string
            if (value is string str) // try to parse
            {
                setter.SetValue(item, Enum.Parse(targetType, str));
            }
            else // try to directly cast to underlying type
            {
                var enumType = Enum.GetUnderlyingType(targetType);
                var enumValue = Convert.ChangeType(value, enumType);

                setter.SetValue(item, enumValue!);
            }
        }
        catch (Exception ex)
        {
            throw new InvalidCastException($"Could not cast {value?.GetType().Name ?? "<null>"} to {setter.PropertyType.Name} for property {typeof(T).Name}.{setter.Name}", ex);
        }
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
        result.Value = TypeNormalise(val);
        result.ParameterName = propName;
        result.SourceColumn = propName;
        
        result.DbType = DbType.Object; // For Npgsql, this means "you work it out"
        result.Direction = ParameterDirection.Input;
        return result;
    }

    private static object? TypeNormalise(object? val)
    {
        if (val is null) return val;
        if (!val.GetType().IsEnum) return val; // pass normal types directly
        
        // cast enums to base type
        var type = Enum.GetUnderlyingType(val.GetType());
        return Convert.ChangeType(val, type);
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