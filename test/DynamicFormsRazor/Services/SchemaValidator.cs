using DynamicFormsRazor.Models;
using System.Text.RegularExpressions;

namespace DynamicFormsRazor.Services;

public class SchemaValidator
{
    public Dictionary<string, string> Validate(FormDefinition def, Dictionary<string, string?> input)
    {
        var errors = new Dictionary<string, string>();

        foreach (var section in def.Sections.OrderBy(s => s.Order))
        {
            foreach (var field in section.Fields)
            {
                input.TryGetValue(field.Name, out var value);
                var label = string.IsNullOrWhiteSpace(field.Label) ? field.Name : field.Label;
                var v = field.Validation ?? new ValidationRule();

                // Required
                if (v.Required && string.IsNullOrWhiteSpace(value))
                {
                    errors[field.Name] = v.CustomMessage ?? $"{label} is required.";
                    continue;
                }

                if (string.IsNullOrWhiteSpace(value))
                    continue; // nothing else to check

                // MinLength/MaxLength
                if (v.MinLength.HasValue && value!.Length < v.MinLength.Value)
                    errors[field.Name] = v.CustomMessage ?? $"{label} must be at least {v.MinLength} characters.";
                if (!errors.ContainsKey(field.Name) && v.MaxLength.HasValue && value!.Length > v.MaxLength.Value)
                    errors[field.Name] = v.CustomMessage ?? $"{label} must be at most {v.MaxLength} characters.";

                // Numeric ranges
                if (!errors.ContainsKey(field.Name) && (v.Min.HasValue || v.Max.HasValue) &&
                    (field.Type == FieldType.Number))
                {
                    if (double.TryParse(value, out var num))
                    {
                        if (v.Min.HasValue && num < v.Min.Value)
                            errors[field.Name] = v.CustomMessage ?? $"{label} must be ≥ {v.Min}.";
                        if (!errors.ContainsKey(field.Name) && v.Max.HasValue && num > v.Max.Value)
                            errors[field.Name] = v.CustomMessage ?? $"{label} must be ≤ {v.Max}.";
                    }
                    else
                    {
                        errors[field.Name] = v.CustomMessage ?? $"{label} must be a number.";
                    }
                }

                // Email
                if (!errors.ContainsKey(field.Name) && field.Type == FieldType.Email)
                {
                    try
                    {
                        var addr = new System.Net.Mail.MailAddress(value!);
                        if (addr.Address != value) throw new Exception();
                    }
                    catch
                    {
                        errors[field.Name] = v.CustomMessage ?? $"{label} must be a valid email address.";
                    }
                }

                // Pattern
                if (!errors.ContainsKey(field.Name) && !string.IsNullOrWhiteSpace(v.Pattern))
                {
                    if (!Regex.IsMatch(value!, v.Pattern!))
                        errors[field.Name] = v.CustomMessage ?? $"{label} format is invalid.";
                }

                // Options validation for Select/Radio
                if (!errors.ContainsKey(field.Name) && (field.Type == FieldType.Select || field.Type == FieldType.Radio))
                {
                    if (field.Options is { Length: > 0 } && !field.Options.Contains(value!))
                        errors[field.Name] = v.CustomMessage ?? $"{label} must be one of the allowed options.";
                }
            }
        }

        return errors;
    }
}
