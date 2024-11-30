using System.Reflection;
using CodeReview.Services;
using Microsoft.AspNetCore.Http.Features;

namespace CodeReview.API;

public class Program
{
    public static void Main(string[] args)
    {
        var path = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? string.Empty, "Projects");

        if (Directory.Exists(path))
            Directory.Delete(path, true);
        else
            Directory.CreateDirectory(path);        
        
        var builder = WebApplication.CreateBuilder(args);

        // Add services to the container.

        builder.WebHost.ConfigureKestrel(options =>
        {
            options.Limits.MaxRequestBodySize = 500_000_000;  // 500 MB, установите нужное значение
        });        
        
        builder.Services.Configure<FormOptions>(options =>
        {
            options.MultipartBodyLengthLimit = 500_000_000; // Установите максимальный размер (в байтах)
        });

        builder.Services.AddCors(options =>
        {
            options.AddPolicy("AllowAll",
                policy => policy.AllowAnyOrigin()  // Разрешить любой источник
                    .AllowAnyMethod()  // Разрешить любой HTTP метод
                    .AllowAnyHeader());  // Разрешить любые заголовки
        });

        
        builder.Services.AddControllers();
        // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen();

        var app = builder.Build();

        // Configure the HTTP request pipeline.
        app.UseSwagger();
        app.UseSwaggerUI();
        app.UseHttpsRedirection();
        app.UseCors("AllowAll");
        app.UseAuthorization();


        app.MapControllers();

        app.Run();
    }
}