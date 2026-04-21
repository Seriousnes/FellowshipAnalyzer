var builder = DistributedApplication.CreateBuilder(args);

builder.AddProject<Projects.FellowshipAnalyzer>("fellowshipanalyzer")
    .WithEndpoint("http", e =>
    {
        
        e.TargetPort = 3772;        
    });

builder.Build().Run();
