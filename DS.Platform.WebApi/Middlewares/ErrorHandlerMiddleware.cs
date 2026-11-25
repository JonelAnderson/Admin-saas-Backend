using DS.Platform.Application.Exceptions;
using DS.Platform.Shared.Static;
using DS.Platform.Shared.Wrapper;
using FluentValidation.Results;
using Microsoft.AspNetCore.Http;
using System;
using Microsoft.Extensions.Localization;
using DS.Platform.Shared.Localization;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace DS.Platform.WebApi.Middlewares
{
    public class ErrorHandlerMiddleware
    {
        private readonly RequestDelegate _next;

        public ErrorHandlerMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public const string ConcurrencyMessage =
            "Otro usuario modificó este registro al mismo tiempo y su operación no se aplicó. Actualice la pantalla y vuelva a intentar.";

        private static readonly JsonSerializerOptions CamelCase = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

        public async Task Invoke(HttpContext context, IStringLocalizer<ServerCommonResources> localizer, ILogger<ErrorHandlerMiddleware> logger)
        {
            try
            {
                await _next(context);
            }
            catch (Exception error)
            {
                var response = context.Response;
                response.ContentType = "application/json";
                var responseModel = await Result<string>.FailAsync(error.Message);

                switch (error)
                {
                    case ApiException e:
                        // custom application error
                        response.StatusCode = (int)HttpStatusCode.BadRequest;
                        break;

                    case ValidationException e:
                        // custom application error
                        response.StatusCode = (int)HttpStatusCode.BadRequest;
                        responseModel.Errors = e.Errors.Select(x => new ValidationFailure { PropertyName = x.PropertyName, ErrorMessage = x.ErrorMessage });
                        responseModel.Messages = new List<string> { localizer["MessageValidate"] };
                        break;
                    case Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException:
                        // Otra petición cambió el mismo documento o saldo a la vez (IConcurrencyChecked): se deshizo todo.
                        logger.LogWarning("Concurrency conflict on {Method} {Path}", context.Request.Method, context.Request.Path);
                        response.StatusCode = (int)HttpStatusCode.Conflict;
                        responseModel = await Result<string>.FailAsync(ConcurrencyMessage);
                        break;

                    case Microsoft.EntityFrameworkCore.DbUpdateException { InnerException: Npgsql.PostgresException { SqlState: Npgsql.PostgresErrorCodes.UniqueViolation } }:
                        // Dos peticiones crearon a la vez el mismo registro único (p. ej. el primer saldo de un producto).
                        logger.LogWarning("Unique violation on {Method} {Path}", context.Request.Method, context.Request.Path);
                        response.StatusCode = (int)HttpStatusCode.Conflict;
                        responseModel = await Result<string>.FailAsync(ConcurrencyMessage);
                        break;

                    case KeyNotFoundException e:
                        // not found error
                        response.StatusCode = (int)HttpStatusCode.NotFound;
                        break;

                    default:
                        // unhandled error: keep the details in the log, not in the response
                        logger.LogError(error, "Unhandled error on {Method} {Path}", context.Request.Method, context.Request.Path);
                        response.StatusCode = (int)HttpStatusCode.InternalServerError;
                        responseModel.Messages = new List<string> { localizer["MessageException"] };
                        break;
                }
                // camelCase like every other API response.
                var result = JsonSerializer.Serialize(responseModel, CamelCase);
                await response.WriteAsync(result);
            }
        }
    }

}