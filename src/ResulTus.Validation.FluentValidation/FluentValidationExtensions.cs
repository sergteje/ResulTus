// Copyright (c) ResulTus. All rights reserved.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using FluentValidation.Results;

namespace ResulTus.Validation.FluentValidation;

/// <summary>
/// Validates an object with a FluentValidation <see cref="IValidator{T}"/> and returns a
/// <see cref="Result{TSuccess,TError}"/> carrying every failure.
/// </summary>
/// <remarks>
/// FluentValidation is an optional dependency: this assembly is the only one that references it, so
/// consumers using only DataAnnotations never take it transitively.
/// </remarks>
public static class FluentValidationExtensions
{
    /// <summary>
    /// Validates <paramref name="instance"/> with <paramref name="validator"/>.
    /// </summary>
    /// <typeparam name="T">The type being validated.</typeparam>
    /// <param name="instance">The instance to validate.</param>
    /// <param name="validator">The validator to run.</param>
    /// <returns>
    /// A success carrying <paramref name="instance"/> when it is valid, or a failure carrying a
    /// <see cref="ValidationErrors"/> with one issue per failure.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="instance"/> or <paramref name="validator"/> is <c>null</c>.
    /// </exception>
    /// <exception cref="AsyncValidatorInvokedSynchronouslyException">
    /// The validator contains asynchronous rules; use <see cref="ValidateAsync{T}"/> instead.
    /// </exception>
    public static Result<T, ValidationErrors> Validate<T>(this T instance, IValidator<T> validator)
    {
        ArgumentNullException.ThrowIfNull(instance);
        ArgumentNullException.ThrowIfNull(validator);

        return ToResult(instance, validator.Validate(instance));
    }

    /// <summary>
    /// Validates <paramref name="instance"/> with <paramref name="validator"/>, awaiting any
    /// asynchronous rules.
    /// </summary>
    /// <typeparam name="T">The type being validated.</typeparam>
    /// <param name="instance">The instance to validate.</param>
    /// <param name="validator">The validator to run.</param>
    /// <param name="cancellationToken">Forwarded to the validator so in-flight rules can be cancelled.</param>
    /// <returns>
    /// A success carrying <paramref name="instance"/> when it is valid, or a failure carrying a
    /// <see cref="ValidationErrors"/> with one issue per failure.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="instance"/> or <paramref name="validator"/> is <c>null</c>.
    /// </exception>
    public static async Task<Result<T, ValidationErrors>> ValidateAsync<T>(
        this T instance,
        IValidator<T> validator,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(instance);
        ArgumentNullException.ThrowIfNull(validator);

        var validationResult = await validator.ValidateAsync(instance, cancellationToken).ConfigureAwait(false);

        return ToResult(instance, validationResult);
    }

    /// <summary>
    /// Projects a FluentValidation <see cref="ValidationResult"/> onto a
    /// <see cref="Result{TSuccess,TError}"/>.
    /// </summary>
    /// <typeparam name="T">The type being validated.</typeparam>
    /// <param name="instance">The instance that was validated.</param>
    /// <param name="validationResult">The validator's verdict.</param>
    /// <returns>The projected result.</returns>
    private static Result<T, ValidationErrors> ToResult<T>(T instance, ValidationResult validationResult)
    {
        if (validationResult.IsValid)
        {
            return Result<T, ValidationErrors>.Success(instance);
        }

        var failures = validationResult.Errors;
        var issues = new List<ValidationIssue>(failures.Count);

        foreach (var failure in failures)
        {
            issues.Add(new ValidationIssue(
                failure.PropertyName ?? string.Empty,
                failure.ErrorMessage,
                failure.ErrorCode));
        }

        return Result<T, ValidationErrors>.Failure(new ValidationErrors(issues));
    }
}
