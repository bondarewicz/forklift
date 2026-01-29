using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Configuration;

// =============================================================================
// FORKLIFT -  Lifts all your dev services.
// =============================================================================

// Allow unsecured transport by default for local development
Environment.SetEnvironmentVariable("ASPIRE_ALLOW_UNSECURED_TRANSPORT", "true");

// Set default dashboard URLs if not already set (for dotnet tool support)
Environment.SetEnvironmentVariable("ASPNETCORE_URLS",
    Environment.GetEnvironmentVariable("ASPNETCORE_URLS") ?? "http://localhost:15888");
Environment.SetEnvironmentVariable("ASPIRE_DASHBOARD_OTLP_HTTP_ENDPOINT_URL",
    Environment.GetEnvironmentVariable("ASPIRE_DASHBOARD_OTLP_HTTP_ENDPOINT_URL") ?? "http://localhost:18889");

// Detect and configure DCP paths at runtime for dotnet tool support
var rid = GetRuntimeIdentifier();
var nugetCache = GetNuGetCacheDirectory();
const string aspireVersion = "13.1.0";

var dcpPackagePath = Path.Combine(nugetCache, $"aspire.hosting.orchestration.{rid}", aspireVersion, "tools");
var dashboardPackagePath = Path.Combine(nugetCache, $"aspire.dashboard.sdk.{rid}", aspireVersion, "tools");

// Check if packages exist, if not instruct user to install
if (!Directory.Exists(dcpPackagePath) || !Directory.Exists(dashboardPackagePath))
{
    Console.Error.WriteLine($"Aspire orchestration packages not found for {rid}.");
    Console.Error.WriteLine("Please run the following commands to install required packages:");
    Console.Error.WriteLine($"  dotnet add package Aspire.Hosting.Orchestration.{rid} --version {aspireVersion}");
    Console.Error.WriteLine($"  dotnet add package Aspire.Dashboard.Sdk.{rid} --version {aspireVersion}");
    Console.Error.WriteLine("Or install the Aspire workload:");
    Console.Error.WriteLine("  dotnet workload install aspire");
    Environment.Exit(1);
}

// Get the directory where the executable is located (for dotnet tool support)
var exeDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;
var appSettingsPath = Path.Combine(exeDir, "appsettings.json");

var builder = DistributedApplication.CreateBuilder(args);

// Configure DCP paths to use user's local NuGet cache
builder.Configuration["DcpPublisher:CliPath"] = Path.Combine(dcpPackagePath, "dcp");
builder.Configuration["DcpPublisher:ExtensionsPath"] = Path.Combine(dcpPackagePath, "ext");
builder.Configuration["DcpPublisher:BinPath"] = Path.Combine(dcpPackagePath, "ext", "bin");
builder.Configuration["DcpPublisher:DashboardPath"] = Path.Combine(dashboardPackagePath, "Aspire.Dashboard.dll");

// Load configuration from the tool's installation directory
builder.Configuration.AddJsonFile(appSettingsPath, optional: false, reloadOnChange: false);

static string GetRuntimeIdentifier()
{
    var arch = RuntimeInformation.OSArchitecture switch
    {
        Architecture.X64 => "x64",
        Architecture.Arm64 => "arm64",
        _ => "x64"
    };

    if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        return $"osx-{arch}";
    if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        return $"linux-{arch}";
    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        return $"win-{arch}";

    return $"linux-{arch}";
}

static string GetNuGetCacheDirectory()
{
    // Check NUGET_PACKAGES environment variable first
    var nugetPackages = Environment.GetEnvironmentVariable("NUGET_PACKAGES");
    if (!string.IsNullOrEmpty(nugetPackages) && Directory.Exists(nugetPackages))
        return nugetPackages;

    // Default locations
    var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    return Path.Combine(home, ".nuget", "packages");
}

// postgresql
var postgresPassword = builder.AddParameter("postgres-password", secret: true);
var postgresDb = builder.AddParameter("postgres-db");
var postgres = builder
    .AddPostgres("postgres", password: postgresPassword)
    .WithDataVolume("forklift-postgres-data")
    .WithEnvironment(ctx => ctx.EnvironmentVariables["POSTGRES_DB"] = postgresDb)
    .WithPgAdmin();

postgres.AddDatabase("postgres-dev");

// mongodb
builder
    .AddMongoDB("mongo", password: builder.AddParameter("mongo-password", secret: true))
    .WithDataVolume("forklift-mongo-data")
    .WithMongoExpress();

// redis
var redisImage = builder.Configuration.GetValue<string>("Images:redis")!;
var redisTag = builder.Configuration.GetValue<string>("Images:redis-tag")!;
#pragma warning disable ASPIRECERTIFICATES001
builder
    .AddRedis("redis")
    .WithImage(redisImage, redisTag)
    .WithDataVolume("forklift-redis-data")
    .WithRedisInsight()
    .WithoutHttpsCertificate();
#pragma warning restore ASPIRECERTIFICATES001

// rabbitmq
var rabbitmqImage = builder.Configuration.GetValue<string>("Images:rabbitmq")!;
var rabbitmqTag = builder.Configuration.GetValue<string>("Images:rabbitmq-tag")!;
var rabbitmqUser = builder.AddParameter("rabbitmq-user");
var rabbitmqPassword = builder.AddParameter("rabbitmq-password", secret: true);
var rabbitmqVhost = builder.AddParameter("rabbitmq-vhost");
builder
    .AddContainer("rabbitmq", rabbitmqImage, rabbitmqTag)
    .WithVolume("forklift-rabbitmq-data", "/var/lib/rabbitmq")
    .WithEnvironment(ctx => ctx.EnvironmentVariables["RABBITMQ_DEFAULT_USER"] = rabbitmqUser)
    .WithEnvironment(ctx => ctx.EnvironmentVariables["RABBITMQ_DEFAULT_PASS"] = rabbitmqPassword)
    .WithEnvironment(ctx => ctx.EnvironmentVariables["RABBITMQ_DEFAULT_VHOST"] = rabbitmqVhost)
    .WithEndpoint(port: 5672, targetPort: 5672, name: "amqp")
    .WithHttpEndpoint(port: 15672, targetPort: 15672, name: "management")
    .WithLifetime(ContainerLifetime.Persistent);

// eventstore/kurrent
var eventstoreImage = builder.Configuration.GetValue<string>("Images:eventstore")!;
var eventstoreTag = builder.Configuration.GetValue<string>("Images:eventstore-tag")!;
var eventstoreRunProjections = builder.AddParameter("eventstore-run-projections");
var eventstoreStartStandardProjections = builder.AddParameter(
    "eventstore-start-standard-projections"
);
var eventstoreInsecure = builder.AddParameter("eventstore-insecure");
var eventstoreEnableAtomPub = builder.AddParameter("eventstore-enable-atom-pub");
var eventstoreClusterSize = builder.AddParameter("eventstore-cluster-size");
var eventstoreMemDb = builder.AddParameter("eventstore-mem-db");
builder
    .AddContainer("eventstore", eventstoreImage, eventstoreTag)
    .WithVolume("forklift-eventstore-data", "/var/lib/eventstore")
    .WithEnvironment(ctx =>
        ctx.EnvironmentVariables["EVENTSTORE_RUN_PROJECTIONS"] = eventstoreRunProjections
    )
    .WithEnvironment(ctx =>
        ctx.EnvironmentVariables["EVENTSTORE_START_STANDARD_PROJECTIONS"] =
            eventstoreStartStandardProjections
    )
    .WithEnvironment(ctx => ctx.EnvironmentVariables["EVENTSTORE_INSECURE"] = eventstoreInsecure)
    .WithEnvironment(ctx =>
        ctx.EnvironmentVariables["EVENTSTORE_ENABLE_ATOM_PUB_OVER_HTTP"] = eventstoreEnableAtomPub
    )
    .WithEnvironment(ctx =>
        ctx.EnvironmentVariables["EVENTSTORE_CLUSTER_SIZE"] = eventstoreClusterSize
    )
    .WithEnvironment(ctx => ctx.EnvironmentVariables["EVENTSTORE_MEM_DB"] = eventstoreMemDb)
    .WithHttpEndpoint(port: 2113, targetPort: 2113, name: "http")
    .WithEndpoint(port: 1113, targetPort: 1113, name: "tcp")
    .WithLifetime(ContainerLifetime.Persistent);

// unleash
var unleashImage = builder.Configuration.GetValue<string>("Images:unleash")!;
var unleashTag = builder.Configuration.GetValue<string>("Images:unleash-tag")!;
var unleashDbName = builder.AddParameter("unleash-db-name");
var unleashDbUsername = builder.AddParameter("unleash-db-username");
var unleashDbSsl = builder.AddParameter("unleash-db-ssl");
var unleashClientApiTokens = builder.AddParameter("unleash-client-api-tokens");
var unleashAdminApiTokens = builder.AddParameter("unleash-admin-api-tokens");
builder
    .AddContainer("unleash", unleashImage, unleashTag)
    .WithEnvironment("DATABASE_HOST", postgres.Resource.Name)
    .WithEnvironment(ctx => ctx.EnvironmentVariables["DATABASE_NAME"] = unleashDbName)
    .WithEnvironment(ctx => ctx.EnvironmentVariables["DATABASE_USERNAME"] = unleashDbUsername)
    .WithEnvironment(ctx => ctx.EnvironmentVariables["DATABASE_PASSWORD"] = postgresPassword)
    .WithEnvironment(ctx => ctx.EnvironmentVariables["DATABASE_SSL"] = unleashDbSsl)
    .WithEnvironment(ctx =>
        ctx.EnvironmentVariables["INIT_CLIENT_API_TOKENS"] = unleashClientApiTokens
    )
    .WithEnvironment(ctx =>
        ctx.EnvironmentVariables["INIT_ADMIN_API_TOKENS"] = unleashAdminApiTokens
    )
    .WithHttpEndpoint(port: 4242, targetPort: 4242, name: "http")
    .WithLifetime(ContainerLifetime.Persistent)
    .WaitFor(postgres);

builder.Build().Run();
