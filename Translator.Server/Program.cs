using Microsoft.Extensions.Options;
using Translator.Models.Configs;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddSignalR();
builder.Services.AddMemoryCache();

builder.Services.Configure<AiSpeechConfig>(builder.Configuration.GetSection(nameof(AiSpeechConfig)));
builder.Services.AddSingleton(sp => sp.GetRequiredService<IOptions<AiSpeechConfig>>().Value);


builder.Services.AddSingleton<Translator.Service.TranslationService>();
builder.Services.AddSingleton<Translator.Service.SynthesizerStreamService>();
builder.Services.AddSingleton<Translator.Controllers.TranslationStream>();

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthorization();

app.MapControllers();

app.MapFallbackToFile("/index.html");

app.Run();
