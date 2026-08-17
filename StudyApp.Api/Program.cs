using System.Threading.Channels;
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
using StudyApp.Infrastructure.Embeddings;
using StudyApp.Infrastructure.Events;
using StudyApp.Infrastructure.Persistence;
using StudyApp.Infrastructure.Repositories;
using StudyApp.Infrastructure.Storage;


var builder = WebApplication.CreateBuilder(args);


builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Default")));

builder.Services.AddScoped<IWorkrepository, WorkspaceRepository>();
builder.Services.AddScoped<IWorkSpaceService, WorkspaceService>();

builder.Services.AddScoped<IFolderRepository, FolderRepository>();
builder.Services.AddScoped<IFolderService, FolderService>();

builder.Services.AddScoped<INoteRepository, NoteRepository>();
builder.Services.AddScoped<INoteService, NoteService>();
builder.Services.AddScoped<IEventPublisher, LoggingEventPublisher>();

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


builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.MapControllers();
app.Run();
