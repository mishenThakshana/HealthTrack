using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace HealthTrack.Helpers;

// A checkbox that must be ticked. [Range] on a bool breaks client-side validation, which compares the values as numbers,
// and the unobtrusive "required" rule ignores checkboxes, so the browser check lives in wwwroot/js/validation.js.
public class MustBeTrueAttribute : ValidationAttribute, IClientModelValidator
{
    public override bool IsValid(object? value) => value is true;

    public void AddValidation(ClientModelValidationContext context)
    {
        context.Attributes.TryAdd("data-val", "true");
        context.Attributes.TryAdd("data-val-mustbetrue", FormatErrorMessage(context.ModelMetadata.GetDisplayName()));
    }
}
