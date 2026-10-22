using System.Windows;
using Microsoft.VisualBasic;
using ObdLink.Core.Safety;

namespace ObdLink.App;

/// <summary>Multi-step confirmation via message boxes; typed phrases use a simple input prompt.</summary>
public sealed class WpfConfirmationProvider(Window owner) : IConfirmationProvider
{
    public Task<bool> ConfirmAsync(ConfirmationStep step, CancellationToken ct = default)
    {
        if (step.TypedPhrase is null)
            return Task.FromResult(MessageBox.Show(owner, step.Message, step.Title, MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes);
        var typed = Interaction.InputBox(step.Message, step.Title, "");
        return Task.FromResult(typed == step.TypedPhrase);
    }
}
