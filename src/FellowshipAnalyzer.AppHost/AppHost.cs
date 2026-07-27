var builder = DistributedApplication.CreateBuilder(args);

var storage = builder.AddAzureStorage("storage")
    .RunAsEmulator(emulator => emulator.WithDataVolume("fellowshipanalyzer-storage-data"));

var blobs = storage.AddBlobs("blobs");

const string azuriteConnectionString =
    "DefaultEndpointsProtocol=http;" +
    "AccountName=devstoreaccount1;" +
    "AccountKey=Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==;" +
    "BlobEndpoint=http://storage:10000/devstoreaccount1;" +
    "QueueEndpoint=http://storage:10001/devstoreaccount1;" +
    "TableEndpoint=http://storage:10002/devstoreaccount1";

builder.AddContainer("storage-explorer", "sebagomez/azurestorageexplorer", "3.2.3")
    .WithHttpEndpoint(targetPort: 8080, name: "http")
    .WithEnvironment("AZURITE", "true")
    .WithEnvironment("AZURE_STORAGE_CONNECTIONSTRING", azuriteConnectionString)
    .WaitFor(storage);

var api = builder
    .AddProject<Projects.FellowshipAnalyzer_DevApi>("fellowshipanalyzerapi")
    .WithReference(blobs)
    .WaitFor(blobs)
    .WithExternalHttpEndpoints();

builder.AddProject<Projects.FellowshipAnalyzer_DevHost>("fellowshipanalyzer-devhost", launchProfileName: "http")
    .WithReference(api)
    .WaitFor(api);

builder.Build().Run();
