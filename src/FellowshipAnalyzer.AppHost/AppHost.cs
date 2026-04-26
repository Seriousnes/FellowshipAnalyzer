var builder = DistributedApplication.CreateBuilder(args);

var api = builder.AddAzureFunctionsProject<Projects.FellowshipAnalyzer_Api>("fellowshipanalyzerapi")
    .WithEndpoint("http", e =>
    {
        e.Port = 5123;
        e.TargetPort = 5123;
        e.TargetHost = "localhost";
        e.UriScheme = "http";
        e.IsProxied = false;
    });

builder.AddProject<Projects.FellowshipAnalyzer>("fellowshipanalyzer")
    .WithReference(api)
    .WithEndpoint("http", e =>
    {
        e.Port = 5120;
        e.TargetPort = 5120;
        e.TargetHost = "fellowshipanalyzer.dev.localhost";
        e.UriScheme = "http";
        e.IsProxied = false;
    });

builder.Build().Run();
