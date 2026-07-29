"""
Reproduce exactly what a real user does:
  - Default browser (no cache bypass; uses HTTP cache like a real browser)
  - Open localhost:5291/
  - Wait long enough to capture the full boot
  - Capture EVERY console message (info, log, warn, error, debug)
  - Capture every network request including responses that have non-error
    status codes
  - Capture pageerror events
  - Capture a screenshot when the page "settles" (either loaded or stuck)

This is the diagnostic the user is running in their own browser.
"""
import json
import time
from playwright.sync_api import sync_playwright

URL = "http://localhost:5291/"
SCREENSHOT_DIR = r"C:\Users\Usuario\AppData\Local\Temp"
SETTLE_MS = 30000  # wait up to 30s for the app to settle

def main():
    with sync_playwright() as p:
        # Use a real-looking user agent. No cache bypass. This is
        # exactly what the user's browser does.
        browser = p.chromium.launch(
            headless=True,
            executable_path=r"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe",
        )
        ctx = browser.new_context(
            user_agent=("Mozilla/5.0 (Windows NT 10.0; Win64; x64) "
                        "AppleWebKit/537.36 (KHTML, like Gecko) "
                        "Chrome/120.0.0.0 Safari/537.36 Edg/120.0.0.0"),
            viewport={"width": 1280, "height": 800},
        )
        page = ctx.new_page()

        all_console = []
        all_responses = []
        all_pageerror = []

        page.on("console", lambda msg: all_console.append({
            "t": time.time(),
            "type": msg.type,
            "text": msg.text[:200],
            "location": msg.location,
        }))
        page.on("pageerror", lambda err: all_pageerror.append({
            "t": time.time(),
            "message": str(err)[:300],
        }))
        def on_resp(resp):
            all_responses.append({
                "t": time.time(),
                "url": resp.url,
                "status": resp.status,
            })
        page.on("response", on_resp)

        # Navigate.
        page.goto(URL, wait_until="domcontentloaded", timeout=30000)

        # Wait the full settle time and sample progress.
        progress_samples = []
        t0 = time.time()
        while (time.time() - t0) * 1000 < SETTLE_MS:
            try:
                state = page.evaluate("""() => {
                    const spt = document.querySelector('.loading-progress-text');
                    const err = document.getElementById('blazor-error-ui');
                    const app = document.getElementById('app');
                    return {
                        spText: spt ? spt.textContent.trim() : null,
                        errVisible: err && getComputedStyle(err).display !== 'none'
                                     && err.getBoundingClientRect().width > 0,
                        appText: app ? app.innerText.trim().slice(0, 120) : '',
                    };
                }""")
            except Exception as ex:
                state = {"_err": str(ex)}
            progress_samples.append({"t_ms": int((time.time() - t0) * 1000), **state})
            if not state.get("errVisible") and state.get("appText") and len(state["appText"]) > 5:
                # Looks like the app rendered. Wait a bit more then break.
                page.wait_for_timeout(2000)
                break
            page.wait_for_timeout(300)

        # Final screenshot.
        ss_path = f"{SCREENSHOT_DIR}\\cardscape-userlike.png"
        page.screenshot(path=ss_path, full_page=True)

        # Final state
        try:
            final = page.evaluate("""() => {
                const spt = document.querySelector('.loading-progress-text');
                const err = document.getElementById('blazor-error-ui');
                const app = document.getElementById('app');
                return {
                    spText: spt ? spt.textContent.trim() : null,
                    errVisible: err && getComputedStyle(err).display !== 'none'
                                 && err.getBoundingClientRect().width > 0,
                    errText: err ? err.textContent.trim() : '',
                    appText: app ? app.innerText.trim().slice(0, 500) : '',
                };
            }""")
        except Exception as ex:
            final = {"_err": str(ex)}

        browser.close()

    # Summarise.
    summary = {
        "settle_budget_ms": SETTLE_MS,
        "final_state": final,
        "console_total": len(all_console),
        "console_by_type": {},
        "pageerror_count": len(all_pageerror),
        "responses_total": len(all_responses),
        "responses_by_status": {},
        "unique_404_urls": [],
        "last_5_console": [],
    }
    for c in all_console:
        summary["console_by_type"][c["type"]] = summary["console_by_type"].get(c["type"], 0) + 1
    for r in all_responses:
        s = str(r["status"])
        summary["responses_by_status"][s] = summary["responses_by_status"].get(s, 0) + 1
    seen = set()
    for r in all_responses:
        if r["status"] == 404 and r["url"] not in seen:
            seen.add(r["url"])
            summary["unique_404_urls"].append(r["url"])
    summary["last_5_console"] = all_console[-5:]

    print("=== SUMMARY ===")
    print(json.dumps(summary, indent=2, default=str))

    print(f"\n=== CONSOLE messages (showing ALL {len(all_console)}) ===")
    for c in all_console:
        loc = c.get("location") or {}
        locstr = f"{loc.get('url', '').split('/')[-1]}:{loc.get('lineNumber', '')}" if loc else ""
        print(f"  [{c['type']:>7}] {c['text']}  @ {locstr}")

    print(f"\n=== PAGE ERRORS ({len(all_pageerror)}) ===")
    for e in all_pageerror:
        print(json.dumps(e, default=str))

    print(f"\n=== UNIQUE 404 URLs ({len(summary['unique_404_urls'])}) ===")
    for u in summary["unique_404_urls"]:
        print(f"  {u}")

    return 0

if __name__ == "__main__":
    raise SystemExit(main())
