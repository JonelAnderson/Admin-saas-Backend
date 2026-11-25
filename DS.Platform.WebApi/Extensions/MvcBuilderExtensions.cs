using FluentValidation.AspNetCore;
using DS.Platform.Application.Configurations;
using Microsoft.Extensions.DependencyInjection;
using FluentValidation;

namespace DS.Platform.WebApi.Extensions
{
    internal static class MvcBuilderExtensions
    {
        internal static IMvcBuilder AddValidators(this IMvcBuilder builder)
        {
            builder.Services.AddFluentValidationAutoValidation();
            builder.Services.AddFluentValidationClientsideAdapters();
            builder.Services.AddValidatorsFromAssemblyContaining<AppConfiguration>();
            return builder;
        }

       
    }
}