using Microsoft.Extensions.Configuration;
using Olve.Engine3D.Input.Actions;

namespace Olve.Engine3D.Platform.Desktop;

/// <summary>
/// Reads <see cref="InputBindingOverrides"/> from the <c>Input:Bindings</c> configuration section: a key per action
/// name, holding one binding or a list of them in <see cref="InputBindingParser"/>'s text, e.g.
/// <c>"RotateToolClockwise": ["Key.T", "Key.Y"]</c>. An empty value leaves the action unbound.
/// </summary>
public static class InputBindingConfiguration
{
    public const string SectionName = "Input";
    public const string BindingsKey = "Bindings";

    public static Result<InputBindingOverrides> Bind(
        IConfiguration configuration,
        IEnumerable<ButtonAction> buttonActions,
        IEnumerable<DirectionalAction> directionalActions)
    {
        var buttonActionNames = buttonActions.Select(action => action.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var directionalActionNames =
            directionalActions.Select(action => action.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var problems = new List<ResultProblem>();
        var buttonBindings = new Dictionary<string, IReadOnlyList<ButtonBinding>>(StringComparer.OrdinalIgnoreCase);
        var directionalBindings =
            new Dictionary<string, IReadOnlyList<DirectionalBinding>>(StringComparer.OrdinalIgnoreCase);

        foreach (var actionSection in configuration.GetSection(SectionName).GetSection(BindingsKey).GetChildren())
        {
            var actionName = actionSection.Key;

            if (buttonActionNames.Contains(actionName))
            {
                buttonBindings[actionName] = ParseAll(actionSection, InputBindingParser.ParseButtonBinding, problems);
            }
            else if (directionalActionNames.Contains(actionName))
            {
                directionalBindings[actionName] =
                    ParseAll(actionSection, InputBindingParser.ParseDirectionalBinding, problems);
            }
            else
            {
                problems.Add(new ResultProblem("Unknown input action {0}:{1}:{2}, expected one of {3}",
                    SectionName, BindingsKey, actionName,
                    string.Join(", ", buttonActionNames.Concat(directionalActionNames))));
            }
        }

        if (problems.Count > 0)
        {
            return new ResultProblemCollection(problems);
        }

        return new InputBindingOverrides
        {
            ButtonBindings = buttonBindings,
            DirectionalBindings = directionalBindings,
        };
    }

    /// <summary>The section's binding, or its list of bindings; none for an empty value.</summary>
    private static IReadOnlyList<T> ParseAll<T>(
        IConfigurationSection section,
        Func<string, Result<T>> parse,
        List<ResultProblem> problems)
    {
        var texts = section.Value is { } value
            ? new[] { value }
            : section.GetChildren().Select(child => child.Value ?? string.Empty).ToArray();

        var bindings = new List<T>();
        foreach (var text in texts.Where(text => !string.IsNullOrWhiteSpace(text)))
        {
            if (parse(text).TryPickProblems(out var bindingProblems, out var binding))
            {
                problems.Add(new ResultProblem("Invalid binding for {0}:{1}:{2}", SectionName, BindingsKey, section.Key));
                problems.AddRange(bindingProblems);
                continue;
            }

            bindings.Add(binding);
        }

        return bindings;
    }
}
