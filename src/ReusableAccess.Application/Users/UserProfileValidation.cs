using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using ReusableAccess.Application.Errors;
using ReusableAccess.Contracts;
using ReusableAccess.Domain.Identity;

namespace ReusableAccess.Application.Users;

public static class UserProfileValidation
{
    public static void Profile(UserProfileInput input)
    {
        var errors = new List<ValidationResult>();

        if (!Validator.TryValidateObject(
                input,
                new ValidationContext(input),
                errors,
                true))
        {
            throw new UseCaseException(
                400,
                string.Join(
                    " ",
                    errors.Select(error => error.ErrorMessage)));
        }

        if ($"{input.FirstName.Trim()} {input.LastName.Trim()}".Length > 200)
        {
            throw new UseCaseException(
                400,
                "مجموع نام و نام خانوادگی بیش از حد مجاز است.");
        }

        var mobile = IdentityRules.NormalizeMobile(input.Mobile);

        if (!Regex.IsMatch(mobile, @"^\+[1-9][0-9]{7,14}$"))
        {
            throw new UseCaseException(
                400,
                "شماره موبایل معتبر نیست.");
        }

        if (string.IsNullOrWhiteSpace(input.FirstName) ||
            string.IsNullOrWhiteSpace(input.LastName) ||
            string.IsNullOrWhiteSpace(input.OrganizationalCode))
        {
            throw new UseCaseException(
                400,
                "همهٔ مشخصات الزامی‌اند.");
        }
    }
}