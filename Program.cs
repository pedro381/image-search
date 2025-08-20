var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddControllers(options =>
{
    options.RespectBrowserAcceptHeader = true;
})
.AddMvcOptions(options =>
{
    options.ReturnHttpNotAcceptable = false; 
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "ImageSearch API",
        Version = "v1",
        Description = "API para busca e manipulação de imagens"
    });
});

var app = builder.Build();

app.UseHttpsRedirection();

app.MapGet("/", () => "API rodando!");

// Pseudocódigo detalhado:
// 1. Adicionar o middleware para servir arquivos estáticos.
// 2. Configurar o StaticFiles para apontar para uma pasta específica (ex: "wwwroot/html").
// 3. Mapear um endpoint para listar os arquivos HTML disponíveis.
// 4. Permitir o acesso direto aos arquivos HTML via rota na API.

app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(
        Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "html")),
    RequestPath = "/html"
});

// Endpoint para listar arquivos HTML disponíveis
app.MapGet("/html-files", () =>
{
    var htmlDir = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "html");
    if (!Directory.Exists(htmlDir))
        return Results.NotFound("Diretório de arquivos HTML não encontrado.");

    var files = Directory.GetFiles(htmlDir, "*.html")
        .Select(f => Path.GetFileName(f))
        .ToList();

    return Results.Ok(files);
});
app.UseSwagger();
app.UseSwaggerUI();

app.UseHttpsRedirection();

app.UseCors("AllowAll");

app.UseAuthorization();

app.MapControllers();

app.Run();