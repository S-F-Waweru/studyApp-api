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
using StudyApp.Application.Retrieval;
using StudyApp.Application.Ai;
using StudyApp.Infrastructure.Embeddings;
using StudyApp.Infrastructure.Events;
using StudyApp.Infrastructure.Persistence;
using StudyApp.Infrastructure.Repositories;
using StudyApp.Infrastructure.Storage;
using StudyApp.Infrastructure.Ai;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Npgsql;
using StudyApp.Application.Common;

using System.Security.Cryptography;
using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using StudyApp.Application.Auth;
using StudyApp.Infrastructure.Identity;

using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using StudyApp.Infrastructure.Embeddings.Readers;
using StudyApp.Infrastructure.Identity;

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
//app-settings configurations
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection("Jwt"));
builder.Services.AddSingleton(sp => sp.GetRequiredService<IOptions<JwtOptions>>().Value);


// Postgres + pgvector — matches "Default" from appsettings.json
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Default"), o => o.UseVector()));

// ── Phase 7.1 — identity ────────────────────────────────────────────────
// the key is resolved once, before anything reads it: issuer and validator must
// provably share the same bytes, and a missing key must fail loudly at startup
var jwt = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();

if (string.IsNullOrWhiteSpace(jwt.SigningKey))
{
    if (!builder.Environment.IsDevelopment())
        throw new InvalidOperationException(
            "Jwt:SigningKey is required outside Development. Set Jwt__SigningKey or use user-secrets.");

    // ephemeral by design: every restart invalidates old tokens, which is exactly
    // what you want on a dev box and exactly what you don't want in production
    jwt.SigningKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));
    Console.WriteLine("JWT: no Jwt:SigningKey configured — generated an ephemeral development key.");
}

builder.Services.AddSingleton(jwt);

builder.Services.AddIdentityCore<AppUser>(options =>
    {
        options.User.RequireUniqueEmail = true;
        options.Password.RequiredLength = 10;
        options.Password.RequireDigit = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireNonAlphanumeric = false;
        options.Lockout.AllowedForNewUsers = true;
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
    })
    .AddRoles<IdentityRole<Guid>>()
    .AddEntityFrameworkStores<AppDbContext>()
    .AddDefaultTokenProviders();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false; // keep "sub" as "sub" — the default remap is a classic trap
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwt.Issuer,
            ValidateAudience = true,
            ValidAudience = jwt.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        };
    });

// login throttling runs on two independent counters: this one limits per-IP
// throughput, while AuthService's lockout limits per-account guessing
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests; // the default is 503

    options.AddPolicy("login", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = builder.Configuration.GetValue("RateLimit:LoginPerMinute", 10),
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));

    // keep the ApiResponse envelope, otherwise the Angular interceptor cannot parse a 429
    options.OnRejected = async (context, ct) =>
    {
        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        await context.HttpContext.Response.WriteAsJsonAsync(
            ApiResponse<object>.Fail(429, "Too many attempts. Try again in a minute."), ct);
    };
});

builder.Services.AddAuthorization();
builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();

// event queue — bounded so a burst of autosaves cannot exhaust memory
builder.Services.AddSingleton(Channel.CreateBounded<DomainEvent>(new BoundedChannelOptions(10_000)
{
    FullMode = BoundedChannelFullMode.DropOldest,
    SingleReader = true
}));
builder.Services.AddSingleton<IEventPublisher, ChannelEventPublisher>();
builder.Services.AddHostedService<EventProcessingWorker>();

// file storage
builder.Services.AddSingleton<IFileStorage>(_ =>
    new LocalFileStorage(Path.Combine(builder.Environment.ContentRootPath, "files")));

// extraction
builder.Services.AddScoped<ITextExtractor, TextExtractor>();
builder.Services.AddScoped<IDocumentChunker, DocumentChunker>();
builder.Services.AddSingleton<PdfTextExtractor>();
builder.Services.AddSingleton<OpenXmlTextExtractor>();
builder.Services.AddSingleton<SpreadsheetTextExtractor>();
builder.Services.AddSingleton<MarkdownTextExtractor>();
builder.Services.AddSingleton<ScribbleCanvasExtractor>();
builder.Services.AddScoped<IImageCaptioner, ImageCaptioner>();

// AI settings — defaults from appsettings "Ai"; once saved via /api/ai/settings the DB row wins
builder.Services.AddSingleton(builder.Configuration.GetSection("Ai").Get<AiDefaults>() ?? new AiDefaults());
builder.Services.AddScoped<IAiSettingsRepository, AiSettingsRepository>();
builder.Services.AddScoped<IAiSettingsService, AiSettingsService>();
builder.Services.AddScoped<IAiConnectionTester, AiConnectionTester>();

// named clients — base address now comes from settings per call, so it can't be fixed at startup
builder.Services.AddHttpClient("ai-chat", client => client.Timeout = TimeSpan.FromMinutes(5));
builder.Services.AddHttpClient("ai-embeddings", client => client.Timeout = TimeSpan.FromMinutes(2));

builder.Services.AddSingleton<IChatProvider, OllamaChatProvider>();
builder.Services.AddSingleton<IChatProvider, OpenAiCompatibleChatProvider>();
builder.Services.AddSingleton<IEmbeddingProvider, OllamaEmbeddingProvider>();
builder.Services.AddSingleton<IEmbeddingProvider, OpenAiCompatibleEmbeddingProvider>();
builder.Services.AddSingleton<IVisionProvider, OllamaVisionProvider>();
builder.Services.AddSingleton<IVisionProvider, OpenAiCompatibleVisionProvider>();

// the public seams keep their interfaces — ChatService / EventProcessingWorker etc. don't change
builder.Services.AddScoped<IEmbeddingService, ConfiguredEmbeddingService>();

// document module
builder.Services.AddScoped<IDocumentRepository, DocumentRepository>();
builder.Services.AddScoped<IVectorChunkRepository, VectorChunkRepository>();
builder.Services.AddScoped<IDocumentService, DocumentService>();

builder.Services.AddScoped<IScopeResolver, ScopeResolver>();
builder.Services.AddScoped<IChatSessionRepository, ChatSessionRepository>();
builder.Services.AddScoped<IChatMessageRepository, ChatMessageRepository>();
builder.Services.AddScoped<IChatService, ChatService>();
builder.Services.AddScoped<ISystemPromptBuilder, SystemPromptBuilder>();
builder.Services.AddScoped<IChatLlmService, ConfiguredChatLlmService>();
builder.Services.AddScoped<ISourceIndexStatusRepository, SourceIndexStatusRepository>();
builder.Services.AddScoped<StudyApp.Application.Indexing.IIndexReindexService, IndexReindexService>();

// retrieval — hybrid vector + keyword, fused and budgeted before it reaches the prompt
builder.Services.AddSingleton(builder.Configuration.GetSection("Retrieval").Get<RetrievalOptions>() ?? new RetrievalOptions());
builder.Services.AddScoped<IRetrievalService, RetrievalService>();

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

// docker compose sets Db__MigrateOnStartup=true so a fresh database volume is
// provisioned automatically; local dev keeps migrations manual (dotnet ef ...)
if (builder.Configuration.GetValue("Db:MigrateOnStartup", false))
{
    using var scope = app.Services.CreateScope();
    await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();
}

app.UseCors("AllowAngular");

// authentication populates HttpContext.User from the bearer token;
// authorization then decides whether that user may proceed. This order is fixed.
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();




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
