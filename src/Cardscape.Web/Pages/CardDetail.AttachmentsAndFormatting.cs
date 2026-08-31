using System.Text.Json;
using Cardscape.Web.Resources;
using Cardscape.Web.Services;
using Cardscape.Web.Services.Api;
using Cardscape.Web.Shared;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Localization;
using Microsoft.JSInterop;
using Radzen;
using Radzen.Blazor;

namespace Cardscape.Web.Pages;

public partial class CardDetail
{
    private static string FieldKindLabel(CustomFieldKind kind) => kind switch
    {
        CustomFieldKind.Text => "Text",
        CustomFieldKind.Number => "Number",
        CustomFieldKind.Date => "Date",
        CustomFieldKind.Dropdown => "Dropdown",
        CustomFieldKind.Checkbox => "Checkbox",
        _ => $"Kind {(int)kind}",
    };

    private static string FormatFieldValue(CustomFieldValueDto value)
    {
        // Values are stored as JSON strings; strip outer quotes for display.
        string raw = value.ValueJson?.Trim('"') ?? string.Empty;
        if (string.IsNullOrEmpty(raw))
        {
            return "(empty)";
        }

        return value.Kind switch
        {
            CustomFieldKind.Checkbox => raw.Equals("true", StringComparison.OrdinalIgnoreCase) ? " Yes" : " No",
            CustomFieldKind.Date => raw, // ISO date; render as-is
            _ => raw,
        };
    }

    private static int ChecklistProgressPercent(ChecklistDto cl) =>
        cl.TotalCount == 0 ? 0 : (int)Math.Round(100.0 * cl.CompletedCount / cl.TotalCount);

    private static string FormatActivityPayload(ActivityDto a)
    {
        if (string.IsNullOrEmpty(a.PayloadJson) || a.PayloadJson == "{}")
        {
            return string.Empty;
        }

        try
        {
            using JsonDocument doc = JsonDocument.Parse(a.PayloadJson);
            if (doc.RootElement.ValueKind == JsonValueKind.Object)
            {
                List<string> parts = [];
                foreach (JsonProperty p in doc.RootElement.EnumerateObject())
                {
                    parts.Add($"{p.Name}: {p.Value}");
                }

                return string.Join(" · ", parts);
            }
        }
        catch
        {
            // fall through
        }

        return a.PayloadJson;
    }

    // BUG-A5-002 — attachment handlers. The upload goes through
    // IAttachmentsApiClient.UploadAsync with a streamed file; the
    // download triggers a browser save via JS interop. Both
    // re-fetch the list on success so the UI stays in lockstep
    // with the server.
    private async Task OnAttachmentSelected(InputFileChangeEventArgs e)
    {
        if (uploadingAttachment) return;
        IBrowserFile file = e.File;
        if (file.Size <= 0) return;
        uploadingAttachment = true;
        try
        {
            await using Stream stream = file.OpenReadStream(maxAllowedSize: 25L * 1024L * 1024L);
            ApiResult<AttachmentDto> result = await Attachments.UploadAsync(
                CardId, stream, file.Name, file.ContentType ?? "application/octet-stream");
            if (result.IsSuccess && attachments is not null)
            {
                attachments = [.. attachments, result.Value!];
            }
        }
        finally
        {
            uploadingAttachment = false;
        }
    }

    private async Task DownloadAttachmentAsync(AttachmentDto attachment)
    {
        ApiResult<byte[]> result = await Attachments.DownloadAsync(CardId, attachment.Id);
        if (!result.IsSuccess || result.Value is null)
        {
            return;
        }
        await JS.InvokeVoidAsync(
            "downloadFromBytes",
            attachment.FileName,
            "application/octet-stream",
            Convert.ToBase64String(result.Value));
    }

    private async Task DeleteAttachmentAsync(AttachmentDto attachment)
    {
        ApiResult<bool> result = await Attachments.DeleteAsync(CardId, attachment.Id);
        if (result.IsSuccess && attachments is not null)
        {
            attachments = attachments.Where(a => a.Id != attachment.Id).ToList();
        }
    }

    private static string FormatSize(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:0.0} KB";
        return $"{bytes / (1024.0 * 1024.0):0.0} MB";
    }
}

