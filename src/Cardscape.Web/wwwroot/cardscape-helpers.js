// Cardscape Web — small browser-side helpers used by Blazor
// pages. Loaded as a regular script (no ES module, no
// bundler) so the wasm runtime can call it via IJSRuntime
// without an interop wrapper.
//
// The `cardscape` global is a small namespace we add
// to the existing window. Future helpers (e.g. clipboard,
// debounce, focus helpers) can hang off the same object.

window.cardscape = window.cardscape || {};

// downloadTextFile: writes `content` to a file and triggers
// a browser download. The MIME type is passed through so
// the same helper serves CSV (text/csv) and JSON
// (application/json) downloads. The temporary anchor is
// removed from the DOM after the click so we do not leak
// elements across downloads.
window.cardscape.downloadTextFile = function (fileName, contentType, content) {
    if (!fileName || typeof fileName !== 'string') {
        throw new Error('downloadTextFile: fileName is required');
    }
    var blob = new Blob([content ?? ''], { type: contentType || 'application/octet-stream' });
    var url = URL.createObjectURL(blob);
    var anchor = document.createElement('a');
    anchor.href = url;
    anchor.download = fileName;
    anchor.style.display = 'none';
    document.body.appendChild(anchor);
    anchor.click();
    document.body.removeChild(anchor);
    // Revoke the URL after a short delay so the browser
    // has time to start the download. Revoking synchronously
    // on some browsers aborts the in-flight download.
    setTimeout(function () { URL.revokeObjectURL(url); }, 1000);
};
