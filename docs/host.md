# TrueRAG Host

The single executable composition root for TrueRAG.

Responsibilities:

- load and own `appsettings*.json`
- configure host-level dependency injection
- wire middleware, routing, and hosting
- register module libraries such as API, Workers, Retrieval, Ingestion, Storage, and Conversations
- serve as the only deployable binary for the modular monolith

## Connection Strings

`TrueRag.Host` owns runtime connection-string configuration under `ConnectionStrings`.

The current host expects:

- `DbWrite`: write-side data source (ingestion path)
- `DbRead`: read-side data source (retrieval path)

Both CrateDB and PostgreSQL use Npgsql-compatible connection string format.

Example CrateDB:

```json
{
  "ConnectionStrings": {
    "DbWrite": "Host=crate-write.local;Port=5432;Database=truerag_write;Username=truerag;Password=secret",
    "DbRead": "Host=crate-read.local;Port=5432;Database=truerag_read;Username=truerag;Password=secret"
  }
}
```

Example PostgreSQL:

```json
{
  "ConnectionStrings": {
    "DbWrite": "Host=pg-write.local;Port=5432;Database=truerag_write;Username=truerag;Password=secret",
    "DbRead": "Host=pg-read.local;Port=5432;Database=truerag_read;Username=truerag;Password=secret"
  }
}
```
