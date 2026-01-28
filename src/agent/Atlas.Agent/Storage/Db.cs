using Microsoft.Data.Sqlite;

namespace Atlas.Agent.Storage;

public sealed class Db : IAsyncDisposable
{
    private readonly SqliteConnection _conn;

    public Db(string dbPath)
    {
        var cs = new SqliteConnectionStringBuilder { DataSource = dbPath }.ToString();
        _conn = new SqliteConnection(cs);
    }

    public async Task OpenAsync() => await _conn.OpenAsync();

    public SqliteConnection Conn => _conn;

    public async ValueTask DisposeAsync()
    {
        await _conn.DisposeAsync();
    }
}
