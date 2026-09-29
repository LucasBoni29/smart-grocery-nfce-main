using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using SmartGrocery.Api.Data;
using SmartGrocery.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddCors(options =>
{
    options.AddPolicy("Development", policy =>
        policy.AllowAnyOrigin()
            .AllowAnyHeader()
            .AllowAnyMethod());
});
builder.Services.AddHttpClient<NfceDocumentReader>(client =>
{
    client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (iPhone; CPU iPhone OS 17_0 like Mac OS X) AppleWebKit/605.1.15 Version/17.0 Mobile/15E148 Safari/604.1");
});

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString));

var apiKey = builder.Configuration["ApiKey"];

// Fora do ambiente de desenvolvimento (ex: Railway), a API tem que nascer protegida.
// Sem isso, um deploy sem a variavel ApiKey configurada ficaria publico por engano.
if (!builder.Environment.IsDevelopment() && string.IsNullOrWhiteSpace(apiKey))
{
    throw new InvalidOperationException(
        "A variavel de ambiente 'ApiKey' precisa estar configurada fora do ambiente de desenvolvimento.");
}

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    dbContext.Database.Migrate();
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("Development");
app.UseHttpsRedirection();

// Se a chave nao foi configurada (dev local, por padrao), a API fica aberta como sempre.
// Se foi configurada (dev local opcional, ou qualquer deploy), toda chamada precisa do
// header X-Api-Key com o valor certo. /swagger fica de fora porque so serve documentacao.
if (!string.IsNullOrWhiteSpace(apiKey))
{
    var apiKeyBytes = Encoding.UTF8.GetBytes(apiKey);

    app.Use(async (context, next) =>
    {
        if (context.Request.Path.StartsWithSegments("/swagger"))
        {
            await next();
            return;
        }

        var provided = context.Request.Headers["X-Api-Key"].ToString();
        if (string.IsNullOrEmpty(provided) ||
            !CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(provided), apiKeyBytes))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new { message = "Chave de API ausente ou invalida." });
            return;
        }

        await next();
    });
}

app.MapControllers();

app.Run();
