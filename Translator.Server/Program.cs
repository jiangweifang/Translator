using Microsoft.Extensions.Options;
using Translator.Controllers;
using Translator.Middlewares;
using Translator.Models.Configs;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddSignalR(option => { option.MaximumReceiveMessageSize = null; });
// cors  
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(
        builder =>
        {
            builder.SetIsOriginAllowed(_ => true)
                .AllowAnyHeader()
                .WithMethods("GET", "POST", "DELETE")
                .AllowCredentials();
        });
});
builder.Services.AddMemoryCache();

builder.Services.Configure<AiSpeechConfig>(builder.Configuration.GetSection(nameof(AiSpeechConfig)));
builder.Services.AddSingleton(sp => sp.GetRequiredService<IOptions<AiSpeechConfig>>().Value);


builder.Services.AddSingleton<Translator.Service.TranslationService>();
builder.Services.AddSingleton<Translator.Service.SynthesizerService>();
builder.Services.AddSingleton<Translator.Service.SynthesizerStreamService>();
builder.Services.AddSingleton<TranslationStream>();
builder.Services.AddSingleton<TranslationHub>();

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();
app.UseMiddleware<CrosMiddle>();
app.UseCors();
// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthorization();

app.MapControllers();
app.MapHub<TranslationHub>("/trans");
app.MapFallbackToFile("/index.html");

app.Run();
