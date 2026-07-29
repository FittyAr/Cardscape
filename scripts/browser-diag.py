"""
Headless Playwright diag that WAITS for the Blazor loading spinner to
actually disappear (or the blazor-error-ui to appear) instead of
relying on arbitrary timeouts. Captures a screenshot at the end so we
can see what the user actually sees.
"""
import json
import sys
from playwright.sync_api import sync_playwright

URL_BASE = "http://localhost:5291/"
NAV_TIMEOUT_MS = 30000
# How long to wait for the loading spinner to clear (or the error UI
# to appear). Tuned generously: a healthy first paint is <5s; if we
# need >20s the app is definitely broken.
WAIT_FOR_APP_MS = 25000
SCREENSHOT_DIR = r"C:\Users\Usuario\AppData\Local\Temp"

def wait_for_app_or_error(page, label):
    """Wait until the loading spinner is gone OR the error UI is shown.

    Returns ('loaded', ms) / ('error', ms) / ('timeout', ms).
    """
    import time
    start = time.time()
    while (time.time() - start) * 1000 < WAIT_FOR_APP_MS:
        try:
            state = page.evaluate("""() => {
                const app = document.getElementById('app');
                const err = document.getElementById('blazor-error-ui');
                const spinner = app ? app.querySelector('.loading-progress') : null;
                const spinnerVisible = spinner && getComputedStyle(spinner).display !== 'none'
                                       && spinner.getBoundingClientRect().width > 0;
                const errVisible = err && getComputedStyle(err).display !== 'none'
                                   && err.textContent.trim().length > 0
                                   && !err.textContent.includes('An unhandled error has occurred') === false
                                   && (err.getBoundingClientRect().width > 0);
                // App is "loaded enough" if the spinner is hidden and there's
                // some non-empty content in #app that's not just the spinner.
                const appHasContent = app && app.children.length > 0
                                      && app.innerText.trim().length > 0
                                      && !spinnerVisible;
                return { spinnerVisible, errVisible, appHasContent,
                         errText: err ? err.textContent.trim() : '',
                         appChildCount: app ? app.children.length : 0,
                         appText: app ? app.innerText.trim().slice(0, 200) : '' };
            }""")
        except Exception as ex:
            state = {"_eval_error": str(ex)}

        if state.get("errVisible"):
            return ("error", int((time.time() - start) * 1000), state)
        if state.get("appHasContent"):
            return ("loaded", int((time.time() - start) * 1000), state)
        page.wait_for_timeout(250)
    return ("timeout", WAIT_FOR_APP_MS, state if 'state' in locals() else {})

def main() -> int:
    findings = {
        "console": [],
        "pageerror": [],
        "requestfailed": [],
        "responses": [],
        "route_results": [],
    }

    with sync_playwright() as p:
        browser = p.chromium.launch(
            headless=True,
            executable_path=r"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe",
        )
        ctx = browser.new_context()
        page = ctx.new_page()

        page.on("console", lambda msg: findings["console"].append({
            "type": msg.type,
            "text": msg.text,
            "url": msg.location.get("url") if msg.location else None,
        }))
        page.on("pageerror", lambda err: findings["pageerror"].append({
            "message": str(err),
        }))
        page.on("requestfailed", lambda req: findings["requestfailed"].append({
            "url": req.url,
            "method": req.method,
            "failure": req.failure,
        }))
        page.on("response", lambda resp: (
            findings["responses"].append({"url": resp.url, "status": resp.status})
            if resp.status >= 400 else None
        ))

        # Visit the root and wait properly.
        page.goto(URL_BASE, wait_until="domcontentloaded", timeout=NAV_TIMEOUT_MS)
        outcome, ms, final_state = wait_for_app_or_error(page, "root")
        ss_path = f"{SCREENSHOT_DIR}\\cardscape-diag-root.png"
        page.screenshot(path=ss_path, full_page=True)
        findings["route_results"].append({
            "route": "/",
            "outcome": outcome,
            "waited_ms": ms,
            "final_state": final_state,
            "screenshot": ss_path,
        })

        # Try the login route too — it should redirect/render without
        # an API token.
        page.goto(URL_BASE.rstrip("/") + "/login", wait_until="domcontentloaded", timeout=NAV_TIMEOUT_MS)
        outcome, ms, final_state = wait_for_app_or_error(page, "login")
        ss_path = f"{SCREENSHOT_DIR}\\cardscape-diag-login.png"
        page.screenshot(path=ss_path, full_page=True)
        findings["route_results"].append({
            "route": "/login",
            "outcome": outcome,
            "waited_ms": ms,
            "final_state": final_state,
            "screenshot": ss_path,
        })

        browser.close()

    # Summarise.
    summary = {
        "console_errors": sum(1 for c in findings["console"] if c["type"] == "error"),
        "console_warnings": sum(1 for c in findings["console"] if c["type"] == "warning"),
        "pageerror_count": len(findings["pageerror"]),
        "requestfailed_count": len(findings["requestfailed"]),
        "bad_response_count": len(findings["responses"]),
        "route_outcomes": [(r["route"], r["outcome"], r["waited_ms"]) for r in findings["route_results"]],
    }
    print("=== SUMMARY ===")
    print(json.dumps(summary, indent=2))

    print("\n=== ROUTE RESULTS ===")
    for r in findings["route_results"]:
        print(json.dumps(r, indent=2, default=str))

    print("\n=== PAGEERRORS (first 20) ===")
    for e in findings["pageerror"][:20]:
        print(json.dumps(e, ensure_ascii=False))

    print("\n=== REQUEST FAILED (first 20) ===")
    for e in findings["requestfailed"][:20]:
        print(json.dumps(e, ensure_ascii=False))

    print("\n=== 4xx/5xx (first 20, unique URLs only) ===")
    seen = set()
    for e in findings["responses"]:
        if e["url"] in seen:
            continue
        seen.add(e["url"])
        print(json.dumps(e, ensure_ascii=False))
        if len(seen) >= 20:
            break

    print("\n=== CONSOLE errors (first 30, unique) ===")
    seen = set()
    for c in findings["console"]:
        if c["type"] != "error":
            continue
        key = c["text"]
        if key in seen:
            continue
        seen.add(key)
        print(f"[{c['type']}] {c['text']}")
        if len(seen) >= 30:
            break

    return 0

if __name__ == "__main__":
    sys.exit(main())
