"""
Diagnostic that mirrors a real user more closely:
  - Polls the Blazor loading-progress text every 250ms while booting
  - Tracks how long each /_framework request takes
  - Flags any request that started but never finished
  - Captures the DOM state at multiple points (boot mid, after settle)
"""
import json
import time
from playwright.sync_api import sync_playwright

URL_BASE = "http://localhost:5291/"
TOTAL_BUDGET_S = 60
SCREENSHOT_DIR = r"C:\Users\Usuario\AppData\Local\Temp"

def main():
    findings = {
        "progress_samples": [],
        "in_flight_requests": [],
        "completed_requests": [],
        "console_errors": [],
        "pageerror": [],
    }

    with sync_playwright() as p:
        browser = p.chromium.launch(
            headless=True,
            executable_path=r"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe",
        )
        # Fresh context = no cache, like a real cold load.
        ctx = browser.new_context()
        page = ctx.new_page()

        # Track in-flight requests with timestamps.
        pending = {}

        def on_request(req):
            if "/_framework/" in req.url or "/api/" in req.url or "/css/" in req.url or "/lib/" in req.url:
                pending[req.url] = time.time()

        def on_response(resp):
            url = resp.url
            if url in pending:
                duration_ms = int((time.time() - pending.pop(url)) * 1000)
                findings["completed_requests"].append({
                    "url": url,
                    "status": resp.status,
                    "duration_ms": duration_ms,
                })
            elif "/_framework/" in url or "/api/" in url:
                findings["completed_requests"].append({
                    "url": url,
                    "status": resp.status,
                    "duration_ms": -1,  # didn't see the request
                })

        def on_failed(req):
            url = req.url
            if url in pending:
                duration_ms = int((time.time() - pending.pop(url)) * 1000)
                findings["in_flight_requests"].append({
                    "url": url, "duration_ms": duration_ms, "failure": req.failure,
                })

        page.on("request", on_request)
        page.on("response", on_response)
        page.on("requestfailed", on_failed)
        page.on("console", lambda msg: (
            findings["console_errors"].append({"type": msg.type, "text": msg.text})
            if msg.type in ("error", "warning") else None
        ))
        page.on("pageerror", lambda err: findings["pageerror"].append({"message": str(err)}))

        # Navigate and start the polling loop.
        page.goto(URL_BASE, wait_until="domcontentloaded", timeout=30000)
        t0 = time.time()

        while time.time() - t0 < TOTAL_BUDGET_S:
            try:
                state = page.evaluate("""() => {
                    const sp = document.querySelector('.loading-progress');
                    const spt = document.querySelector('.loading-progress-text');
                    const err = document.getElementById('blazor-error-ui');
                    const errVisible = err && getComputedStyle(err).display !== 'none'
                                       && err.getBoundingClientRect().width > 0;
                    const spText = spt ? spt.textContent.trim() : null;
                    const spVisible = sp && getComputedStyle(sp).display !== 'none'
                                      && sp.getBoundingClientRect().width > 0;
                    const app = document.getElementById('app');
                    const appText = app ? app.innerText.trim().slice(0, 100) : '';
                    return { spText, spVisible, errVisible, appText };
                }""")
            except Exception as ex:
                state = {"_eval_error": str(ex)}

            elapsed = int((time.time() - t0) * 1000)
            findings["progress_samples"].append({
                "t_ms": elapsed,
                **state,
            })

            if state.get("errVisible"):
                break
            # Loaded = spinner gone + app has content
            if not state.get("spVisible") and state.get("appText") and len(state["appText"]) > 5:
                # One more sample then break
                page.wait_for_timeout(500)
                continue
            page.wait_for_timeout(250)

        # Final screenshot.
        page.screenshot(path=f"{SCREENSHOT_DIR}\\cardscape-watch.png", full_page=True)
        findings["final_screenshot"] = f"{SCREENSHOT_DIR}\\cardscape-watch.png"
        findings["final_state"] = state if 'state' in locals() else {}

        browser.close()

    # Report.
    summary = {
        "total_budget_s": TOTAL_BUDGET_S,
        "samples_collected": len(findings["progress_samples"]),
        "completed_request_count": len(findings["completed_requests"]),
        "in_flight_count": len(findings["in_flight_requests"]),
        "console_error_count": len(findings["console_errors"]),
        "pageerror_count": len(findings["pageerror"]),
    }
    print("=== SUMMARY ===")
    print(json.dumps(summary, indent=2))

    print("\n=== PROGRESS TIMELINE (sampled every 250ms) ===")
    for s in findings["progress_samples"]:
        t = s.get("t_ms", 0)
        sp = (s.get("spText") or "")
        vis = "vis" if s.get("spVisible") else "hid"
        err = "ERR" if s.get("errVisible") else "   "
        app = (s.get("appText") or "")[:50].replace("\n", " ")
        print(f"  t={t:>5}ms  spinner='{sp[:10]:<10}'  {vis}  {err}  app='{app}'")

    # Unique progress values to see if it ever advanced.
    progress_values = set(s.get("spText") for s in findings["progress_samples"] if s.get("spText"))
    print(f"\n=== Unique spinner text values seen: {sorted(progress_values)} ===")

    print("\n=== COMPLETED /_framework REQUESTS (slowest first) ===")
    framework_reqs = [r for r in findings["completed_requests"] if "/_framework/" in r["url"]]
    framework_reqs.sort(key=lambda r: -r["duration_ms"])
    for r in framework_reqs[:30]:
        print(f"  {r['status']:>3} {r['duration_ms']:>5}ms  {r['url'].split('/')[-1]}")

    print("\n=== IN-FLIGHT (started but never finished) ===")
    for r in findings["in_flight_requests"]:
        print(json.dumps(r))

    print("\n=== ERRORS (unique, first 15) ===")
    seen = set()
    for e in findings["console_errors"]:
        key = e.get("text", "")
        if key in seen:
            continue
        seen.add(key)
        print(f"[{e.get('type')}] {e.get('text')}")
        if len(seen) >= 15:
            break

    return 0

if __name__ == "__main__":
    raise SystemExit(main())
