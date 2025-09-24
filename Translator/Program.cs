using Microsoft.Extensions.Options;
using Translator.Controllers;
using Translator.Models.Configs;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
builder.Services.AddSignalR();
builder.Services.AddMemoryCache();

//ע���ȡ�����ļ�
builder.Services.Configure<AiSpeechConfig>(builder.Configuration.GetSection(nameof(AiSpeechConfig)));
builder.Services.AddSingleton(sp => sp.GetRequiredService<IOptions<AiSpeechConfig>>().Value);

builder.Services.AddScoped<Translator.Service.TranslationService>();
builder.Services.AddScoped<Translator.Service.SynthesizerService>();

var app = builder.Build();

// Configure the HTTP request pipeline.

app.UseAuthorization();

app.MapControllers();
app.MapHub<TranslationHub>("/ChatTrans");

app.Run();
