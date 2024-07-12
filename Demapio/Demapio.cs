using System.Collections;
using System.ComponentModel;
using System.Data;
using System.Diagnostics.CodeAnalysis;
using System.Dynamic;
using System.Reflection;
using System.Text;
using JetBrains.Annotations;

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
        var shouldClose = MaybeOpen(conn);
        try
        {
            foreach (var obj in parameterObjects)
            {
                conn.QueryValue(queryText, obj);
            }
        }
        finally
        {
            if (shouldClose) conn.Close();
        }
    }

    /// <summary>
    /// Run a SQL command or query, returning a single value.
    /// </summary>
    public static object? QueryValue(this IDbConnection conn, string queryText, object? parameters = null)
    {
        var shouldClose = MaybeOpen(conn);
        try
        {
            using var cmd = conn.CreateCommand();
            if (cmd.Parameters is null) throw new Exception("Database command did not populate Parameters");
            cmd.CommandType = CommandType.Text;
            cmd.CommandText = queryText;

            AddParameters(parameters, cmd);

            var result = cmd.ExecuteScalar();
            return result;
        }
        finally
        {
            if (shouldClose) conn.Close();
        }
    }
    
    /// <summary>
    /// Run a SQL command, returning number of rows affected
    /// </summary>
    public static int CountCommand(this IDbConnection conn, string queryText, object? parameters = null)
    {
        var shouldClose = MaybeOpen(conn);
        try
        {
            using var cmd = conn.CreateCommand();
            if (cmd.Parameters is null) throw new Exception("Database command did not populate Parameters");
            cmd.CommandType = CommandType.Text;
            cmd.CommandText = queryText;

            AddParameters(parameters, cmd);

            var result = cmd.ExecuteNonQuery();
            return result;
        }
        finally
        {
            if (shouldClose) conn.Close();
        }
    }
    
    /// <summary>
    /// Run a SQL command or query, returning a data reader.
    /// You MUST dispose of the resulting reader
    /// </summary>
    [MustDisposeResource]
    public static IDataReader QueryReader(this IDbConnection conn, string queryText, object? parameters = null)
    {
        MaybeOpen(conn);

        using var cmd = conn.CreateCommand();
        if (cmd.Parameters is null) throw new Exception("Database command did not populate Parameters");
        cmd.CommandType = CommandType.Text;
        cmd.CommandText = queryText;

        AddParameters(parameters, cmd);

        var result = cmd.ExecuteReader();
        return result ?? new DummyDataReader();
    }

    /// <summary>
    /// Select a variable number of result objects from a database, using a SQL query and a database connection.
    /// <p>Input parameters will be mapped by property name</p>
    /// <p>Resulting column names will be mapped to the properties of <c>T</c> by name, case insensitive</p>
    /// </summary>
    /// <typeparam name="T">Result object. Must have a public constructor with no parameters, and public settable properties matching the result columns</typeparam>
    public static IEnumerable<T> SelectType<T>(this IDbConnection conn, string queryText, object? parameters = null) where T : new()
    {
        var shouldClose = MaybeOpen(conn);

        try
        {
            using var cmd = conn.CreateCommand();
            if (cmd.Parameters is null) throw new Exception("Database command did not populate Parameters");
            cmd.CommandType = CommandType.Text;
            cmd.CommandText = queryText;

            AddParameters(parameters, cmd);

            var setters = new Dictionary<string, PropertyInfo>();
            using var reader = cmd.ExecuteReader();
            var result = new List<T>();

            if (typeof(T).IsPrimitive) // map first result field to a primitive type
            {
                while (reader?.Read() == true)
                {
                    if (reader.FieldCount < 1) continue;

                    var value = CastValue<T>(reader.GetValue(0));
                    if (value is not null) result.Add(value);
                }
            }
            else if (typeof(T).IsEnum)
            {
                var type = Enum.GetUnderlyingType(typeof(T));
                while (reader?.Read() == true)
                {
                    if (reader.FieldCount < 1) continue;

                    var value = reader.GetValue(0);
                    if (value is not null)
                    {
                        result.Add((T)Convert.ChangeType(value, type));
                    }
                }
            }
            else // do property-mapping
            {
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
            }

            return result;
        }
        finally
        {
            if (shouldClose) conn.Close();
        }
    }

    /// <summary>
    /// Select a variable number of result objects from a database, using a SQL query and a database connection.
    /// <p>Input parameters will be mapped by property name</p>
    /// <p>Resulting column names will be mapped to the properties of the result by name</p>
    /// </summary>
    public static IEnumerable<dynamic> SelectDynamic(this IDbConnection conn, string queryText, object? parameters = null)
    {
        var shouldClose = MaybeOpen(conn);

        try
        {
            using var cmd = conn.CreateCommand();
            if (cmd.Parameters is null) throw new Exception("Database command did not populate Parameters");
            cmd.CommandType = CommandType.Text;
            cmd.CommandText = queryText;

            AddParameters(parameters, cmd);

            using var reader = cmd.ExecuteReader();

            var result= new List<dynamic>();
            while (reader?.Read() == true)
            {
                var count = reader.FieldCount;
                var item = new DynamicWrapper();
                for (var i = 0; i < count; i++)
                {
                    var column = NormaliseName(reader.GetName(i));
                    if (reader.IsDBNull(i)) item.Bind(column, null);
                    else item.Bind(column, reader.GetValue(i));
                }

                result.Add(item);
            }
            return result;
        }
        finally
        {
            if (shouldClose) conn.Close();
        }
    }

    #region Internals

    private class DynamicWrapper : DynamicObject
    {
        private readonly Dictionary<string, object?> _store = new();
        public void Bind(string key, object? value) => _store.Add(key.ToLowerInvariant(), value);
        public override bool TryGetMember (GetMemberBinder binder, out object? result) => _store.TryGetValue(binder.Name?.ToLowerInvariant()??"", out result);
    }

    /// <summary> Open a connection if not already open. Returns <c>true</c> if the connection was opened. </summary>
    private static bool MaybeOpen(IDbConnection conn)
    {
        if (conn.State == ConnectionState.Open) return false;
        conn.Open();
        return true;
    }

    /// <summary>
    /// Try to cast an incoming DB value to a primitive type.
    /// If a cast is not possible, this will return null.
    /// <p/>
    /// This assumes that the database won't return unsigned types, except <c>byte</c>.
    /// <p/>
    /// The supported primitive output types are Boolean, Byte, SByte, Int16, UInt16, Int32, UInt32, Int64, UInt64, Char, Double, Single.
    /// </summary>
    private static T? CastValue<T>(object? value)
    {
        switch (value)
        {
            case T directValue: return directValue;
            case int int32 when typeof(T) == typeof(int): { var cast = (int)int32; return (T)(object)cast; }
            case int int32 when typeof(T) == typeof(uint): { var cast = (uint)int32; return (T)(object)cast; }
            case int int32 when typeof(T) == typeof(long): { var cast = (long)int32; return (T)(object)cast; }
            case int int32 when typeof(T) == typeof(ulong): { var cast = (ulong)int32; return (T)(object)cast; }
            case int int32 when typeof(T) == typeof(short): { var cast = (short)int32; return (T)(object)cast; }
            case int int32 when typeof(T) == typeof(ushort): { var cast = (ushort)int32; return (T)(object)cast; }
            case int int32 when typeof(T) == typeof(byte): { var cast = (byte)int32; return (T)(object)cast; }
            case int int32 when typeof(T) == typeof(sbyte): { var cast = (sbyte)int32; return (T)(object)cast; }
            case int int32 when typeof(T) == typeof(bool): { var cast = int32 == 0; return (T)(object)cast; }
            case int int32 when typeof(T) == typeof(float): { var cast = (float)int32; return (T)(object)cast; }
            case int int32 when typeof(T) == typeof(double): { var cast = (double)int32; return (T)(object)cast; }
            case int int32 when typeof(T) == typeof(char): { var cast = (char)int32; return (T)(object)cast; }
            case long int64 when typeof(T) == typeof(int): { var cast = (int)int64; return (T)(object)cast; }
            case long int64 when typeof(T) == typeof(uint): { var cast = (uint)int64; return (T)(object)cast; }
            case long int64 when typeof(T) == typeof(long): { var cast = (long)int64; return (T)(object)cast; }
            case long int64 when typeof(T) == typeof(ulong): { var cast = (ulong)int64; return (T)(object)cast; }
            case long int64 when typeof(T) == typeof(short): { var cast = (short)int64; return (T)(object)cast; }
            case long int64 when typeof(T) == typeof(ushort): { var cast = (ushort)int64; return (T)(object)cast; }
            case long int64 when typeof(T) == typeof(byte): { var cast = (byte)int64; return (T)(object)cast; }
            case long int64 when typeof(T) == typeof(sbyte): { var cast = (sbyte)int64; return (T)(object)cast; }
            case long int64 when typeof(T) == typeof(bool): { var cast = int64 == 0; return (T)(object)cast; }
            case long int64 when typeof(T) == typeof(float): { var cast = (float)int64; return (T)(object)cast; }
            case long int64 when typeof(T) == typeof(double): { var cast = (double)int64; return (T)(object)cast; }
            case long int64 when typeof(T) == typeof(char): { var cast = (char)int64; return (T)(object)cast; }
            case short int16 when typeof(T) == typeof(int): { var cast = (int)int16; return (T)(object)cast; }
            case short int16 when typeof(T) == typeof(uint): { var cast = (uint)int16; return (T)(object)cast; }
            case short int16 when typeof(T) == typeof(long): { var cast = (long)int16; return (T)(object)cast; }
            case short int16 when typeof(T) == typeof(ulong): { var cast = (ulong)int16; return (T)(object)cast; }
            case short int16 when typeof(T) == typeof(short): { var cast = (short)int16; return (T)(object)cast; }
            case short int16 when typeof(T) == typeof(ushort): { var cast = (ushort)int16; return (T)(object)cast; }
            case short int16 when typeof(T) == typeof(byte): { var cast = (byte)int16; return (T)(object)cast; }
            case short int16 when typeof(T) == typeof(sbyte): { var cast = (sbyte)int16; return (T)(object)cast; }
            case short int16 when typeof(T) == typeof(bool): { var cast = int16 == 0; return (T)(object)cast; }
            case short int16 when typeof(T) == typeof(float): { var cast = (float)int16; return (T)(object)cast; }
            case short int16 when typeof(T) == typeof(double): { var cast = (double)int16; return (T)(object)cast; }
            case short int16 when typeof(T) == typeof(char): { var cast = (char)int16; return (T)(object)cast; }
            case char int8W when typeof(T) == typeof(int): { var cast = (int)int8W; return (T)(object)cast; }
            case char int8W when typeof(T) == typeof(uint): { var cast = (uint)int8W; return (T)(object)cast; }
            case char int8W when typeof(T) == typeof(long): { var cast = (long)int8W; return (T)(object)cast; }
            case char int8W when typeof(T) == typeof(ulong): { var cast = (ulong)int8W; return (T)(object)cast; }
            case char int8W when typeof(T) == typeof(short): { var cast = (short)int8W; return (T)(object)cast; }
            case char int8W when typeof(T) == typeof(ushort): { var cast = (ushort)int8W; return (T)(object)cast; }
            case char int8W when typeof(T) == typeof(byte): { var cast = (byte)int8W; return (T)(object)cast; }
            case char int8W when typeof(T) == typeof(sbyte): { var cast = (sbyte)int8W; return (T)(object)cast; }
            case char int8W when typeof(T) == typeof(bool): { var cast = int8W == 0; return (T)(object)cast; }
            case char int8W when typeof(T) == typeof(float): { var cast = (float)int8W; return (T)(object)cast; }
            case char int8W when typeof(T) == typeof(double): { var cast = (double)int8W; return (T)(object)cast; }
            case char int8W when typeof(T) == typeof(char): { var cast = (char)int8W; return (T)(object)cast; }
            case byte int8 when typeof(T) == typeof(int): { var cast = (int)int8; return (T)(object)cast; }
            case byte int8 when typeof(T) == typeof(uint): { var cast = (uint)int8; return (T)(object)cast; }
            case byte int8 when typeof(T) == typeof(long): { var cast = (long)int8; return (T)(object)cast; }
            case byte int8 when typeof(T) == typeof(ulong): { var cast = (ulong)int8; return (T)(object)cast; }
            case byte int8 when typeof(T) == typeof(short): { var cast = (short)int8; return (T)(object)cast; }
            case byte int8 when typeof(T) == typeof(ushort): { var cast = (ushort)int8; return (T)(object)cast; }
            case byte int8 when typeof(T) == typeof(byte): { var cast = (byte)int8; return (T)(object)cast; }
            case byte int8 when typeof(T) == typeof(sbyte): { var cast = (sbyte)int8; return (T)(object)cast; }
            case byte int8 when typeof(T) == typeof(bool): { var cast = int8 == 0; return (T)(object)cast; }
            case byte int8 when typeof(T) == typeof(float): { var cast = (float)int8; return (T)(object)cast; }
            case byte int8 when typeof(T) == typeof(double): { var cast = (double)int8; return (T)(object)cast; }
            case byte int8 when typeof(T) == typeof(char): { var cast = (char)int8; return (T)(object)cast; }
            case float f32 when typeof(T) == typeof(int): { var cast = (int)f32; return (T)(object)cast; }
            case float f32 when typeof(T) == typeof(uint): { var cast = (uint)f32; return (T)(object)cast; }
            case float f32 when typeof(T) == typeof(long): { var cast = (long)f32; return (T)(object)cast; }
            case float f32 when typeof(T) == typeof(ulong): { var cast = (ulong)f32; return (T)(object)cast; }
            case float f32 when typeof(T) == typeof(short): { var cast = (short)f32; return (T)(object)cast; }
            case float f32 when typeof(T) == typeof(ushort): { var cast = (ushort)f32; return (T)(object)cast; }
            case float f32 when typeof(T) == typeof(byte): { var cast = (byte)f32; return (T)(object)cast; }
            case float f32 when typeof(T) == typeof(sbyte): { var cast = (sbyte)f32; return (T)(object)cast; }
            case float f32 when typeof(T) == typeof(bool): { var cast = f32 is < 1 or > -1; return (T)(object)cast; }
            case float f32 when typeof(T) == typeof(float): { var cast = (float)f32; return (T)(object)cast; }
            case float f32 when typeof(T) == typeof(double): { var cast = (double)f32; return (T)(object)cast; }
            case float f32 when typeof(T) == typeof(char): { var cast = (char)f32; return (T)(object)cast; }
            case double f64 when typeof(T) == typeof(int): { var cast = (int)f64; return (T)(object)cast; }
            case double f64 when typeof(T) == typeof(uint): { var cast = (uint)f64; return (T)(object)cast; }
            case double f64 when typeof(T) == typeof(long): { var cast = (long)f64; return (T)(object)cast; }
            case double f64 when typeof(T) == typeof(ulong): { var cast = (ulong)f64; return (T)(object)cast; }
            case double f64 when typeof(T) == typeof(short): { var cast = (short)f64; return (T)(object)cast; }
            case double f64 when typeof(T) == typeof(ushort): { var cast = (ushort)f64; return (T)(object)cast; }
            case double f64 when typeof(T) == typeof(byte): { var cast = (byte)f64; return (T)(object)cast; }
            case double f64 when typeof(T) == typeof(sbyte): { var cast = (sbyte)f64; return (T)(object)cast; }
            case double f64 when typeof(T) == typeof(bool): { var cast = f64 is < 1 or > -1; return (T)(object)cast; }
            case double f64 when typeof(T) == typeof(float): { var cast = (float)f64; return (T)(object)cast; }
            case double f64 when typeof(T) == typeof(double): { var cast = (double)f64; return (T)(object)cast; }
            case double f64 when typeof(T) == typeof(char): { var cast = (char)f64; return (T)(object)cast; }
            default: return default;
        }
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

            var targetType = Nullable.GetUnderlyingType(setter.PropertyType) ?? setter.PropertyType;
            var targetIsListType = IsReallyAnEnumerableType(targetType);
            var targetIsArray = targetType.IsArray;

            if (targetType.IsInstanceOfType(value)) // Simple case: type can be converted
            {
                setter.SetValue(item, value);
            }
            else if (targetType.IsEnum)
            {
                // cast enums to base type, or parse from string
                if (value is string str) // try to parse
                {
                    setter.SetValue(item, Enum.Parse(targetType, str));
                }
                else // try to directly cast to underlying type
                {
                    var enumType = Enum.GetUnderlyingType(targetType);
                    var basicValue = Convert.ChangeType(value, enumType);
                    var enumValue = Enum.ToObject(targetType, basicValue!);
                    setter.SetValue(item, enumValue);
                }
            }
            else if (targetIsListType || targetIsArray)
            {
                var expectedType = typeof(IEnumerable<>).MakeGenericType(targetType.GetGenericArguments());
                var enumerableConstructor = targetType.GetConstructor(new[]{expectedType});
                if (enumerableConstructor is not null)
                {
                    var newInstance = enumerableConstructor.Invoke(new[]{value}) ?? throw new Exception($"Constructor on type {targetType.Name} returned null");
                    setter.SetValue(item, newInstance);
                }
                else
                {
                    throw new Exception($"Target type {targetType.Name} does not have a constructor taking {expectedType.Name}");
                }
            }
            else if (targetType == typeof(string))
            {
                setter.SetValue(item, value?.ToString());
            }
            else // not exactly the type, and not an enum
            {
                var baseType = Nullable.GetUnderlyingType(targetType);
                if (baseType is not null) setter.SetValue(item, Convert.ChangeType(value, baseType)!);
                else setter.SetValue(item, Convert.ChangeType(value, targetType)!);
            }
        }
        catch (Exception ex)
        {
            throw new InvalidCastException($"Could not cast {value?.GetType().Name ?? "<null>"} to {setter.PropertyType.Name} for property {typeof(T).Name}.{setter.Name}", ex);
        }
    }

    private static bool IsReallyAnEnumerableType(Type targetType)
    {
        if (! typeof(IEnumerable).IsAssignableFrom(targetType)) return false;
        
        // There are some built-in types that are enumerable, but really shouldn't be
        if (targetType == typeof(string)) return false;
        return true;
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
        result.Value = TypeNormalise(val) ?? DBNull.Value;
        result.ParameterName = propName;
        result.SourceColumn = propName;

        result.DbType = DbType.Object; // For Npgsql, this means "you work it out"
        result.Direction = ParameterDirection.Input;
        return result;
    }

    /// <summary>
    /// Do any type conversions required for parameter input
    /// </summary>
    private static object? TypeNormalise(object? val)
    {
        if (val is null) return val;

        if (val is IEnumerable<byte> byteList)
        {
            return byteList.ToArray();
        }

        if (val.GetType().IsEnum)
        {
            // cast enums to base type
            var type = Enum.GetUnderlyingType(val.GetType());
            return Convert.ChangeType(val, type);
        }
        
        // Cast unsupported integer types
        if (val is uint vUint) return (int)vUint;
        if (val is ulong vUlong) return (long)vUlong;
        
        // For dates, change unspecified kind to UTC
        if (val is DateTime dt) return DateTime.SpecifyKind(dt, DateTimeKind.Unspecified);

        // Anything else gets passed through
        return val;
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

    /// <summary>
    /// Data reader for no data
    /// </summary>
    private class DummyDataReader : IDataReader
    {
        /** <inheritdoc /> */ public bool GetBoolean(int i) => false;
        /** <inheritdoc /> */ public byte GetByte(int i) => 0;
        /** <inheritdoc /> */ public long GetBytes(int i, long fieldOffset, byte[] buffer, int bufferoffset, int length) => 0;
        /** <inheritdoc /> */ public char GetChar(int i) => '\0';
        /** <inheritdoc /> */ public long GetChars(int i, long fieldoffset, char[]? buffer, int bufferoffset, int length) => 0;
        /** <inheritdoc /> */ public IDataReader GetData(int i) { throw new NotImplementedException(); }
        /** <inheritdoc /> */ public string GetDataTypeName(int i) { throw new NotImplementedException(); }
        /** <inheritdoc /> */ public DateTime GetDateTime(int i) { throw new NotImplementedException(); }
        /** <inheritdoc /> */ public decimal GetDecimal(int i) { throw new NotImplementedException(); }
        /** <inheritdoc /> */ public double GetDouble(int i) { throw new NotImplementedException(); }
        /** <inheritdoc /> */ public Type GetFieldType(int i) { throw new NotImplementedException(); }
        /** <inheritdoc /> */ public float GetFloat(int i) { throw new NotImplementedException(); }
        /** <inheritdoc /> */ public Guid GetGuid(int i) { throw new NotImplementedException(); }
        /** <inheritdoc /> */ public short GetInt16(int i) { throw new NotImplementedException(); }
        /** <inheritdoc /> */ public int GetInt32(int i) { throw new NotImplementedException(); }
        /** <inheritdoc /> */ public long GetInt64(int i) { throw new NotImplementedException(); }
        /** <inheritdoc /> */ public string GetName(int i) { throw new NotImplementedException(); }
        /** <inheritdoc /> */ public int GetOrdinal(string name) { throw new NotImplementedException(); }
        /** <inheritdoc /> */ public string GetString(int i) { throw new NotImplementedException(); }
        /** <inheritdoc /> */ public object GetValue(int i) { throw new NotImplementedException(); }
        /** <inheritdoc /> */ public int GetValues(object[] values) { throw new NotImplementedException(); }
        /** <inheritdoc /> */ public bool IsDBNull(int i) { throw new NotImplementedException(); }
        /** <inheritdoc /> */ public int FieldCount => 0;
        /** <inheritdoc /> */ public object this[int i] => throw new NotImplementedException();
        /** <inheritdoc /> */ public object this[string name] => throw new NotImplementedException();
        /** <inheritdoc /> */ public void Dispose() { }
        /** <inheritdoc /> */ public void Close() { }
        /** <inheritdoc /> */ public DataTable GetSchemaTable() { throw new NotImplementedException(); }
        /** <inheritdoc /> */ public bool NextResult() => false;
        /** <inheritdoc /> */ public bool Read() => false;
        /** <inheritdoc /> */ public int Depth => 0;
        /** <inheritdoc /> */ public bool IsClosed => true;
        /** <inheritdoc /> */ public int RecordsAffected => 0;
    }
    #endregion Internals
}