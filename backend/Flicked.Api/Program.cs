var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

// The launcher's webview is a different origin from the API, so it has to be allowed by name.
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy => policy
        .WithOrigins(
            "http://localhost:1420",    // tauri dev (vite)
            "http://tauri.localhost",   // the built app on Windows
            "tauri://localhost")        // the built app on macOS and Linux
        .AllowAnyHeader()
        .AllowAnyMethod());
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// No HTTPS redirect while the launcher talks to http://localhost:5165: the redirect
// would send it to the self-signed dev certificate, which the webview refuses.
// Put it back (behind an IsDevelopment check) once this is deployed for real.
app.UseCors();

app.UseAuthorization();

app.MapControllers();

app.Run();
