using System;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;
using modulum.Application.Interfaces.Repositories;
using modulum.Application.Interfaces.Services.Storage;
using modulum.Application.Interfaces.Services.Storage.Provider;
using modulum.Application.Interfaces.Serialization.Serializers;
using modulum.Application.Serialization.JsonConverters;
using modulum.Infrastructure.Repositories;
using modulum.Infrastructure.Services.Storage;
using modulum.Application.Serialization.Options;
using modulum.Infrastructure.Services.Storage.Provider;
using modulum.Application.Serialization.Serializers;

namespace modulum.Infrastructure.Extensions
{
    public static class ServiceCollectionExtensions
    {
        public static void AddInfrastructureMappings(this IServiceCollection services)
        {
            services.AddAutoMapper(Assembly.GetExecutingAssembly());
        }

        //public static IServiceCollection AddRepositories(this IServiceCollection services)
        //{
        //    return services
        //        .AddTransient(typeof(IRepositoryAsync<,>), typeof(RepositoryAsync<,>))
        //        .AddTransient<IProductRepository, ProductRepository>()
        //        .AddTransient<IBrandRepository, BrandRepository>()
        //        .AddTransient<IDocumentRepository, DocumentRepository>()
        //        .AddTransient<IDocumentTypeRepository, DocumentTypeRepository>()
        //        .AddTransient(typeof(IUnitOfWork<>), typeof(UnitOfWork<>))
        //        ;
        //}

        public static IServiceCollection AddServerStorage(this IServiceCollection services)
            => AddServerStorage(services, null);

        public static IServiceCollection AddServerStorage(this IServiceCollection services, Action<SystemTextJsonOptions> configure)
        {
            return services
                .AddScoped<IJsonSerializer, SystemTextJsonSerializer>()
                .AddScoped<IStorageProvider, ServerStorageProvider>()
                .AddScoped<IServerStorageService, ServerStorageService>()
                .AddScoped<ISyncServerStorageService, ServerStorageService>()
                .Configure<SystemTextJsonOptions>(configureOptions =>
                {
                    configure?.Invoke(configureOptions);
                    if (!configureOptions.JsonSerializerOptions.Converters.Any(c => c.GetType() == typeof(TimespanJsonConverter)))
                        configureOptions.JsonSerializerOptions.Converters.Add(new TimespanJsonConverter());
                });
        }
    }
}