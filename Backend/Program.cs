using CopilotApi.Services;
using CopilotApi.Channels;
using CopilotApi.Options;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddHttpClient();

// Options
builder.Services.Configure<TelegramOptions>(builder.Configuration.GetSection(TelegramOptions.SectionName));

// Add CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// Register Copilot Service as singleton
builder.Services.AddSingleton<CopilotService>();
builder.Services.AddSingleton<IChatChannel, TelegramChannel>();
builder.Services.AddHostedService<ChannelService>();

var app = builder.Build();

// Initialize Copilot Service
var copilotService = app.Services.GetRequiredService<CopilotService>();
await copilotService.InitializeAsync();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseCors("AllowAll");
app.UseHttpsRedirection();
app.MapControllers();

app.Run();
