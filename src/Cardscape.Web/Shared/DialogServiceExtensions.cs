using Microsoft.Extensions.Localization;
using Radzen;

namespace Cardscape.Web.Shared;

public static class DialogServiceExtensions
{
    extension(DialogService dialogs)
    {
        /// <summary>
        /// Radzen's confirm dialog with the standard Delete / Cancel buttons.
        /// True only when the user confirmed.
        /// </summary>
        public async Task<bool> ConfirmDeleteAsync(IStringLocalizer localizer, string message, string title) =>
            await dialogs.Confirm(
                message,
                title,
                new ConfirmOptions { OkButtonText = localizer["ActionDelete"], CancelButtonText = localizer["ActionCancel"] })
            == true;
    }
}
