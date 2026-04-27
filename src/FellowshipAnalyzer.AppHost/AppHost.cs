var builder = DistributedApplication.CreateBuilder(args);

var api = builder
    .AddProject<Projects.FellowshipAnalyzer_DevApi>("fellowshipanalyzerapi")
    .WithExternalHttpEndpoints();

builder.AddProject<Projects.FellowshipAnalyzer>("fellowshipanalyzer")
    .WithReference(api)
    .WaitFor(api);

builder.Build().Run();
