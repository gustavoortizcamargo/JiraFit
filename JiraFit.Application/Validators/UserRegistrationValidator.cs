using FluentValidation;
using JiraFit.Application.DTOs;

namespace JiraFit.Application.Validators;

public class UserRegistrationValidator : AbstractValidator<UserRegistrationDto>
{
    public UserRegistrationValidator()
    {
        RuleFor(x => x.PhoneNumber)
            .NotEmpty().WithMessage("Phone Number is required.");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required.")
            .MaximumLength(100);

        RuleFor(x => x.Weight)
            .GreaterThan(0).WithMessage("Weight must be greater than 0.");

        RuleFor(x => x.Height)
            .GreaterThan(0).WithMessage("Height must be greater than 0.");

        RuleFor(x => x.Age)
            .InclusiveBetween(10, 120).WithMessage("Age must be between 10 and 120.");
            
        RuleFor(x => x.Gender)
            .Must(g => g == "Male" || g == "Female").WithMessage("Gender must be 'Male' or 'Female'.");

        RuleFor(x => x.Objective)
            .Must(g => g == "WeightLoss" || g == "MuscleGain" || g == "Maintenance")
            .WithMessage("Objective must be WeightLoss, MuscleGain, or Maintenance.");
    }
}
