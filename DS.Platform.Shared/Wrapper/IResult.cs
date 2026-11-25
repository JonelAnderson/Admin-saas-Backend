using FluentValidation.Results;

namespace DS.Platform.Shared.Wrapper
{
    public interface IResult
    {
        List<string> Messages { get; set; }

        bool Succeeded { get; set; }

        IEnumerable<ValidationFailure>? Errors { get; set; }
    }

    public interface IResult<out T> : IResult
    {
        T Data { get; }
    }
}