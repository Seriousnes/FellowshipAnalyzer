var builder = DistributedApplication.CreateBuilder(args);

var api = builder.AddProject<Projects.FellowshipAnalyzer_Api>("fellowshipanalyzerapi")
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
        e.Port = 5122;
        e.TargetPort = 5122;
        e.TargetHost = "fellowshipanalyzer.dev.localhost";
        e.UriScheme = "http";
        e.IsProxied = false;
    });

builder.Build().Run();
