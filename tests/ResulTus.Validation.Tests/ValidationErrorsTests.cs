// Copyright (c) ResulTus. All rights reserved.

using System.ComponentModel.DataAnnotations;

namespace ResulTus.Validation.Tests;

/// <summary>
/// A DTO whose properties carry DataAnnotations attributes, used to exercise the adapter.
/// </summary>
public sealed class AnnotatedPerson
{
    /// <summary>
    /// Gets or sets the name, which is required.
    /// </summary>
    [Required]
    public string? Name { get; set; }

    /// <summary>
    /// Gets or sets the age, which must be between 18 and 120.
    /// </summary>
    [Range(18, 120)]
    public int Age { get; set; }

    /// <summary>
    /// Gets or sets the email, which must look like an email when supplied.
    /// </summary>
    [EmailAddress]
    public string? Email { get; set; }
}

/// <summary>
/// A DTO whose validation is object-level: the attribute sits on the type, so the failure carries
/// no member name.
/// </summary>
[AtLeastOneProperty]
public sealed class ObjectLevelModel
{
    /// <summary>
    /// Gets or sets an optional value.
    /// </summary>
    public string? Value { get; set; }
}

/// <summary>
/// An object-level validation attribute used to prove failures without a member name are kept.
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public sealed class AtLeastOnePropertyAttribute : ValidationAttribute
{
    /// <inheritdoc />
    public override bool IsValid(object? value) => false;
}

/// <summary>
/// A model whose object-level validation reports a failure without an error message, used to prove
/// the adapter's fallback message rather than leaving that path untested.
/// </summary>
public sealed class NoMessageModel : IValidatableObject
{
    /// <summary>
    /// Gets or sets an optional value.
    /// </summary>
    public string? Value { get; set; }

    /// <inheritdoc />
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        yield return new ValidationResult(errorMessage: null, [nameof(Value)]);
    }
}

/// <summary>
/// Tests for <see cref="ValidationErrors"/> as a data type.
/// </summary>
public class ValidationErrorsTests
{
    /// <summary>
    /// GIVEN a <see cref="ValidationErrors"/> built from no issues
    /// WHEN <see cref="ValidationErrors.HasErrors"/> is read
    /// THEN it MUST be <c>false</c>.
    /// </summary>
    [Fact]
    public void Empty_ShouldNotHaveErrors()
    {
        var errors = new ValidationErrors([]);

        errors.HasErrors.Should().BeFalse();
        errors.Issues.Should().BeEmpty();
    }

    /// <summary>
    /// GIVEN a <see cref="ValidationErrors"/> built from several issues
    /// WHEN it is inspected
    /// THEN every issue MUST be preserved, in order, with its field, message and code.
    /// </summary>
    [Fact]
    public void Issues_ShouldPreserveEverythingInOrder()
    {
        var first = new ValidationIssue("Name", "required", "Required");
        var second = new ValidationIssue("Age", "too young");

        var errors = new ValidationErrors([first, second]);

        errors.HasErrors.Should().BeTrue();
        errors.Issues.Should().Equal(first, second);
        errors.Issues[0].Code.Should().Be("Required");
        errors.Issues[1].Code.Should().BeNull();
    }

    /// <summary>
    /// GIVEN several issues sharing a field name
    /// WHEN <see cref="ValidationErrors.ToDictionary"/> is called
    /// THEN the messages MUST be grouped under that field, keeping their order.
    /// </summary>
    [Fact]
    public void ToDictionary_ShouldGroupByField()
    {
        var errors = new ValidationErrors(
        [
            new ValidationIssue("Name", "first"),
            new ValidationIssue("Age", "too young"),
            new ValidationIssue("Name", "second"),
        ]);

        var map = errors.ToDictionary();

        map.Should().HaveCount(2);
        map["Name"].Should().Equal("first", "second");
        map["Age"].Should().Equal("too young");
    }

    /// <summary>
    /// GIVEN a validation failure
    /// WHEN <see cref="ValidationErrors.ToError"/> is called
    /// THEN it MUST produce a <see cref="ErrorType.Validation"/> error carrying the failure count.
    /// </summary>
    [Fact]
    public void ToError_ShouldBeValidationType()
    {
        var errors = new ValidationErrors(
        [
            new ValidationIssue("Name", "required"),
            new ValidationIssue("Age", "too young"),
        ]);

        var error = errors.ToError();

        error.Type.Should().Be(ErrorType.Validation);
        error.Code.Should().Be("Validation.Failed");
        error.Message.Should().Contain("2");
    }

    /// <summary>
    /// GIVEN a <c>null</c> issue collection
    /// WHEN <see cref="ValidationErrors"/> is constructed
    /// THEN it MUST throw <see cref="ArgumentNullException"/>.
    /// </summary>
    [Fact]
    public void Constructor_WithNull_ShouldThrow()
    {
        var act = () => new ValidationErrors(null!);

        act.Should().Throw<ArgumentNullException>();
    }
}

/// <summary>
/// Tests for the DataAnnotations adapter.
/// </summary>
public class DataAnnotationsValidationTests
{
    /// <summary>
    /// GIVEN a valid object
    /// WHEN it is validated
    /// THEN the result MUST be a success carrying the same instance.
    /// </summary>
    [Fact]
    public void ValidObject_ShouldBeSuccess()
    {
        var person = new AnnotatedPerson { Name = "Ada", Age = 36 };

        var (isSuccess, value, errors) = person.ValidateAnnotations();

        isSuccess.Should().BeTrue();
        value.Should().BeSameAs(person);
        errors.Should().BeNull();
    }

    /// <summary>
    /// GIVEN an object violating two attributes
    /// WHEN it is validated
    /// THEN the result MUST be a failure carrying one issue per violated attribute, named after
    /// the property that failed.
    /// </summary>
    [Fact]
    public void InvalidObject_ShouldReportEveryViolation()
    {
        var person = new AnnotatedPerson { Name = null, Age = 10 };

        var (isSuccess, _, errors) = person.ValidateAnnotations();

        isSuccess.Should().BeFalse();
        errors.Should().NotBeNull();
        errors!.Issues.Select(i => i.Field).Should().BeEquivalentTo(["Name", "Age"]);
        errors.Issues.Should().OnlyContain(i => !string.IsNullOrWhiteSpace(i.Message));
    }

    /// <summary>
    /// GIVEN an object whose only problem is a non-required property
    /// WHEN it is validated
    /// THEN it MUST still fail, proving every property is checked and not just the required ones.
    /// </summary>
    [Fact]
    public void NonRequiredAttribute_ShouldStillBeValidated()
    {
        var person = new AnnotatedPerson { Name = "Ada", Age = 36, Email = "not-an-email" };

        var (isSuccess, _, errors) = person.ValidateAnnotations();

        isSuccess.Should().BeFalse();
        errors!.Issues.Should().ContainSingle();
        errors.Issues[0].Field.Should().Be("Email");
    }

    /// <summary>
    /// GIVEN an object whose validation attribute reports no member name
    /// WHEN it is validated
    /// THEN the issue MUST still be captured, under the empty-string field.
    /// </summary>
    [Fact]
    public void ObjectLevelFailure_ShouldUseEmptyFieldName()
    {
        var model = new ObjectLevelModel();

        var (isSuccess, _, errors) = model.ValidateAnnotations();

        isSuccess.Should().BeFalse();
        errors!.Issues.Should().ContainSingle();
        errors.Issues[0].Field.Should().BeEmpty();
    }

    /// <summary>
    /// GIVEN a <c>null</c> instance
    /// WHEN it is validated
    /// THEN it MUST throw <see cref="ArgumentNullException"/>.
    /// </summary>
    [Fact]
    public void NullInstance_ShouldThrow()
    {
        AnnotatedPerson? person = null;

        var act = () => person!.ValidateAnnotations();

        act.Should().Throw<ArgumentNullException>();
    }

    /// <summary>
    /// GIVEN an object whose validation reports a failure without a message
    /// WHEN it is validated
    /// THEN the issue MUST still be reported, with a non-empty fallback message instead of an
    /// empty string the client would render as a blank error.
    /// </summary>
    [Fact]
    public void FailureWithoutMessage_ShouldUseTheFallbackMessage()
    {
        var (isSuccess, _, errors) = new NoMessageModel().ValidateAnnotations();

        isSuccess.Should().BeFalse();
        errors!.Issues.Should().ContainSingle();
        errors.Issues[0].Field.Should().Be("Value");
        errors.Issues[0].Message.Should().Be("The value is invalid.");
    }
}
