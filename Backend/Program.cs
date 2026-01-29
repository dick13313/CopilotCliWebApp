using CopilotApi.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddOpenApi();

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
