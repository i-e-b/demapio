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

        var result = conn.QueryValue("SELECT ('Hello, ' || :para) as result;", new { para = "world" });
        
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
        var result = conn.SelectType<SamplePoco>("SELECT * FROM TestTable WHERE userId=:userId", new {userId = 10}).ToList()!;
            
        Assert.That(result, Is.Not.Null);
        Console.WriteLine(string.Join(", ", result));
        Assert.That(string.Join(", ", result), Is.EqualTo("Id=0; UserId=10; DeviceId='User10 Phone', Id=0; UserId=10; DeviceId='User10 PC'"));
    }
}

public class SamplePoco
{
    public int Id { get; set; }
    public long UserId { get; set; }
    public string DeviceId { get; set; }="";

    public override string ToString()
    {
        return $"Id={Id}; UserId={UserId}; DeviceId='{DeviceId}'";
    }
}