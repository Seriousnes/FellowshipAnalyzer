using FellowshipAnalyzer.Api.Core;

using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = FunctionsApplication.CreateBuilder(args);

builder.AddApiDefaults();
builder.ConfigureFunctionsWebApplication();

builder.Services.AddFellowshipLogsApi(
    builder.Configuration,
    allowDevelopmentLoopbackOrigins: builder.Environment.IsDevelopment());

var app = builder.Build();
app.Run();
