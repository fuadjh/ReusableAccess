using System.ComponentModel.DataAnnotations;
using ReusableAccess.Contracts;
using ReusableAccess.Web.Ui;
using Assert = Xunit.Assert;

namespace ReusableAccess.Web.Tests;

public sealed class ManagedUserPresentationTests
{
    [Fact]
    public void Editor_is_a_copy_not_the_list_item()
    {
        var original = Row();
        var model = ManagedUserEditorModel.From(original);

        model.FirstName = "Changed";

        Assert.Equal("Alice", original.FirstName);
        Assert.False(model.IsNew);
        Assert.True(model.NationalIdReadOnly);
        Assert.Equal(
            "stamp",
            model.ToUpdate(original.ConcurrencyStamp).ConcurrencyStamp);
    }

    [Fact]
    public void Creation_requires_a_temporary_password()
    {
        var model = ManagedUserEditorModel.From(null);
        model.FirstName = "Alice";
        model.LastName = "Tester";
        model.OrganizationalCode = "ORG-1";
        model.NationalId = "0012345679";
        model.Email = "alice@example.test";
        model.Mobile = "09123456789";

        var errors = new List<ValidationResult>();

        Assert.False(Validator.TryValidateObject(
            model, new ValidationContext(model), errors, true));

        Assert.Contains(
            errors,
            error => error.MemberNames.Contains(nameof(model.Password)));
    }

    [Fact]
    public void Validation_problem_errors_are_read()
    {
        const string json = """
            {
              "title": "Validation failed",
              "errors": {
                "FirstName": ["نام الزامی است."],
                "Password": ["رمز کوتاه است."]
              }
            }
            """;

        var message = ApiProblemText.Read(json, "fallback");

        Assert.Contains("نام الزامی است.", message);
        Assert.Contains("رمز کوتاه است.", message);
    }

    [Fact]
    public void Non_json_error_uses_safe_fallback()
    {
        Assert.Equal(
            "fallback",
            ApiProblemText.Read("<html>internal failure</html>", "fallback"));
    }

    private static UserRow Row() => new()
    {
        UserId = Guid.NewGuid(),
        FirstName = "Alice",
        LastName = "Tester",
        DisplayName = "Alice Tester",
        OrganizationalCode = "ORG-1",
        NationalId = "0012345679",
        UserName = "0012345679",
        Email = "alice@example.test",
        Mobile = "+989123456789",
        ConcurrencyStamp = "stamp"
    };
}