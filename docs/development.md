# Development

## Prerequisites

- Node.js 22 or newer and npm
- .NET 10 SDK
- Docker Engine with Docker Compose v2 for the containerized setup

## Local API database

The API uses SQLite and applies committed EF Core migrations at startup. The
default connection string is `Data Source=storykeeper.db`; the database file
is ignored by Git. Override it with `ConnectionStrings__Storykeeper`, for
example `Data Source=C:\data\storykeeper.db` on Windows or
`Data Source=/data/storykeeper.db` on Linux.

Docker Compose uses the named `storykeeper-data` volume mounted at `/data`,
which keeps the database when the API container is recreated. Use
`docker compose down -v` only when intentionally deleting that local data.

## Tests and migrations

Run backend tests with:

```sh
dotnet test Storykeeper.slnx
```

EF Core tools are used to create schema migrations. The `dotnet-ef` tool must
match the EF Core major version. To add a migration after changing the model:

```sh
dotnet ef migrations add <MigrationName> --project src/Storykeeper.Api/Storykeeper.Api.csproj --startup-project src/Storykeeper.Api/Storykeeper.Api.csproj --output-dir Data/Migrations
```

Commit generated migration files with the model change. Do not commit local
SQLite databases.
