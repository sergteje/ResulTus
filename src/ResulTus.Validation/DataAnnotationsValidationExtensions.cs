// Copyright (c) ResulTus. All rights reserved.

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace ResulTus.Validation;

/// <summary>
/// Validates an object against its <see cref="System.ComponentModel.DataAnnotations"/> attributes and
/// returns a <see cref="Result{TSuccess,TError}"/> carrying every failure.
/// </summary>
/// <remarks>
/// Every property is checked, not only the ones carrying <see cref="RequiredAttribute"/>. A failure
/// reported without a member name is kept under the empty field name rather than dropped.
/// </remarks>
public static class DataAnnotationsValidationExtensions
{
    /// <summary>
    /// Validates <paramref name="instance"/> against its DataAnnotations attributes.
    /// </summary>
    /// <typeparam name="T">The type being validated.</typeparam>
    /// <param name="instance">The instance to validate.</param>
    /// <returns>
    /// A success carrying <paramref name="instance"/> when it is valid, or a failure carrying a
    /// <see cref="ValidationErrors"/> with one issue per violated attribute.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="instance"/> is <c>null</c>.</exception>
    /// <remarks>
    /// <see cref="System.ComponentModel.DataAnnotations"/> validation reflects over the properties of
    /// <typeparamref name="T"/>, whose set cannot be discovered statically, so this method carries
    /// <see cref="RequiresUnreferencedCodeAttribute"/> honestly instead of silencing the analyzer.
    /// Callers that trim must keep the validated type's members reachable, or call it from code that
    /// is not trimmed.
    /// </remarks>
    [RequiresUnreferencedCode(
        "DataAnnotations validation reflects over the properties of the validated type, which trimming may remove.")]
    public static Result<T, ValidationErrors> ValidateAnnotations<T>(this T instance)
    {
        ArgumentNullException.ThrowIfNull(instance);

        var context = new ValidationContext(instance);
        var results = new List<ValidationResult>();

        if (Validator.TryValidateObject(instance, context, results, validateAllProperties: true))
        {
            return Result<T, ValidationErrors>.Success(instance);
        }

        var issues = new List<ValidationIssue>(results.Count);

        foreach (var result in results)
        {
            var message = result.ErrorMessage ?? "The value is invalid.";

            if (result.MemberNames.Any())
            {
                foreach (var member in result.MemberNames)
                {
                    issues.Add(new ValidationIssue(member, message));
                }
            }
            else
            {
                issues.Add(new ValidationIssue(string.Empty, message));
            }
        }

        return Result<T, ValidationErrors>.Failure(new ValidationErrors(issues));
    }
}
