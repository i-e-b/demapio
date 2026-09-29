using System.ComponentModel;
using System.Globalization;

namespace DemapioTests;


// ReSharper disable InconsistentNaming
// ReSharper disable UnusedMember.Global
// ReSharper disable PropertyCanBeMadeInitOnly.Global
// ReSharper disable UnusedAutoPropertyAccessor.Global

public class WeirdContainer
{
    public int Id { get; set; }

    public WeirdType? TypeValue { get; set; }

    public override string ToString()
    {
        return $"Id={Id}; TypeValue={TypeValue};";
    }
}

public class WeirdType
{
    public WeirdType(string msg)
    {
        Message = msg;
    }

    public string Message { get; set; }

    public static WeirdType? FromText(object? arg)
    {
        return arg is null ? null : new WeirdType(arg.ToString()!);
    }

    public static object? ToText(WeirdType? arg)
    {
        return arg?.Message;
    }

    public override string ToString()
    {
        return Message;
    }
}

public class GeneratedPropertyType
{
    public string PartA { get; set; } = "";
    public string PartB { get; set; } = "";

    public string Mix => $"Mix {PartA} with {PartB}";
}

public class GuidTestType
{
    public int Id { get; set; }
    public Guid? GuidCol { get; set; }
}

public class GuidAsStringTestType
{
    public int Id { get; set; }
    public string? GuidCol { get; set; }
}

public class DateTimeValues
{
    public DateTime? NullableDateOne { get; set; }
    public DateTime? NullableDateTwo { get; set; }
    public DateTime DateOne { get; set; }
    public DateTime DateTwo { get; set; }
    public DateTime DateThree { get; set; }
}

public class ByteArrayValue
{
    public int Id { get; set; }
    public byte[] Data { get; set; } = Array.Empty<byte>();

    public override string ToString()
    {
        return $"ID={Id}; Data='{Convert.ToHexString(Data)}'";
    }
}
public class ByteListValue
{
    public int Id { get; set; }
    // ReSharper disable once CollectionNeverUpdated.Global
    public List<byte> Data { get; set; } = new();

    public override string ToString()
    {
        return $"ID={Id}; Data='{Convert.ToHexString(Data.ToArray())}'";
    }
}
public class ByteEnumerableValue
{
    public int Id { get; set; }
    public IEnumerable<byte>? Data { get; set; }

    public override string ToString()
    {
        if (Data is null) return $"ID={Id}; Data=<null>";
        return $"ID={Id}; Data='{Convert.ToHexString(Data.ToArray())}'";
    }
}

public class NullablePrimitives
{
    public long? NLong { get; set; }
    public int? NInt { get; set; }
    public GeoZone? NEnum { get; set; }

    private static string MarkNulls<T>(T thing) => thing?.ToString() ?? "<null>";

    public override string ToString()
    {
        return $"NLong={MarkNulls(NLong)}; NInt={MarkNulls(NInt)}; NEnum='{MarkNulls(NEnum)}'";
    }
}

public class PocoWithEnums
{
    public int Id { get; set; }
    public Geolocation Location { get; set; }
    public GeoLandmass Landmass { get; set; }
    public GeoZone Zone { get; set; }
    public double Sales { get; set; }

    public override string ToString()
    {
        return $"{Id}: {Zone.ToString()}/{Landmass.ToString()}/{Location.ToString()} = {Sales:0.00}";
    }
}

public class SamplePoco
{
    public int Id { get; set; }
    public long UserId { get; set; }
    public string? DeviceId { get; set; }

    public override string ToString()
    {
        return $"Id={Id}; UserId={UserId}; DeviceId='{DeviceId}'";
    }
}

public class UsingConvertibleNumber
{
    public WideNumber Id { get; set; } = WideNumber.Zero;
    public long UserId { get; set; }
    public string? DeviceId { get; set; }
}

public class WideNumber : IConvertible
{
    private readonly double _doubleValue;
    private readonly bool   _doubleOk;

    private readonly decimal _decimalValue;
    private readonly bool    _decimalOk;

    private readonly ulong _ulongValue;
    private readonly bool  _ulongOk;

    private readonly long _longValue;
    private readonly bool _longOk;

    private readonly string _original;

    /// <summary>
    /// Zero valued number
    /// </summary>
    public static WideNumber Zero { get; } = new("0");

    /// <summary>
    /// Try to parse a string as a range of wide number types.
    /// Returns true if at least one type parsed successfully.
    /// </summary>
    public static bool TryParse(string str, out WideNumber result)
    {
        result = new WideNumber(str);
        return result._doubleOk || result._decimalOk || result._ulongOk || result._longOk;
    }

    /// <summary>
    /// Try to parse a string as a range of wide number types.
    /// Throws an exception if the value cannot be parsed
    /// </summary>
    public static WideNumber Parse(string str)
    {
        var result = new WideNumber(str);
        var ok = result._doubleOk || result._decimalOk || result._ulongOk || result._longOk;

        if (!ok) throw new Exception($"Could not parse numeric value '{str}'");
        return result;
    }

    private WideNumber(string str)
    {
        _original = str;
        _doubleOk = double.TryParse(str, NumberStyles.Number, CultureInfo.InvariantCulture, out _doubleValue);
        _decimalOk = decimal.TryParse(str, NumberStyles.Number, CultureInfo.InvariantCulture, out _decimalValue);
        _ulongOk = ulong.TryParse(str, NumberStyles.Number, CultureInfo.InvariantCulture, out _ulongValue);
        _longOk = long.TryParse(str, NumberStyles.Number, CultureInfo.InvariantCulture, out _longValue);
    }

    /// <summary>
    /// Try to cast this WideNumber type to a primitive value.
    /// Returns null if the cast is not supported.
    /// </summary>
    /// <param name="type">Target type</param>
    /// <param name="precisionLoss">Set to true if the cast is from a floating point to a fixed point or integer value; or from a fixed point to integer value</param>
    /// <returns></returns>
    public object? CastTo(Type? type, out bool precisionLoss)
    {
        precisionLoss = false;
        if (type is null) return null;

        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>))
            type = type.GetGenericArguments().FirstOrDefault() ?? throw new Exception("Invalid type definition: nullable wrapper with no internal type defined");

        if (type == typeof(sbyte))
        {
            if (_longOk) return (sbyte)_longValue;
            if (!_decimalOk) return null;
            precisionLoss = true;
            return (sbyte)_decimalValue;
        }

        if (type == typeof(short))
        {
            if (_longOk) return (short)_longValue;
            if (!_decimalOk) return null;
            precisionLoss = true;
            return (short)_decimalValue;
        }

        if (type == typeof(int))
        {
            if (_longOk) return (int)_longValue;
            if (!_decimalOk) return null;
            precisionLoss = true;
            return (int)_decimalValue;
        }

        if (type == typeof(long))
        {
            if (_longOk) return _longValue;
            if (!_decimalOk) return null;
            precisionLoss = true;
            return (long)_decimalValue;
        }

        if (type == typeof(byte))
        {
            if (_ulongOk) return (byte)_ulongValue;
            return null;
        }

        if (type == typeof(ushort))
        {
            if (_ulongOk) return (ushort)_ulongValue;
            return null;
        }

        if (type == typeof(uint))
        {
            if (_ulongOk) return (uint)_ulongValue;
            return null;
        }

        if (type == typeof(ulong))
        {
            if (_ulongOk) return _ulongValue;
            return null;
        }

        if (type == typeof(decimal))
        {
            if (_decimalOk) return _decimalValue;
            return null;
        }

        if (type == typeof(float))
        {
            if (_doubleOk) return (float)_doubleValue;
            if (!_decimalOk) return null;
            precisionLoss = true;
            return (float)_decimalValue;
        }

        if (type == typeof(double))
        {
            if (_doubleOk) return _doubleValue;
            if (!_decimalOk) return null;
            precisionLoss = true;
            return (double)_decimalValue;
        }

        if (type == typeof(string))
        {
            return _original;
        }

        return null;
    }

    /// <summary>
    /// Cast a wide number to long
    /// </summary>
    public static implicit operator long(WideNumber src)
    {
        return src.ToLong();
    }

    /// <summary>
    /// Cast a wide number to double
    /// </summary>
    public static implicit operator double(WideNumber src)
    {
        return src.ToDouble();
    }


    /// <summary>
    /// Cast long to a wide number
    /// </summary>
    public static implicit operator WideNumber(long src)
    {
        return new(src.ToString());
    }

    /// <summary>
    /// Cast a double to a wide number
    /// </summary>
    public static implicit operator WideNumber(double src)
    {
        return new(src.ToString(CultureInfo.InvariantCulture));
    }

    /// <summary>
    /// Return the full precision string value
    /// </summary>
    public override string ToString() => _original;

    /// <summary>
    /// Convert to a <c>long</c> type. This may go out of range
    /// </summary>
    public long ToLong()
    {
        if (_longOk) return _longValue;
        if (_doubleOk) return (long)_doubleValue;
        if (_decimalOk) return (long)_decimalValue;
        throw new Exception("Could not convert numeric value to 'long' type");
    }

    /// <summary>
    /// Convert to a <c>double</c> type. This may involve precision loss
    /// </summary>
    public double ToDouble()
    {
        if (_doubleOk) return _doubleValue;
        if (_decimalOk) return (double)_decimalValue;
        if (_longOk) return _longValue;
        throw new Exception("Could not convert numeric value to 'double' type");
    }

    #region IConvertable

    /// <inheritdoc />
    public TypeCode GetTypeCode()
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc />
    public bool ToBoolean(IFormatProvider? provider)
    {
        if (_longOk) return _longValue != 0;
        if (_doubleOk) return (_doubleValue != 0);
        if (_decimalOk) return (_decimalValue != 0);
        return !string.IsNullOrEmpty(_original);
    }

    /// <inheritdoc />
    public byte ToByte(IFormatProvider? provider)
    {
        if (_longOk) return (byte)_longValue;
        if (_doubleOk) return (byte)_doubleValue;
        if (_decimalOk) return (byte)_decimalValue;
        return 0;
    }

    /// <inheritdoc />
    public char ToChar(IFormatProvider? provider)
    {
        if (_longOk) return (char)_longValue;
        if (_doubleOk) return (char)_doubleValue;
        if (_decimalOk) return (char)_decimalValue;
        return (char)0;
    }

    /// <inheritdoc />
    public DateTime ToDateTime(IFormatProvider? provider)
    {
        if (_longOk) return new DateTime(ticks: _longValue);
        if (_doubleOk) return new DateTime(ticks: (long)_doubleValue);
        if (_decimalOk) return new DateTime(ticks: (long)_decimalValue);
        return DateTime.MinValue;
    }

    /// <inheritdoc />
    public decimal ToDecimal(IFormatProvider? provider)
    {
        if (_longOk) return _longValue;
        if (_doubleOk) return (decimal)_doubleValue;
        if (_decimalOk) return _decimalValue;
        return 0m;
    }

    /// <inheritdoc />
    public double ToDouble(IFormatProvider? provider)
    {
        if (_longOk) return _longValue;
        if (_doubleOk) return _doubleValue;
        if (_decimalOk) return (double)_decimalValue;
        return 0;
    }

    /// <inheritdoc />
    public short ToInt16(IFormatProvider? provider)
    {
        if (_longOk) return (short)_longValue;
        if (_doubleOk) return (short)_doubleValue;
        if (_decimalOk) return (short)_decimalValue;
        return 0;
    }

    /// <inheritdoc />
    public int ToInt32(IFormatProvider? provider)
    {
        if (_longOk) return (int)_longValue;
        if (_doubleOk) return (int)_doubleValue;
        if (_decimalOk) return (int)_decimalValue;
        return 0;
    }

    /// <inheritdoc />
    public long ToInt64(IFormatProvider? provider)
    {
        if (_longOk) return _longValue;
        if (_doubleOk) return (long)_doubleValue;
        if (_decimalOk) return (long)_decimalValue;
        return 0;
    }

    /// <inheritdoc />
    public sbyte ToSByte(IFormatProvider? provider)
    {
        if (_longOk) return (sbyte)_longValue;
        if (_doubleOk) return (sbyte)_doubleValue;
        if (_decimalOk) return (sbyte)_decimalValue;
        return 0;
    }

    /// <inheritdoc />
    public float ToSingle(IFormatProvider? provider)
    {
        if (_longOk) return _longValue;
        if (_doubleOk) return (float)_doubleValue;
        if (_decimalOk) return (float)_decimalValue;
        return 0;
    }

    /// <inheritdoc />
    public string ToString(IFormatProvider? provider)
    {
        return _original;
    }

    /// <inheritdoc />
    public object ToType(Type conversionType, IFormatProvider? provider)
    {
        if (conversionType == typeof(string)) return _original;
        return CastTo(conversionType, out _) ?? throw new Exception($"Cannot cast numeric type to {conversionType.Name}");
    }

    /// <inheritdoc />
    public ushort ToUInt16(IFormatProvider? provider)
    {
        if (_longOk) return (ushort)_longValue;
        if (_doubleOk) return (ushort)_doubleValue;
        if (_decimalOk) return (ushort)_decimalValue;
        return 0;
    }

    /// <inheritdoc />
    public uint ToUInt32(IFormatProvider? provider)
    {
        if (_longOk) return (uint)_longValue;
        if (_doubleOk) return (uint)_doubleValue;
        if (_decimalOk) return (uint)_decimalValue;
        return 0;
    }

    /// <inheritdoc />
    public ulong ToUInt64(IFormatProvider? provider)
    {
        if (_longOk) return (ulong)_longValue;
        if (_doubleOk) return (ulong)_doubleValue;
        if (_decimalOk) return (ulong)_decimalValue;
        return 0;
    }

    #endregion IConvertible
}

public enum GeoZone : long
{
    APAC, EMEA, AMER
}

public enum GeoLandmass: long
{
    Europe, WestAsia, EastAsia, Subcontinent, NorthAfrica, SubSahara,
    Oceania, NorthAmerica, CentralAmerica, SouthAmerica, Other
}

[DefaultValue(Kenya)]
public enum Geolocation: long
{
    China, India, USA, Indonesia, Pakistan, Nigeria,
    Brazil, Bangladesh, Russia, Mexico, Japan,
    Philippines, Ethiopia, Egypt, Vietnam, DrCongo,
    Iran, Turkey, Germany, France, UK, Thailand,
    Tanzania, SouthAfrica, Italy, Myanmar, SouthKorea,
    Colombia, Spain, Kenya, Argentina, Algeria,
    Sudan, Uganda, Iraq, Ukraine, Canada, Poland,
    Morocco, Uzbekistan, SaudiArabia, Yemen, Peru,
    Angola, Afghanistan, Malaysia, Mozambique, Ghana,
    IvoryCoast, Nepal, Venezuela, Madagascar,
    Australia, NorthKorea, Cameroon, Niger, Taiwan,
    Mali, SriLanka, Syria, BurkinaFaso, Malawi,
    Chile, Kazakhstan, Zambia, Romania, Ecuador,
    Netherlands, Somalia, Senegal, Guatemala, Chad
}