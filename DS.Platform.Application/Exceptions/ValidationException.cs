using DS.Platform.Application.Commons.Bases;
using System;
using System.Collections.Generic;

namespace DS.Platform.Application.Exceptions;

public class ValidationException : Exception
{
    public IEnumerable<BaseError>? Errors { get; }

    public ValidationException() : base()
    {
        Errors = new List<BaseError>();
    }

    public ValidationException(IEnumerable<BaseError> errors) : this()
    {
        Errors = errors;
    }
}