using Microsoft.OpenApi.Models;
using UniversityApi.Services;

namespace architect
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.
            builder.Services.AddControllers();
            builder.Services.AddSingleton<IRedisService, RedisService>();
            builder.Services.AddSingleton<IElasticsearchService, ElasticsearchService>();
            builder.Services.AddSingleton<IAttendanceService, AttendanceService>();
            // Add Swagger/OpenAPI
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new OpenApiInfo
                {
                    Title = "University API",
                    Version = "v1",
                    Description = "API for university management system",
                    Contact = new OpenApiContact
                    {
                        Name = "Development Team",
                        Email = "dev@university.com"
                    }
                });
            });

            // Add custom services

            var app = builder.Build();

            // Configure the HTTP request pipeline.
            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI(c =>
                {
                    c.SwaggerEndpoint("/swagger/v1/swagger.json", "University API v1");
                    c.RoutePrefix = "api-docs"; // ������ ����� /api-docs
                });
            }

            app.UseHttpsRedirection();
            app.UseAuthorization();
            app.MapControllers();

            app.Run();
        }
    }
}