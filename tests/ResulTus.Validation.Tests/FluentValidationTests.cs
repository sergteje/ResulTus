// Copyright (c) ResulTus. All rights reserved.

using FluentValidation;
using FluentValidation.Results;

namespace ResulTus.Validation.Tests;

/// <summary>
/// A model validated by FluentValidation rules.
/// </summary>
public sealed class Customer
{
    /// <summary>
    /// Gets or sets the name, which must be supplied.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Gets or sets the age, which must be at least 18.
    /// </summary>
    public int Age { get; set; }
}

/// <summary>
/// A synchronous-only FluentValidation validator over <see cref="Customer"/>.
/// </summary>
/// <remarks>
/// Deliberately free of asynchronous rules: FluentValidation throws
/// <see cref="AsyncValidatorInvokedSynchronouslyException"/> when a validator with asynchronous
/// rules is invoked synchronously, so the synchronous tests need a validator that has none.
/// </remarks>
public sealed class CustomerValidator : AbstractValidator<Customer>
{
    /// <summary>
    /// Initializes a new <see cref="CustomerValidator"/>.
    /// </summary>
    public CustomerValidator()
    {
        RuleFor(c => c.Name).NotEmpty().WithMessage("Name is required.");
        RuleFor(c => c.Name).MaximumLength(50).WithMessage("Name is too long.");
        RuleFor(c => c.Age).GreaterThanOrEqualTo(18).WithMessage("Age must be 18 or older.");
    }
}

/// <summary>
/// A validator with two rules over the same property, used to prove failures are not deduplicated.
/// </summary>
public sealed class TwoRuleValidator : AbstractValidator<Customer>
{
    /// <summary>
    /// Initializes a new <see cref="TwoRuleValidator"/>.
    /// </summary>
    public TwoRuleValidator()
    {
        RuleFor(c => c.Name).NotEmpty().WithMessage("first");
        RuleFor(c => c.Name).MinimumLength(3).WithMessage("second");
    }
}

/// <summary>
/// A validator carrying an asynchronous rule that observes the cancellation token.
/// </summary>
public sealed class AsyncCustomerValidator : AbstractValidator<Customer>
{
    /// <summary>
    /// Gets the cancellation token the asynchronous rule observed, or <c>null</c> when it never ran.
    /// </summary>
    public CancellationToken? ObservedToken { get; private set; }

    /// <summary>
    /// Initializes a new <see cref="AsyncCustomerValidator"/>.
    /// </summary>
    public AsyncCustomerValidator()
    {
        RuleFor(c => c.Name).NotEmpty().WithMessage("Name is required.");
        RuleFor(c => c.Age).GreaterThanOrEqualTo(18).WithMessage("Age must be 18 or older.");
        RuleFor(c => c.Name)
            .MustAsync(async (name, token) =>
            {
                ObservedToken = token;
                await Task.Yield();
                token.ThrowIfCancellationRequested();
                return name != "taken";
            })
            .WithMessage("Name is already taken.");
    }
}

/// <summary>
/// A hand-written validator that reports a failure with no property name.
/// </summary>
/// <remarks>
/// FluentValidation's own rules always supply a property name (empty for object-level rules), so
/// this is the only way to reach the adapter's null-property-name guard. A hand-written
/// <see cref="IValidator{T}"/> is a supported extension point, and it must not be able to make the
/// adapter produce a dictionary with a null key.
/// </remarks>
public sealed class NullPropertyValidator : IValidator<Customer>
{
    /// <inheritdoc />
    public ValidationResult Validate(Customer instance)
        => new([new ValidationFailure(null, "the object is invalid") { ErrorCode = "Custom" }]);

    /// <inheritdoc />
    public Task<ValidationResult> ValidateAsync(Customer instance, CancellationToken cancellation = default)
        => Task.FromResult(Validate(instance));

    /// <inheritdoc />
    public ValidationResult Validate(IValidationContext context) => Validate((Customer)context.InstanceToValidate);

    /// <inheritdoc />
    public Task<ValidationResult> ValidateAsync(IValidationContext context, CancellationToken cancellation = default)
        => ValidateAsync((Customer)context.InstanceToValidate, cancellation);

    /// <inheritdoc />
    public IValidatorDescriptor CreateDescriptor() => null!;

    /// <inheritdoc />
    public bool CanValidateInstancesOfType(Type type) => type == typeof(Customer);
}

/// <summary>
/// Tests for the FluentValidation adapter.
/// </summary>
public class FluentValidationTests
{
    /// <summary>
    /// GIVEN a valid object
    /// WHEN it is validated synchronously
    /// THEN the result MUST be a success carrying the same instance.
    /// </summary>
    [Fact]
    public void ValidObject_ShouldBeSuccess()
    {
        var customer = new Customer { Name = "Ada", Age = 36 };

        var (isSuccess, value, errors) = customer.Validate(new CustomerValidator());

        isSuccess.Should().BeTrue();
        value.Should().BeSameAs(customer);
        errors.Should().BeNull();
    }

    /// <summary>
    /// GIVEN an object violating two rules
    /// WHEN it is validated synchronously
    /// THEN the result MUST carry one issue per failure, named after the property, with the
    /// validator's message and a machine-readable code.
    /// </summary>
    [Fact]
    public void InvalidObject_ShouldReportEveryFailure()
    {
        var customer = new Customer { Name = null, Age = 10 };

        var (isSuccess, _, errors) = customer.Validate(new CustomerValidator());

        isSuccess.Should().BeFalse();
        errors.Should().NotBeNull();
        errors!.Issues.Select(i => i.Field).Should().BeEquivalentTo(["Name", "Age"]);
        errors.Issues.Should().Contain(i => i.Message == "Name is required.");
        errors.Issues.Should().Contain(i => i.Message == "Age must be 18 or older.");
        errors.Issues.Should().OnlyContain(i => !string.IsNullOrWhiteSpace(i.Code));
    }

    /// <summary>
    /// GIVEN an object failing two rules on the same property
    /// WHEN it is validated
    /// THEN both failures MUST be preserved, proving nothing is deduplicated or truncated.
    /// </summary>
    [Fact]
    public void RepeatedFailures_ShouldNotBeDeduplicated()
    {
        var customer = new Customer { Name = string.Empty, Age = 36 };

        var (_, _, errors) = customer.Validate(new TwoRuleValidator());

        errors!.Issues.Should().HaveCount(2);
        errors.Issues.Should().OnlyContain(i => i.Field == "Name");
        errors.Issues.Select(i => i.Message).Should().Equal("first", "second");
    }

    /// <summary>
    /// GIVEN a validator with asynchronous rules
    /// WHEN it is invoked through the synchronous overload
    /// THEN FluentValidation's refusal MUST surface rather than being swallowed, so a caller never
    /// silently gets an unvalidated success.
    /// </summary>
    [Fact]
    public void AsyncRule_InvokedSynchronously_ShouldThrow()
    {
        var act = () => new Customer { Name = "Ada", Age = 36 }.Validate(new AsyncCustomerValidator());

        act.Should().Throw<AsyncValidatorInvokedSynchronouslyException>();
    }

    /// <summary>
    /// GIVEN a valid object
    /// WHEN it is validated asynchronously
    /// THEN the result MUST be a success carrying the same instance.
    /// </summary>
    [Fact]
    public async Task ValidObject_ValidateAsync_ShouldBeSuccess()
    {
        var customer = new Customer { Name = "Ada", Age = 36 };

        var (isSuccess, value, errors) =
            await customer.ValidateAsync(new AsyncCustomerValidator(), TestContext.Current.CancellationToken);

        isSuccess.Should().BeTrue();
        value.Should().BeSameAs(customer);
        errors.Should().BeNull();
    }

    /// <summary>
    /// GIVEN an invalid object
    /// WHEN it is validated asynchronously
    /// THEN the result MUST carry every failure, including the one produced by an asynchronous rule.
    /// </summary>
    [Fact]
    public async Task InvalidObject_ValidateAsync_ShouldReportEveryFailure()
    {
        var customer = new Customer { Name = "taken", Age = 10 };

        var (isSuccess, _, errors) =
            await customer.ValidateAsync(new AsyncCustomerValidator(), TestContext.Current.CancellationToken);

        isSuccess.Should().BeFalse();
        errors!.Issues.Select(i => i.Field).Should().BeEquivalentTo(["Name", "Age"]);
        errors.Issues.Should().Contain(i => i.Message == "Name is already taken.");
        errors.Issues.Should().Contain(i => i.Message == "Age must be 18 or older.");
    }

    /// <summary>
    /// GIVEN a cancellation token
    /// WHEN the object is validated asynchronously
    /// THEN the token MUST reach the asynchronous rule, proving it is forwarded rather than dropped.
    /// </summary>
    [Fact]
    public async Task ValidateAsync_ShouldForwardTheCancellationToken()
    {
        using var source = new CancellationTokenSource();
        var validator = new AsyncCustomerValidator();

        await new Customer { Name = "Ada", Age = 36 }.ValidateAsync(validator, source.Token);

        validator.ObservedToken.Should().Be(source.Token);
    }

    /// <summary>
    /// GIVEN an already-cancelled token
    /// WHEN the object is validated asynchronously
    /// THEN the cancellation MUST surface instead of being turned into a validation failure.
    /// </summary>
    [Fact]
    public async Task ValidateAsync_WithCancelledToken_ShouldThrow()
    {
        using var source = new CancellationTokenSource();
        await source.CancelAsync();

        var act = () => new Customer { Name = "Ada", Age = 36 }
            .ValidateAsync(new AsyncCustomerValidator(), source.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    /// <summary>
    /// GIVEN a hand-written validator that reports a failure with no property name
    /// WHEN the object is validated
    /// THEN the issue MUST land under the empty field name, so the resulting map never carries a
    /// null key.
    /// </summary>
    [Fact]
    public void NullPropertyName_ShouldLandUnderTheEmptyKey()
    {
        var (isSuccess, _, errors) = new Customer { Name = "Ada", Age = 36 }
            .Validate(new NullPropertyValidator());

        isSuccess.Should().BeFalse();
        errors!.Issues.Should().ContainSingle();
        errors.Issues[0].Field.Should().BeEmpty();
        errors.Issues[0].Code.Should().Be("Custom");
        errors.ToDictionary().Should().ContainKey(string.Empty);
    }

    /// <summary>
    /// GIVEN a <c>null</c> validator
    /// WHEN either overload is called
    /// THEN it MUST throw <see cref="ArgumentNullException"/>.
    /// </summary>
    [Fact]
    public async Task NullValidator_ShouldThrow()
    {
        var customer = new Customer { Name = "Ada", Age = 36 };

        var sync = () => customer.Validate(null!);
        var async = () => customer.ValidateAsync(null!, TestContext.Current.CancellationToken);

        sync.Should().Throw<ArgumentNullException>();
        await async.Should().ThrowAsync<ArgumentNullException>();
    }

    /// <summary>
    /// GIVEN a <c>null</c> instance
    /// WHEN it is validated
    /// THEN it MUST throw <see cref="ArgumentNullException"/> rather than reporting a success.
    /// </summary>
    [Fact]
    public void NullInstance_ShouldThrow()
    {
        Customer? customer = null;

        var act = () => customer!.Validate(new CustomerValidator());

        act.Should().Throw<ArgumentNullException>();
    }
}
