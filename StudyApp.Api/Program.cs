using System.Threading.Channels;
using Scalar.AspNetCore;
using Microsoft.EntityFrameworkCore;
using StudyApp.Application.Documents;
using StudyApp.Application.Embeddings;
using StudyApp.Application.Events;
using StudyApp.Application.Folders;
using StudyApp.Application.Notes;
using StudyApp.Application.Repositories;
using StudyApp.Application.Scribbles;
using StudyApp.Application.Storage;
using StudyApp.Application.Workspaces;
using StudyApp.Application.Scoping;
using StudyApp.Application.Chat;
using StudyApp.Infrastructure.Embeddings;
using StudyApp.Infrastructure.Events;
using StudyApp.Infrastructure.Persistence;
using StudyApp.Infrastructure.Repositories;
using StudyApp.Infrastructure.Storage;
using StudyApp.Infrastructure.Chat;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using StudyApp.Application.Common;

using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);

// Telemetry stuff for integration with SigNoz
var otelEndpoint = builder.Configuration["Otel:Endpoint"] ?? "http://localhost:4317";
const string serviceName = "studyapp-api";

builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService(serviceName))
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddEntityFrameworkCoreInstrumentation()
        .AddNpgsql()
        .AddOtlpExporter(otlp => otlp.Endpoint = new Uri(otelEndpoint)))
        .WithMetrics(metrics => metrics
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddRuntimeInstrumentation()
        .AddOtlpExporter(otlp => otlp.Endpoint = new Uri(otelEndpoint)));

builder.Logging.AddOpenTelemetry(logging =>
{
    logging.IncludeFormattedMessage = true;
    logging.IncludeScopes = true;
    logging.AddOtlpExporter(otlp => otlp.Endpoint = new Uri(otelEndpoint));
});

builder.Services.AddCors(options => {
    options.AddPolicy("AllowAngular", policy =>
    {
        policy
        .WithOrigins("http://localhost:4200")
        .AllowAnyHeader()
        .AllowAnyMethod();
    });
});

builder.Services.AddScoped<IWorkrepository, WorkspaceRepository>();
builder.Services.AddScoped<IWorkSpaceService, WorkspaceService>();

builder.Services.AddScoped<IFolderRepository, FolderRepository>();
builder.Services.AddScoped<IFolderService, FolderService>();

builder.Services.AddScoped<INoteRepository, NoteRepository>();
builder.Services.AddScoped<INoteService, NoteService>();

builder.Services.AddScoped<IScribbleRepository, ScribbleRepository>();
builder.Services.AddScoped<INoteScribbleLinkRepository, NoteScribbleLinkRepository>();
builder.Services.AddScoped<IScribbleService, ScribbleService>();

// Postgres + pgvector — matches "Default" from appsettings.json
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Default"), o => o.UseVector()));

// event queue
builder.Services.AddSingleton(Channel.CreateUnbounded<DomainEvent>());
builder.Services.AddSingleton<IEventPublisher, ChannelEventPublisher>();
builder.Services.AddHostedService<EventProcessingWorker>();

// file storage
builder.Services.AddSingleton<IFileStorage>(_ =>
    new LocalFileStorage(Path.Combine(builder.Environment.ContentRootPath, "files")));

// extraction + embedding
builder.Services.AddScoped<ITextExtractor, TextExtractor>();
builder.Services.AddHttpClient<IEmbeddingService, OllamaEmbeddingService>(client =>
    client.BaseAddress = new Uri("http://localhost:11434"));

// document module
builder.Services.AddScoped<IDocumentRepository, DocumentRepository>();
builder.Services.AddScoped<IVectorChunkRepository, VectorChunkRepository>();
builder.Services.AddScoped<IDocumentService, DocumentService>();

builder.Services.AddScoped<IScopeResolver, ScopeResolver>();
builder.Services.AddScoped<IChatSessionRepository, ChatSessionRepository>();
builder.Services.AddScoped<IChatMessageRepository, ChatMessageRepository>();
builder.Services.AddScoped<IChatService, ChatService>();
builder.Services.AddHttpClient<IChatLlmService, OllamaChatService>(client =>
    client.BaseAddress = new Uri("http://localhost:11434"));

builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    options.InvalidModelStateResponseFactory = context =>
    {
        var errors = context.ModelState
            .Where(e => e.Value?.Errors.Count > 0)
            .ToDictionary(
                e => e.Key,
                e => e.Value!.Errors.Select(x => x.ErrorMessage).ToArray()
            );

        var response = ApiResponse<object>.Fail(400, "Validation failed", errors);
        return new BadRequestObjectResult(response);
    };
});

builder.Services.AddControllers()
    .AddJsonOptions(options =>
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();

var app = builder.Build();

app.UseCors("AllowAngular");

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler(errorApp =>
    {
        errorApp.Run(async context =>
        {
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            context.Response.ContentType = "application/problem+json";
            await context.Response.WriteAsJsonAsync(new
            {
                title = "An unexpected error occurred.",
                status = 500
            });
        });
    });
}

app.MapControllers();
app.Run();
