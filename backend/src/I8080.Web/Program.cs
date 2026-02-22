using I8080.Web.Hubs;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSignalR(options =>
{
    options.EnableDetailedErrors = true;
    options.MaximumReceiveMessageSize = 1024;
});

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins("http://localhost:5173", "http://localhost:3000")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

var app = builder.Build();

app.UseCors();

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapHub<TerminalHub>("/terminal");

app.MapGet("/api/health", () => Results.Ok(new { status = "ok", system = "CP/M 2.2 Emulator" }));

app.Run();
