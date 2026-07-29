"""Inspect the actual response headers for the framework assets the
runtime downloads, especially the .br variants. The .NET 11
preview/ServeUnknownFileTypes path might be missing Content-Type for
.br files, which would make the browser refuse to decode them.
"""
import sys
from playwright.sync_api import sync_playwright

URL_BASE = "http://localhost:5291/"

SAMPLE_PATHS = [
    "/_framework/Microsoft.Extensions.Hosting.Abstractions.o74pq60xl6.wasm",
    "/_framework/Microsoft.Extensions.Hosting.Abstractions.o74pq60xl6.wasm.br",
    "/_framework/Microsoft.Extensions.Hosting.Abstractions.o74pq60xl6.wasm.gz",
    "/_framework/dotnet.js",
    "/_framework/dotnet.js.br",
    "/_framework/Cardscape.Web.g6m5ys90n3.wasm",
    "/_framework/Cardscape.Web.g6m5ys90n3.wasm.br",
    "/_framework/blazor.webassembly.js",
    "/_framework/blazor.webassembly.js.br",
]

def main():
    with sync_playwright() as p:
        browser = p.chromium.launch(
            headless=True,
            executable_path=r"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe",
        )
        ctx = browser.new_context()
        page = ctx.new_page()

        results = []
        for path in SAMPLE_PATHS:
            url = URL_BASE.rstrip("/") + path
            try:
                resp = page.request.get(url, timeout=10000)
                results.append({
                    "path": path,
                    "status": resp.status,
                    "content_type": resp.headers.get("content-type", "<missing>"),
                    "content_encoding": resp.headers.get("content-encoding", "<missing>"),
                    "content_length": resp.headers.get("content-length", "<missing>"),
                    "vary": resp.headers.get("vary", "<missing>"),
                })
            except Exception as ex:
                results.append({"path": path, "error": str(ex)})

        # Also navigate the actual app and grab the headers the
        # browser sees for the .wasm.br files it requests.
        runtime_headers = []
        def on_response(resp):
            if "/_framework/" in resp.url and (".br" in resp.url or ".wasm" in resp.url or ".js" in resp.url):
                try:
                    runtime_headers.append({
                        "url": resp.url,
                        "status": resp.status,
                        "content_type": resp.headers.get("content-type", "<missing>"),
                        "content_encoding": resp.headers.get("content-encoding", "<missing>"),
                        "content_length": resp.headers.get("content-length", "<missing>"),
                    })
                except Exception:
                    pass

        page.on("response", on_response)
        page.goto(URL_BASE, wait_until="domcontentloaded", timeout=30000)
        page.wait_for_timeout(8000)  # let the runtime boot
        browser.close()

    print("=== DIRECT REQUESTS ===")
    for r in results:
        print(json.dumps(r))

    print(f"\n=== RUNTIME-DRIVEN REQUESTS (n={len(runtime_headers)}) ===")
    # Show unique (url, status, content_type, content_encoding) tuples
    seen = set()
    for r in runtime_headers:
        key = (r["url"], r["status"], r["content_type"], r["content_encoding"])
        if key in seen:
            continue
        seen.add(key)
        print(json.dumps(r))

    return 0

if __name__ == "__main__":
    import json
    sys.exit(main())
