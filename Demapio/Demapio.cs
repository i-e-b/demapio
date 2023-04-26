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
    public static object? SimpleSelect(this System.Data.IDbConnection conn, string queryText, object? parameters)
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
                //cmd.Parameters.AddWithValue(prop.Name, val);
                cmd.Parameters[prop.Name] = val;
            }
        }

        var result = cmd.ExecuteScalar();
        conn.Close();
        return result;
    }

    /// <summary>
    /// Poor man's Dapper.
    /// This is to work around a bug in real Dapper.
    /// </summary>
    private IEnumerable<T> ParameterSelect<T>(string queryText, object? parameters) where T : new()
    {
        using var conn = new NpgsqlConnection(_connectionString);
        conn.Open();

        using var cmd = new NpgsqlCommand(queryText, conn);

        if (parameters != null)
        {
            var props = TypeDescriptor.GetProperties(parameters);
            foreach (PropertyDescriptor prop in props)
            {
                var val = prop.GetValue(parameters);
                if (val is null) continue;
                cmd.Parameters.AddWithValue(prop.Name, val);
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