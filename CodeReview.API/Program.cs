using System.Reflection;
using CodeReview.Services;
using Microsoft.AspNetCore.Http.Features;

namespace CodeReview.API;

public class Program
{
    public static void Main(string[] args)
    {
        Directory.Delete(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)! + @"\Projects", true);
        var builder = WebApplication.CreateBuilder(args);

        // Add services to the container.

        builder.WebHost.ConfigureKestrel(options =>
        {
            options.Limits.MaxRequestBodySize = 500_000_000;  // 200 MB, установите нужное значение
        });        
        
        builder.Services.Configure<FormOptions>(options =>
        {
            options.MultipartBodyLengthLimit = 500_000_000; // Установите максимальный размер (в байтах)
        });

        
        builder.Services.AddControllers();
        // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen();

        var app = builder.Build();

        // Configure the HTTP request pipeline.
        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }
        app.UseHttpsRedirection();

        app.UseAuthorization();


        app.MapControllers();

        app.Run();
    }
}