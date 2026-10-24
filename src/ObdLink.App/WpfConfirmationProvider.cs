using System.Windows;
using ObdLink.Core.Safety;

namespace ObdLink.App;

/// <summary>Multi-step confirmation via message boxes; typed phrases use a simple input prompt.</summary>
public sealed class WpfConfirmationProvider(Window owner) : IConfirmationProvider
{
    public Task<bool> ConfirmAsync(ConfirmationStep step, CancellationToken ct = default)
    {
        if (step.TypedPhrase is null)
            return Task.FromResult(DialogService.YesNo(step.Message, step.Title, MessageBoxImage.Warning));
        var typed = DialogService.Prompt(step.Message, step.Title);
        return Task.FromResult(typed == step.TypedPhrase);
    }
}
