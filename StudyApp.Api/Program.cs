using System.Threading.Channels;
using Scalar.AspNetCore;
using Microsoft.EntityFrameworkCore;
using StudyApp.Application.Documents;
using StudyApp.Application.Embeddings;
using StudyApp.Application.Events;
using StudyApp.Application.Folders;
using StudyApp.Application.Notes;
using  StudyApp.Application.Repositories;
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
using StudyApp.Application.Common;


var builder = WebApplication.CreateBuilder(args);


builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Default")));



builder.Services.AddScoped<IWorkrepository, WorkspaceRepository>();
builder.Services.AddScoped<IWorkSpaceService, WorkspaceService>();

builder.Services.AddScoped<IFolderRepository, FolderRepository>();
builder.Services.AddScoped<IFolderService, FolderService>();

builder.Services.AddScoped<INoteRepository, NoteRepository>();
builder.Services.AddScoped<INoteService, NoteService>();

builder.Services.AddScoped<IScribbleRepository, ScribbleRepository>();
builder.Services.AddScoped<INoteScribbleLinkRepository, NoteScribbleLinkRepository>();
builder.Services.AddScoped<IScribbleService, ScribbleService>();

// Postgres + pgvector — note the .UseVector() call, required for the Vector column type
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Default"), o => o.UseVector()));

// event queue — single shared channel, unbounded is fine at this scale
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



// builder.Services.AddControllers();
builder.Services.AddControllers()
    .AddJsonOptions(options =>
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();

var app = builder.Build();

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

app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async context =>
    {
        context.Response.StatusCode = 500;
        context.Response.ContentType = "application/json";
        var response = ApiResponse<object>.Fail(500, "An unexpected error occurred.");
        await context.Response.WriteAsJsonAsync(response);
    });
});

app.MapControllers();
app.Run();
