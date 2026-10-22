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
builder.Services.AddScoped<CanonicalProductMatcher>();
builder.Services.AddHttpClient<NfceDocumentReader>(client =>
{
    // O WAF da SEFAZ-SP rejeita silenciosamente (HTTP 200 com uma pagina "Request Rejected")
    // requisicoes que alegam ser um navegador conhecido (testado com UA de Safari e de Chrome)
    // em algumas URLs de consulta, mas libera uma UA que nao finge ser navegador. Nao
    // disfarçar.
    client.DefaultRequestHeaders.UserAgent.ParseAdd("SmartGrocery-NFCeReader/1.0 (+https://github.com/LucasBoni29/smart-grocery-nfce-main)");
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
