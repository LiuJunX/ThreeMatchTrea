using System.Threading.Tasks;

namespace Match3.Editor.Interfaces;

/// <summary>
/// Abstraction for UI interactions (dialogs, clipboard, etc.)
/// </summary>
public interface IPlatformService
{
    Task ShowAlertAsync(string message);
    Task ShowAlertAsync(string title, string message);
    Task<bool> ConfirmAsync(string title, string message);
    Task<string> PromptAsync(string title, string defaultValue = "");
    Task CopyToClipboardAsync(string text);
}
