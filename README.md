# Forklift

Lifts all your dev services (based on [aspire.dev](https://aspire.dev/))

## Services

| Service | Image | Port(s) | UI |
|---------|-------|---------|-----|
| PostgreSQL | (Aspire default) | 5432 | pgAdmin |
| MongoDB | (Aspire default) | 27017 | Mongo Express |
| Redis | redis:7.0 | 6379 | Redis Insight |
| RabbitMQ | rabbitmq:3.12-management | 5672, 15672 | Management UI |
| EventStoreDB | eventstore/eventstore:21.10.5-buster-slim | 1113, 2113 | Built-in UI |
| Unleash | unleashorg/unleash-server:6.5.3 | 4242 | Built-in UI |

## Installation

### Prerequisites

- .NET 10 SDK
- Docker Desktop
- Aspire orchestration packages (see below)

### Install Aspire Packages

Forklift requires Aspire orchestration packages in your NuGet cache. Run these commands once:

```bash
cd /tmp
mkdir aspire-setup && cd aspire-setup
dotnet new console

# For macOS ARM (M1/M2/M3)
dotnet add package Aspire.Hosting.Orchestration.osx-arm64 --version 13.1.0
dotnet add package Aspire.Dashboard.Sdk.osx-arm64 --version 13.1.0

# For macOS Intel
# dotnet add package Aspire.Hosting.Orchestration.osx-x64 --version 13.1.0
# dotnet add package Aspire.Dashboard.Sdk.osx-x64 --version 13.1.0

# For Linux x64
# dotnet add package Aspire.Hosting.Orchestration.linux-x64 --version 13.1.0
# dotnet add package Aspire.Dashboard.Sdk.linux-x64 --version 13.1.0

# For Windows x64
# dotnet add package Aspire.Hosting.Orchestration.win-x64 --version 13.1.0
# dotnet add package Aspire.Dashboard.Sdk.win-x64 --version 13.1.0

cd ~
rm -rf /tmp/aspire-setup
```

### Install as .NET Tool

1. Authenticate with GitHub Packages (one-time setup):

```bash
dotnet nuget add source "https://nuget.pkg.github.com/bondarewicz/index.json" \
  --name "github-bondarewicz" \
  --username YOUR_GITHUB_USERNAME \
  --password YOUR_GITHUB_PAT
```

2. Install the tool:

```bash
dotnet tool install -g Forklift --source github-bondarewicz
```

3. Run:

```bash
forklift
```

### Update

```bash
dotnet tool update -g Forklift --source github-bondarewicz
```

### From Source

```bash
git clone https://github.com/bondarewicz/forklift.git
cd forklift
dotnet run --project src
```

The Aspire Dashboard will open automatically showing all services status, logs, and traces.

## Configuration

All configuration is externalized to `appsettings.json`. No hardcoded values in code.

### Container Images

Configure which container images to use:

```json
{
  "Images": {
    "redis": "redis",
    "redis-tag": "7.0",
    "rabbitmq": "rabbitmq",
    "rabbitmq-tag": "3.12-management",
    "eventstore": "eventstore/eventstore",
    "eventstore-tag": "21.10.5-buster-slim",
    "unleash": "unleashorg/unleash-server",
    "unleash-tag": "6.5.3"
  }
}
```

### Service Parameters

Configure service credentials and settings:

```json
{
  "Parameters": {
    "postgres-password": "changeit",
    "postgres-db": "postgres_dev",
    "mongo-password": "your-password",
    "rabbitmq-user": "pvRetailDev",
    "rabbitmq-password": "pvRetailDev",
    "rabbitmq-vhost": "ParcelVision.Retail",
    "eventstore-run-projections": "All",
    "eventstore-start-standard-projections": "true",
    "eventstore-insecure": "true",
    "eventstore-enable-atom-pub": "true",
    "eventstore-cluster-size": "1",
    "eventstore-mem-db": "false",
    "unleash-db-name": "postgres-dev",
    "unleash-db-username": "postgres",
    "unleash-db-ssl": "false",
    "unleash-client-api-tokens": "*:development.test",
    "unleash-admin-api-tokens": "*:*.test"
  }
}
```

### Using User Secrets (Recommended)

For sensitive values, use User Secrets instead of appsettings:

```bash
cd src
dotnet user-secrets init
dotnet user-secrets set "Parameters:postgres-password" "your-secure-password"
dotnet user-secrets set "Parameters:mongo-password" "your-secure-password"
dotnet user-secrets set "Parameters:rabbitmq-password" "your-secure-password"
```

## Data Persistence

All services use named Docker volumes for data persistence:

| Service | Volume |
|---------|--------|
| PostgreSQL | forklift-postgres-data |
| MongoDB | forklift-mongo-data |
| Redis | forklift-redis-data |
| RabbitMQ | forklift-rabbitmq-data |
| EventStoreDB | forklift-eventstore-data |

Data persists across container restarts. To reset, remove the volumes:

```bash
docker volume rm forklift-postgres-data forklift-mongo-data forklift-redis-data forklift-rabbitmq-data forklift-eventstore-data
```

## Service URLs

When running, access the service UIs at:

| Service | URL |
|---------|-----|
| Aspire Dashboard | https://localhost:17241 |
| pgAdmin | http://localhost:5050 |
| Mongo Express | http://localhost:8081 |
| Redis Insight | http://localhost:5540 |
| RabbitMQ Management | http://localhost:15672 |
| EventStoreDB | http://localhost:2113 |
| Unleash | http://localhost:4242 |

Note: Ports may vary. Check the Aspire Dashboard for actual endpoints.

## Development

### Conventional Commits

This project uses [Conventional Commits](https://www.conventionalcommits.org/) for semantic versioning:

```bash
feat: add new service      # → minor bump (0.1.0)
fix: resolve connection    # → patch bump (0.0.1)
feat!: redesign config     # → major bump (1.0.0)
docs: update readme        # → no version bump
chore: update deps         # → no version bump
```

### Running Tests

```bash
dotnet test
```

### Running GitHub Actions Locally

Install [act](https://github.com/nektos/act):

```bash
brew install act
```

Run workflows:

```bash
# Run CI (pull request) workflow
act pull_request

# Run publish workflow
act push

# List available workflows
act -l
```
