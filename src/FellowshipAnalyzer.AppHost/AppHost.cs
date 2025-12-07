var builder = DistributedApplication.CreateBuilder(args);

var api = builder.AddAzureFunctionsProject<Projects.FellowshipAnalyzer_Api>("fellowshipanalyzerapi");

builder.AddProject<Projects.FellowshipAnalyzer>("fellowshipanalyzer")
    .WithReference(api)
    .WaitFor(api);

builder.Build().Run();
