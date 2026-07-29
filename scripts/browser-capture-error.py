"""
Capture the actual Blazor error UI content + dump the DOM around #blazor-error-ui
and run a console.error capture for full unhandled error messages.
"""
import json
import time
from playwright.sync_api import sync_playwright

URL = "http://localhost:5291/"

def main():
    with sync_playwright() as p:
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

        errs = []
        page.on("console", lambda msg: errs.append({
            "t": time.time(),
            "type": msg.type,
            "text": msg.text,
            "location": msg.location,
        }) if msg.type in ("error", "warning") else None)
        page.on("pageerror", lambda err: errs.append({
            "t": time.time(),
            "type": "pageerror",
            "text": str(err),
        }))

        page.goto(URL, wait_until="domcontentloaded", timeout=30000)
        page.wait_for_timeout(8000)  # let Blazor boot fully

        # Capture the blazor-error-ui content and surrounding DOM
        detail = page.evaluate("""() => {
            const ui = document.getElementById('blazor-error-ui');
            const out = {
                exists: !!ui,
                visible: false,
                html: null,
                text: null,
                computed_display: null,
                attributes: {},
            };
            if (ui) {
                for (const a of ui.attributes) out.attributes[a.name] = a.value;
                out.html = ui.outerHTML;
                out.text = ui.textContent.trim();
                const cs = getComputedStyle(ui);
                out.computed_display = cs.display;
                out.visible = cs.display !== 'none' && ui.getBoundingClientRect().width > 0;
            }
            // Try to find any details/console error logged
            return out;
        }""")

        # Also try fetching /health to make sure API is reachable
        try:
            r = page.request.get("http://localhost:5291/health", timeout=5000)
            health = {"status": r.status, "body": r.text()[:200]}
        except Exception as e:
            health = {"error": str(e)}

        ss_path = r"C:\Users\Usuario\AppData\Local\Temp\cardscape-capture-error.png"
        page.screenshot(path=ss_path, full_page=True)

        print("=== BLazor error UI ===")
        print(json.dumps(detail, indent=2))
        print()
        print("=== /health ===")
        print(json.dumps(health, indent=2))
        print()
        print("=== console errors/warnings ({} total) ===".format(len(errs)))
        for e in errs:
            loc = e.get("location") or {}
            locstr = f"{loc.get('url', '').split('/')[-1]}:{loc.get('lineNumber', '')}" if loc else ""
            print(f"  [{e['type']:>9}] {e['text']}  @ {locstr}")
        print()
        print(f"Screenshot: {ss_path}")

        browser.close()

if __name__ == "__main__":
    main()
