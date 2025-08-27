using Microsoft.AspNetCore.HttpOverrides; // <-- Add this using directive

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// --- START: Add Forwarded Headers Configuration ---
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    // This tells the app to trust the headers that say the original protocol was HTTPS.
    options.ForwardedHeaders =
        ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;

    // It's good practice to clear known networks and proxies if you're in a cloud environment
    // where the proxy IP isn't static or known.
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});
// --- END: Add Forwarded Headers Configuration ---
// Force Kestrel to listen on 0.0.0.0:80
builder.WebHost.ConfigureKestrel(serverOptions =>
{
    serverOptions.ListenAnyIP(80);
});

var app = builder.Build();

// --- START: Use the Forwarded Headers Middleware ---
// This MUST be one of the first pieces of middleware to run.
app.UseForwardedHeaders();
// --- END: Use the Forwarded Headers Middleware ---

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

//app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
