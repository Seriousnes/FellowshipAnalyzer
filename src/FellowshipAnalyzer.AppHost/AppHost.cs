var builder = DistributedApplication.CreateBuilder(args);

var storage = builder.AddAzureStorage("storage").RunAsEmulator();
var blobs = storage.AddBlobs("blobs");

var api = builder
    .AddProject<Projects.FellowshipAnalyzer_DevApi>("fellowshipanalyzerapi")
    .WithReference(blobs)
    .WaitFor(blobs)
    .WithExternalHttpEndpoints();

builder.AddProject<Projects.FellowshipAnalyzer_DevHost>("fellowshipanalyzer-devhost")
    .WithReference(api)
    .WaitFor(api);

builder.Build().Run();
