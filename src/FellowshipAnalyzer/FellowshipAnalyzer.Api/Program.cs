using FellowshipAnalyzer.Api.Core;

using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.Hosting;

var builder = FunctionsApplication.CreateBuilder(args);

builder.AddApiDefaults();
builder.ConfigureFunctionsWebApplication();

builder.AddAzureBlobServiceClient("BlobsConnection");

builder.Services.AddFellowshipLogsApi(
    builder.Configuration,
    allowDevelopmentLoopbackOrigins: builder.Environment.IsDevelopment());

builder.Services.AddBlobPersistentCache();

var app = builder.Build();
app.Run();
