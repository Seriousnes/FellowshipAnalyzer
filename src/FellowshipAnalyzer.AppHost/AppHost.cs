var builder = DistributedApplication.CreateBuilder(args);

builder.AddProject<Projects.FellowshipAnalyzer>("fellowshipanalyzer");

builder.Build().Run();
