using System.Text.Json;

namespace ReusableAccess.Web.Ui;

public static class ApiProblemText
{
    public static string Read(string text, string fallback)
    {
        try
        {
            using var document = JsonDocument.Parse(text);
            var root = document.RootElement;

            if (root.ValueKind == JsonValueKind.String)
                return NonEmpty(root.GetString(), fallback);

            if (root.ValueKind != JsonValueKind.Object)
                return fallback;

            if (root.TryGetProperty("errors", out var errors) &&
                errors.ValueKind == JsonValueKind.Object)
            {
                var messages = new List<string>();

                foreach (var property in errors.EnumerateObject())
                {
                    if (property.Value.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var item in property.Value.EnumerateArray())
                        {
                            if (item.ValueKind == JsonValueKind.String &&
                                !string.IsNullOrWhiteSpace(item.GetString()))
                            {
                                messages.Add(item.GetString()!);
                            }
                        }
                    }
                    else if (property.Value.ValueKind == JsonValueKind.String &&
                             !string.IsNullOrWhiteSpace(property.Value.GetString()))
                    {
                        messages.Add(property.Value.GetString()!);
                    }
                }

                if (messages.Count > 0)
                    return string.Join(" ", messages.Distinct());
            }

            foreach (var name in new[] { "detail", "message", "title" })
            {
                if (root.TryGetProperty(name, out var value) &&
                    value.ValueKind == JsonValueKind.String &&
                    !string.IsNullOrWhiteSpace(value.GetString()))
                {
                    return value.GetString()!;
                }
            }
        }
        catch (JsonException)
        {
            // پاسخ غیر JSON نباید موجب نمایش HTML یا exception داخلی شود.
        }

        return fallback;
    }

    private static string NonEmpty(string? value, string fallback)
        => string.IsNullOrWhiteSpace(value) ? fallback : value;
}