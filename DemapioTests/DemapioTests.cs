using Demapio;
using Npgsql;
using NUnit.Framework;
#pragma warning disable CS8602

namespace DemapioTests;

// ReSharper disable CommentTypo
/// <summary>
/// These tests will try to connect to a local Postgresql/CockroachDB server.
/// 
/// To run these tests, start a crdb instance. Under Windows, a temp DB can be set up like this:
/// <code>
/// cd /cockroach-windows
/// .\cockroach.exe demo --disable-demo-license --no-example-database --embedded --insecure --sql-port 26299 --http-port 5020
///  
/// root@127.0.0.1:26299/defaultdb> \unset errexit
/// root@127.0.0.1:26299/defaultdb> CREATE DATABASE IF NOT EXISTS testdb;
/// </code>
/// 
/// </summary>
// ReSharper restore CommentTypo
[TestFixture]
public class DemapioTests
{
    private const string ConnStr = @"Server=127.0.0.1;Port=26299;Database=testdb;User Id=root;Include Error Detail=true;CommandTimeout=360;Enlist=false;No Reset On Close=true;";


    [Test]
    public void querying_to_a_primitive_type()
    {
        var conn = new NpgsqlConnection(ConnStr);

        object? result = conn.QueryValue("SELECT ('Hello, ' || :para) as result;", new { para = "world" });
        
        Assert.That(result, Is.Not.Null);
        Console.WriteLine(result);
        Assert.That(result.ToString(), Is.EqualTo("Hello, world"));
    }

    [Test]
    public void querying_to_a_list_of_classes()
    {
        var conn = new NpgsqlConnection(ConnStr);

        // Create a test table
        conn.QueryValue(@"
CREATE TABLE IF NOT EXISTS TestTable (
    id        int  not null constraint ""primary"" primary key,
    userId    bigint not null,
    deviceId  text
);
");
        
        // Make sure it's empty
        conn.QueryValue("TRUNCATE TABLE TestTable CASCADE;");
        
        // Add some test data
        conn.RepeatCommand("INSERT INTO TestTable (id, userId, deviceId) VALUES (:id, :userId, :deviceId);",
            new {id=1, userId=10, deviceId="User10 Phone"},
            new {id=2, userId=10, deviceId="User10 PC"},
            new {id=3, userId=20, deviceId="User20 Phone"},
            new {id=4, userId=20, deviceId="User20 Modem"}
        );
        
        // Query data back out
        var result = conn.SelectType<SamplePoco>("SELECT * FROM TestTable WHERE userId=:userId", new {userId = 10}).ToList();
            
        Assert.That(result, Is.Not.Null);
        Console.WriteLine(string.Join(", ", result));
        Assert.That(string.Join(", ", result), Is.EqualTo("Id=1; UserId=10; DeviceId='User10 Phone', Id=2; UserId=10; DeviceId='User10 PC'"));
    }

    [Test]
    public void can_give_a_null_value_in_query_parameters()
    {
        var conn = new NpgsqlConnection(ConnStr);
        
        // Create a test table
        conn.QueryValue(@"
CREATE TABLE IF NOT EXISTS TestTable (
    id        int  not null constraint ""primary"" primary key,
    userId    bigint not null,
    deviceId  text
);
");
        // Make sure it's empty
        conn.QueryValue("TRUNCATE TABLE TestTable CASCADE;");
        
        // Add some test data
        conn.RepeatCommand("INSERT INTO TestTable (id, userId, deviceId) VALUES (:id, :userId, :deviceId);",
            new {id=1, userId=10, deviceId=(string?)null},
            new {id=2, userId=10, deviceId="User10 PC"},
            new {id=3, userId=20, deviceId=(string?)null},
            new {id=4, userId=20, deviceId="User20 Modem"}
        );
        
        // Query data back out
        var result = conn.SelectType<SamplePoco>("SELECT * FROM TestTable WHERE userId=:userId", new {userId = 10}).ToList();
            
        Assert.That(result, Is.Not.Null);
        Console.WriteLine(string.Join(", ", result));
        Assert.That(string.Join(", ", result), Is.EqualTo("Id=1; UserId=10; DeviceId='', Id=2; UserId=10; DeviceId='User10 PC'"));
    }
    
    [Test]
    public void can_supply_query_parameters_as_a_string_object_dictionary()
    {
        var conn = new NpgsqlConnection(ConnStr);
        
        // Create a test table
        conn.QueryValue(@"
CREATE TABLE IF NOT EXISTS TestTable (
    id        int  not null constraint ""primary"" primary key,
    userId    bigint not null,
    deviceId  text
);
");
        // Make sure it's empty
        conn.QueryValue("TRUNCATE TABLE TestTable CASCADE;");
        
        // Add some test data
        conn.RepeatCommand("INSERT INTO TestTable (id, userId, deviceId) VALUES (:id, :userId, :deviceId);",
            new Dictionary<string, object?> { { "id", 1 }, { "userId", 10 }, { "deviceId", null } },
            new Dictionary<string, object?> { { "id", 2 }, { "userId", 10 }, { "deviceId", "User10 PC" } },
            new Dictionary<string, object?> { { "id", 3 }, { "userId", 20 }, { "deviceId", null } },
            new Dictionary<string, object?> { { "id", 4 }, { "userId", 20 }, { "deviceId", "User20 Modem" } }
        );
        
        // Query data back out
        var result = conn.SelectType<SamplePoco>("SELECT * FROM TestTable WHERE userId=:userId", new {userId = 10}).ToList();
            
        Assert.That(result, Is.Not.Null);
        Console.WriteLine(string.Join(", ", result));
        Assert.That(string.Join(", ", result), Is.EqualTo("Id=1; UserId=10; DeviceId='', Id=2; UserId=10; DeviceId='User10 PC'"));
    }
    
    [Test]
    public void null_results_are_given_as_dotnet_null_values()
    {
        var conn = new NpgsqlConnection(ConnStr);
        
        // Create a test table
        conn.QueryValue(@"
CREATE TABLE IF NOT EXISTS TestTable (
    id        int  not null constraint ""primary"" primary key,
    userId    bigint not null,
    deviceId  text
);
");
        // Make sure it's empty
        conn.QueryValue("TRUNCATE TABLE TestTable CASCADE;");
        
        // Add some test data
        conn.RepeatCommand("INSERT INTO TestTable (id, userId, deviceId) VALUES (:id, :userId, :deviceId);",
            new {id=1, userId=10, deviceId=(string?)null},
            new {id=2, userId=10, deviceId="User10 PC"},
            new {id=3, userId=20, deviceId=(string?)null},
            new {id=4, userId=20, deviceId="User20 Modem"}
        );
        
        // Query data back out
        var result = conn.SelectType<SamplePoco>("SELECT * FROM TestTable WHERE userId=:userId", new {userId = 10}).ToList();
        
        Assert.That(result.First().DeviceId, Is.Null);
    }

    [Test]
    public void can_use_enums_as_int_parameters_and_query_output_value_integers()
    {
        var conn = new NpgsqlConnection(ConnStr);

        // Create a test table
        conn.QueryValue(@"
CREATE TABLE IF NOT EXISTS EnumTable (
    id        int not null constraint ""primary"" primary key,
    sales     decimal not null,
    location  int,
    landmass  int,
    zone      int
);
");
        
        // Make sure it's empty
        conn.QueryValue("TRUNCATE TABLE EnumTable CASCADE;");
        
        // Add some test data
        conn.RepeatCommand("INSERT INTO EnumTable (id, sales, location, landmass, zone) VALUES (:id, :sales, :location, :landmass, :zone);",
            new {id=1, sales=10.01, location=Geolocation.Cameroon,    landmass=GeoLandmass.SubSahara,    zone=GeoZone.EMEA},
            new {id=2, sales=10.02, location=Geolocation.Philippines, landmass=GeoLandmass.Oceania,      zone=GeoZone.APAC},
            new {id=3, sales=20.03, location=Geolocation.Ukraine,     landmass=GeoLandmass.Europe,       zone=GeoZone.EMEA},
            new {id=4, sales=20.04, location=Geolocation.SriLanka,    landmass=GeoLandmass.Subcontinent, zone=GeoZone.APAC}
        );
        
        // Query data back out
        var result = conn.SelectType<PocoWithEnums>("SELECT * FROM EnumTable WHERE zone = :zone;", new {zone=GeoZone.APAC}).ToList();
            
        Assert.That(result, Is.Not.Null);
        Console.WriteLine(string.Join(", ", result));
        Assert.That(string.Join(", ", result), Is.EqualTo("2: APAC/Oceania/Philippines = 10.02, 4: APAC/Subcontinent/SriLanka = 20.04"));
    }
    
    [Test]
    public void can_use_enums_as_string_parameters_and_query_output_value_strings()
    {
        var conn = new NpgsqlConnection(ConnStr);

        // Create a test table
        conn.QueryValue(@"
CREATE TABLE IF NOT EXISTS EnumStrTable (
    id        int not null constraint ""primary"" primary key,
    sales     decimal not null,
    location  text,
    landmass  text,
    zone      text
);
");
        
        // Make sure it's empty
        conn.QueryValue("TRUNCATE TABLE EnumStrTable CASCADE;");
        
        // Add some test data
        conn.RepeatCommand("INSERT INTO EnumStrTable (id, sales, location, landmass, zone) VALUES (:id, :sales, :location, :landmass, :zone);",
            new {id=1, sales=10.01, location=Geolocation.Cameroon.ToString(),    landmass=GeoLandmass.SubSahara.ToString(),    zone=GeoZone.EMEA.ToString()},
            new {id=2, sales=10.02, location=Geolocation.Philippines.ToString(), landmass=GeoLandmass.Oceania.ToString(),      zone=GeoZone.APAC.ToString()},
            new {id=3, sales=20.03, location=Geolocation.Ukraine.ToString(),     landmass=GeoLandmass.Europe.ToString(),       zone=GeoZone.EMEA.ToString()},
            new {id=4, sales=20.04, location=Geolocation.SriLanka.ToString(),    landmass=GeoLandmass.Subcontinent.ToString(), zone=GeoZone.APAC.ToString()}
        );
        
        // Query data back out
        var result = conn.SelectType<PocoWithEnums>("SELECT * FROM EnumStrTable WHERE zone = :zone;", new {zone=GeoZone.APAC.ToString()}).ToList();
            
        Assert.That(result, Is.Not.Null);
        Console.WriteLine(string.Join(", ", result));
        Assert.That(string.Join(", ", result), Is.EqualTo("2: APAC/Oceania/Philippines = 10.02, 4: APAC/Subcontinent/SriLanka = 20.04"));
    }

    [Test]
    public void can_assign_primitive_values_to_nullable_properties()
    {
        var conn = new NpgsqlConnection(ConnStr);
        
        // Create a test table
        conn.QueryValue(@"
CREATE TABLE IF NOT EXISTS PrimitivesValues (
    nLong bigint,
    nInt  int,
    nEnum int
);
");
        // Make sure it's empty
        conn.QueryValue("TRUNCATE TABLE PrimitivesValues CASCADE;");
        
        // Add some test data
        conn.RepeatCommand("INSERT INTO PrimitivesValues (nLong, nInt, nEnum) VALUES (:nLong, :nInt, :nEnum);",
            new { nLong = 123L, nInt = 456, nEnum = GeoZone.EMEA }, // with all populated
            new { nLong = (long?)null, nInt = 456, nEnum = GeoZone.EMEA }, // a primitive missing
            new { nLong = 123L, nInt = 456, nEnum = (GeoZone?)null } // an enum missing
        );
        
        // Query data back out
        var result = conn.SelectType<NullablePrimitives>("SELECT * FROM PrimitivesValues;", null).ToList();
            
        Assert.That(result, Is.Not.Null);
        Console.WriteLine(string.Join(", ", result));
        Assert.That(string.Join(", ", result), Is.EqualTo("NLong=123; NInt=456; NEnum='EMEA', NLong=<null>; NInt=456; NEnum='EMEA', NLong=123; NInt=456; NEnum='<null>'"));
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


public enum GeoZone : long
{
    // ReSharper disable InconsistentNaming
    APAC, EMEA, AMER
    // ReSharper restore InconsistentNaming
}

public enum GeoLandmass: long
{
    Europe, WestAsia, EastAsia, Subcontinent, NorthAfrica, SubSahara,
    Oceania, NorthAmerica, CentralAmerica, SouthAmerica, Other
}

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