using Microsoft.Extensions.Options;
using Translator.Models.Configs;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<AiSpeechConfig>(builder.Configuration.GetSection(nameof(AiSpeechConfig)));
builder.Services.AddSingleton(sp => sp.GetRequiredService<IOptions<AiSpeechConfig>>().Value);

// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.AddSignalR();
builder.Services.AddMemoryCache();

// 服务注册（确保 TranslationHub 是同一个单例既可注入到 Controller，也可作为 HostedService）
builder.Services.AddSingleton<Translator.Service.TranslationService>();
builder.Services.AddSingleton<Translator.Service.SynthesizerService>();
builder.Services.AddSingleton<Translator.Controllers.TranslationHub>();
// 将同一个 TranslationHub 单例作为 IHostedService 注入
builder.Services.AddSingleton<IHostedService>(sp => sp.GetRequiredService<Translator.Controllers.TranslationHub>());

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
}
app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
