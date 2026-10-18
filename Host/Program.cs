using Application;
using EFCore;
using Microsoft.EntityFrameworkCore;
using Telegram;
using XUi;

var builder = WebApplication.CreateBuilder(args);

if (builder.Environment.IsDevelopment())
{
    builder.Configuration.AddJsonFile(
        path: "appsettings.secret.json",
        optional: true,
        reloadOnChange: true);
}

builder.Services.AddTelegramConfiguration(builder.Configuration);
builder.Services.AddXUiConfiguration(builder.Configuration);
builder.Services.AddEfCoreConfiguration(builder.Configuration);
builder.Services.AddApplicationConfiguration();

var app = builder.Build();

// Применяем все миграции при старте приложения
using (var scope = app.Services.CreateScope())
{
	var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
	await db.Database.MigrateAsync();
}

app.Run();