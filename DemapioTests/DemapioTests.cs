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

        var result = conn.SimpleSelect("SELECT ('Hello, ' || :para) as result;", new { para = "world" });
        
        Assert.That(result, Is.Not.Null);
        Assert.That(result.ToString(), Is.EqualTo("Hello, world"));
    }
    

    [Test]
    public void stub()
    {
        Assert.Fail("Tests not yet created");
    }
}