using FellowshipAnalyzer.Api.Core;
using FellowshipAnalyzer.Api.Core.Generated;
using FellowshipAnalyzer.ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddAzureBlobServiceClient("blobs");

builder.Services.AddFellowshipLogsApi(
    builder.Configuration,
    allowDevelopmentLoopbackOrigins: builder.Environment.IsDevelopment());

builder.Services.AddBlobPersistentCache();

builder.Services.AddOpenApi();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

app.MapDefaultEndpoints();
app.MapOpenApi();

app.UseSwagger();
app.UseSwaggerUI(o =>
{
    o.SwaggerEndpoint("/swagger/v1/swagger.json", "v1");
    o.RoutePrefix = string.Empty;
});

app.MapFellowshipApiEndpoints();

app.Run();
