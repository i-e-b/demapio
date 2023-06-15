Demapio
=======

Small SQL mapper for C#

### What?

It's like [Dapper](https://github.com/DapperLib/Dapper), but smaller and more focussed.

### Why not just use Dapper?

If that works, do it. I keep having trouble with new versions of Dapper being 'smart' and re-writing my working SQL into broken SQL.

Demapio does a lot less, and aims to be small and stable rather than big and clever.
No guarantees on speed.

### How?

Select a single value:

```
var conn = new NpgsqlConnection(ConnStr);

object? result = conn.QueryValue("SELECT ('Hello, ' || :para) as result;", new { para = "world" });
```

Query data to a list of C# objects:

```
var conn = new NpgsqlConnection(ConnStr);

var result = conn.SelectType<SamplePoco>("SELECT * FROM TestTable WHERE userId=:userId", new {userId = 10}).ToList()!;
```

Repeat a single statement with a batch of parameters:

```
var conn = new NpgsqlConnection(ConnStr);

conn.RepeatCommand("INSERT INTO TestTable (id, userId, deviceId) VALUES (:id, :userId, :deviceId);",
    new {id=1, userId=10, deviceId="User10 Phone"},
    new {id=2, userId=10, deviceId="User10 PC"},
    new {id=3, userId=20, deviceId="User20 Phone"},
    new {id=4, userId=20, deviceId="User20 Modem"}
);
```