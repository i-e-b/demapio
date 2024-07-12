using Demapio;
using Npgsql;
using NUnit.Framework;
// ReSharper disable AssignNullToNotNullAttribute
#pragma warning disable CS8629 // Nullable value type may be null.
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
        
        // Select directly into an enum
        var enumResult = conn.SelectType<Geolocation>("SELECT location FROM EnumTable WHERE id = 2;").ToList();
        Assert.That(enumResult[0], Is.EqualTo(Geolocation.Philippines), "Direct enum result");
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
        
        // Select directly into an enum
        var enumResult = conn.SelectType<Geolocation>("SELECT location FROM EnumTable WHERE id = 2;").ToList();
        Assert.That(enumResult[0], Is.EqualTo(Geolocation.Philippines), "Direct enum result");
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
        var result = conn.SelectType<NullablePrimitives>("SELECT * FROM PrimitivesValues;").ToList();
            
        Assert.That(result, Is.Not.Null);
        Console.WriteLine(string.Join(", ", result));
        Assert.That(string.Join(", ", result), Is.EqualTo("NLong=123; NInt=456; NEnum='EMEA', NLong=<null>; NInt=456; NEnum='EMEA', NLong=123; NInt=456; NEnum='<null>'"));
    }

    [Test]
    public void can_write_and_query_byte_array_types()
    {
        var conn = new NpgsqlConnection(ConnStr);
        
        // Create a test table
        conn.QueryValue(@"
CREATE TABLE IF NOT EXISTS ByteArrayValues (
    id   int,
    data bytea
);
");
        // Make sure it's empty
        conn.QueryValue("TRUNCATE TABLE ByteArrayValues CASCADE;");
        
        // Add some test data
        conn.RepeatCommand("INSERT INTO ByteArrayValues (id, data) VALUES (:id, :data);",
            
            new { id = 1, data= new byte[]{1,2,3,4,5}}, // byte array as input
            new { id = 1, data= new List<byte>{6,7,8,9,10}}, // byte list as input
            new { id = 1, data= (IEnumerable<byte>)(new byte[]{1,2,3,4,5})}  // byte enumerable as input
        );
        
        // Query data back out as array
        var result1 = conn.SelectType<ByteArrayValue>("SELECT * FROM ByteArrayValues;").ToList();
            
        Assert.That(result1, Is.Not.Null);
        Console.WriteLine(string.Join(", ", result1));
        Assert.That(string.Join(", ", result1), Is.EqualTo("ID=1; Data='0102030405', ID=1; Data='060708090A', ID=1; Data='0102030405'"));
        
        
        // Query data back out as list
        var result2 = conn.SelectType<ByteListValue>("SELECT * FROM ByteArrayValues;").ToList();
            
        Assert.That(result2, Is.Not.Null);
        Console.WriteLine(string.Join(", ", result2));
        Assert.That(string.Join(", ", result2), Is.EqualTo("ID=1; Data='0102030405', ID=1; Data='060708090A', ID=1; Data='0102030405'"));
        
        
        // Query data back out as an IEnumerable interface
        var result3 = conn.SelectType<ByteEnumerableValue>("SELECT * FROM ByteArrayValues;").ToList();
            
        Assert.That(result3, Is.Not.Null);
        Console.WriteLine(string.Join(", ", result3));
        Assert.That(string.Join(", ", result3), Is.EqualTo("ID=1; Data='0102030405', ID=1; Data='060708090A', ID=1; Data='0102030405'"));
        
        
        // Query data back using a data reader
        using var result4 = conn.QueryReader("SELECT * FROM ByteArrayValues;");
            
        Assert.That(result4, Is.Not.Null);
        Assert.That(result4.Read(), Is.True);
    }
    
    [Test]
    public void run_a_query_and_get_count_of_rows_affected()
    {
        var conn = new NpgsqlConnection(ConnStr);
        
        // Create a test table
        conn.QueryValue(@"
CREATE TABLE IF NOT EXISTS ByteArrayValues (
    id   int,
    data bytea
);
");
        // Make sure it's empty
        conn.QueryValue("TRUNCATE TABLE ByteArrayValues CASCADE;");
        
        // Add some test data
        conn.RepeatCommand("INSERT INTO ByteArrayValues (id, data) VALUES (:id, :data);",
            new { id = 1, data= new byte[]{1,2,3,4,5}}, // byte array as input
            new { id = 2, data= new List<byte>{6,7,8,9,10}}, // byte list as input
            new { id = 3, data= (IEnumerable<byte>)(new byte[]{1,2,3,4,5})}  // byte enumerable as input
        );
        
        // Update rows, get change count
        var count = conn.CountCommand("UPDATE ByteArrayValues SET id = 5 WHERE id < 3;");
            
        Assert.That(count, Is.EqualTo(2));
    }

    [Test]
    public void selecting_type_will_cast_between_integer_types()
    {
        var conn = new NpgsqlConnection(ConnStr);
        
        // Create a test table
        conn.QueryValue(@"
CREATE TABLE IF NOT EXISTS TestTable (
    id        bigint  not null constraint ""primary"" primary key,
    userId    int not null,
    deviceId  text
);
");
        // Make sure it's empty
        conn.QueryValue("TRUNCATE TABLE TestTable CASCADE;");
        
        // Add some test data
        conn.RepeatCommand("INSERT INTO TestTable (id, userId, deviceId) VALUES (:id, :userId, :deviceId);",
            new {id=1L, userId=1, deviceId="s,s"},
            new {id=2UL, userId=2, deviceId="u,s"},
            new {id=3L, userId=3U, deviceId="s,u"},
            new {id=4UL, userId=4U, deviceId="u,u"}
        );
        
        // Query with different types
        
        // int->int
        var result1 = conn.SelectType<int>("SELECT userId FROM TestTable ORDER BY userId;").ToList();
        Assert.That(result1, Is.EqualTo(new[]{1,2,3,4}).AsCollection, "Casting int to int");
        
        // int->uint
        var result2 = conn.SelectType<uint>("SELECT userId FROM TestTable ORDER BY userId;").ToList();
        Assert.That(result2, Is.EqualTo(new uint[]{1,2,3,4}).AsCollection, "Casting int to uint");
        
        // int->long
        var result3 = conn.SelectType<long>("SELECT userId FROM TestTable ORDER BY userId;").ToList();
        Assert.That(result3, Is.EqualTo(new long[]{1,2,3,4}).AsCollection, "Casting int to long");
        
        // int->ulong
        var result4 = conn.SelectType<ulong>("SELECT userId FROM TestTable ORDER BY userId;").ToList();
        Assert.That(result4, Is.EqualTo(new ulong[]{1,2,3,4}).AsCollection, "Casting int to ulong");
        
        
        // long->int (may truncate)
        var result5 = conn.SelectType<int>("SELECT id FROM TestTable ORDER BY id;").ToList();
        Assert.That(result5, Is.EqualTo(new[]{1,2,3,4}).AsCollection, "Casting long to int");
        
        // long->uint (may truncate)
        var result6 = conn.SelectType<uint>("SELECT id FROM TestTable ORDER BY id;").ToList();
        Assert.That(result6, Is.EqualTo(new uint[]{1,2,3,4}).AsCollection, "Casting long to uint");
        
        // long->long
        var result7 = conn.SelectType<long>("SELECT id FROM TestTable ORDER BY id;").ToList();
        Assert.That(result7, Is.EqualTo(new long[]{1,2,3,4}).AsCollection, "Casting long to long");
        
        // long->ulong
        var result8 = conn.SelectType<ulong>("SELECT id FROM TestTable ORDER BY id;").ToList();
        Assert.That(result8, Is.EqualTo(new ulong[]{1,2,3,4}).AsCollection, "Casting long to ulong");
    }

    [Test]
    public void dates_are_taken_as_unspecified_time_zone()
    {
        var conn = new NpgsqlConnection(ConnStr);
        
        var original = new DateTimeValues
        {
            NullableDateOne = null,
            NullableDateTwo = new DateTime(2024, 6,5,4,3,2, DateTimeKind.Local),
            DateOne = new DateTime(2024, 6,5,4,3,2, DateTimeKind.Unspecified),
            DateTwo = new DateTime(2024, 6,5,4,3,2, DateTimeKind.Utc),
            DateThree = new DateTime(2024, 6,5,4,3,2, DateTimeKind.Local),
        };
        
        var result = conn.SelectType<DateTimeValues>(
            "SELECT :NullableDateOne::timestamp as NullableDateOne, :NullableDateTwo as NullableDateTwo, :DateOne as DateOne, :DateTwo as DateTwo, :DateThree::TimestampTz as DateThree;",
            original).FirstOrDefault();
        
        Assert.That(result, Is.Not.Null);
        
        Assert.That(result.NullableDateOne, Is.Null);
        Assert.That(result.NullableDateTwo.Value.ToString("yyyy-MM-dd HH:mm:ss"), Is.EqualTo("2024-06-05 04:03:02"));
        Assert.That(result.DateOne.ToString("yyyy-MM-dd HH:mm:ss"), Is.EqualTo("2024-06-05 04:03:02"));
        Assert.That(result.DateTwo.ToString("yyyy-MM-dd HH:mm:ss"), Is.EqualTo("2024-06-05 04:03:02"));
        Assert.That(result.DateThree.ToString("yyyy-MM-dd HH:mm:ss"), Is.EqualTo("2024-06-05 04:03:02"));
    }

    [Test]
    public void can_set_nullable_uuid_to_null_with_nullable_wrapped_value()
    {
        var conn = new NpgsqlConnection(ConnStr);
        
        conn.CountCommand(@"
create table if not exists UuidTester
(
    Id              int not null,
    GuidCol    uuid default null
);
truncate table UuidTester;
");
        
        conn.RepeatCommand("INSERT INTO UuidTester (Id, GuidCol) VALUES (:id, :guidVal);",
            new {id= 1, guidVal = Guid.NewGuid()},
            new {id= 2, guidVal = Guid.Empty},
            new {id= 3, guidVal = (Guid?)null});
        
        
        var initial = conn.SelectType<GuidTestType>("SELECT * FROM UuidTester;").ToList();
        Assert.That(initial[0].GuidCol, Is.Not.Null);
        Assert.That(initial[1].GuidCol, Is.Not.Null);
        Assert.That(initial[2].GuidCol, Is.Null);
        
        conn.RepeatCommand("UPDATE UuidTester SET GuidCol = :guidVal WHERE Id = :id;",
                new {id= 1, guidVal = (Guid?)null},
                new {id= 2, guidVal = (Guid?)null},
                new {id= 3, guidVal = (Guid?)null}
            );
        
        var after = conn.SelectType<GuidTestType>("SELECT * FROM UuidTester;").ToList();
        
        Assert.That(after[0].GuidCol, Is.Null);
        Assert.That(after[1].GuidCol, Is.Null);
        Assert.That(after[2].GuidCol, Is.Null);
    }

    [Test]
    public void can_write_uuid_into_string_property()
    {
        var conn = new NpgsqlConnection(ConnStr);
        
        conn.CountCommand(@"
create table if not exists UuidTester
(
    Id              int not null,
    GuidCol    uuid default null
);
truncate table UuidTester;
");
        conn.RepeatCommand("INSERT INTO UuidTester (Id, GuidCol) VALUES (:id, :guidVal);",
            new {id= 1, guidVal = Guid.Parse("94C8B00E-43E7-457E-9359-34C835D66692")},
            new {id= 2, guidVal = Guid.Empty},
            new {id= 3, guidVal = (Guid?)null});
        
        var result = conn.SelectType<GuidAsStringTestType>("SELECT * FROM UuidTester ORDER BY id;").ToList();

        Assert.That(result[0].GuidCol, Is.EqualTo("94c8b00e-43e7-457e-9359-34c835d66692")); // Guid.ToString() gives lower case.
        Assert.That(result[1].GuidCol, Is.EqualTo("00000000-0000-0000-0000-000000000000"));
        Assert.That(result[2].GuidCol, Is.Null);
    }

    [Test]
    public void can_read_arbitrary_data_into_dynamic_result()
    {
        var conn = new NpgsqlConnection(ConnStr);
        
        conn.CountCommand(@"
create table if not exists DynamicTests
(
    Id              int not null,
    StringCol       text default null,
    IntCol          int default null,
    ByteCol         bytea not null
);
truncate table DynamicTests;
");
        
        conn.RepeatCommand("INSERT INTO DynamicTests (Id, StringCol, IntCol, ByteCol) VALUES (:id, :stringCol, :intCol, :byteCol);",
            new {id= 1, stringCol = "one", intCol = 100, byteCol=new byte[]{1,2,3}},
            new {id= 2, stringCol = (string?)null, intCol = 200, byteCol=new byte[]{4,5,6}},
            new {id= 3, stringCol = "three", intCol = (int?)null, byteCol=new byte[]{7,8,9}}
        );
        
        var result = conn.SelectDynamic("SELECT * FROM DynamicTests;").ToList();
        
        Assert.That(result.Count, Is.EqualTo(3));
        Assert.That(result[0].id, Is.EqualTo(1), "should get value");
        Assert.That(result[0].ID, Is.EqualTo(1), "values should be case insensitive");
        
        Assert.That(result[0].stringCol, Is.EqualTo("one"));
        Assert.That(result[1].StringCol, Is.Null);
        Assert.That(result[2].stringcol, Is.EqualTo("three"));
        
        Assert.That(result[0].intCol, Is.EqualTo(100));
        Assert.That(result[1].IntCol, Is.EqualTo(200));
        Assert.That(result[2].intcol, Is.Null);
        
        Assert.That(result[0].byteCol, Is.EqualTo(new byte[]{1,2,3}).AsCollection);
        Assert.That(result[1].ByteCol, Is.EqualTo(new byte[]{4,5,6}).AsCollection);
        Assert.That(result[2].bytecol, Is.EqualTo(new byte[]{7,8,9}).AsCollection);
    }
}

// ReSharper disable InconsistentNaming
// ReSharper disable UnusedMember.Global
// ReSharper disable PropertyCanBeMadeInitOnly.Global

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


public enum GeoZone : long
{
    APAC, EMEA, AMER
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