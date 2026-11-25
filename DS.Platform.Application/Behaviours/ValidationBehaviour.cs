using DS.Platform.Application.Commons.Bases;
using FluentValidation;
using MediatR;
using ValidationException = DS.Platform.Application.Exceptions.ValidationException;

namespace DS.Platform.Application.Behaviours
{
    public class ValidationBehavior<TRequest, TResponse>(IEnumerable<IValidator<TRequest>> validators) : IPipelineBehavior<TRequest, TResponse>
        where TRequest : IRequest<TResponse>
    {
        public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
        {
            if (validators.Any())
            {
                var context = new ValidationContext<TRequest>(request);
                var validationResults = await Task.WhenAll(validators.Select(validator => validator.ValidateAsync(context, cancellationToken)));
                var failures = validationResults.SelectMany(validationResult => validationResult.Errors)
                    .Where(validationResult => validationResult != null)
                    .ToList();

                if (failures.Any())
                {
                    var errors = failures.Select(f => new BaseError
                    {
                        PropertyName = f.PropertyName,
                        ErrorMessage = f.ErrorMessage
                    }).ToList();

                    throw new ValidationException(errors);
                }
            }
            return await next();
        }
    }
}