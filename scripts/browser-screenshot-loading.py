"""Take a screenshot DURING the loading-spinner phase so we can see
the actual percentage the user sees when they say 'stuck at 6%'.
"""
import time
from playwright.sync_api import sync_playwright

URL_BASE = "http://localhost:5291/"
SCREENSHOT_DIR = r"C:\Users\Usuario\AppData\Local\Temp"

with sync_playwright() as p:
    browser = p.chromium.launch(
        headless=True,
        executable_path=r"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe",
    )
    # Throttle the network so the spinner stays visible long enough
    # to capture. We're emulating a slow connection to see what
    # the user sees when they say 'stuck at 6%'.
    ctx = browser.new_context()
    page = ctx.new_page()

    # CDP throttling
    cdp = ctx.new_cdp_session(page)
    cdp.send("Network.enable")
    cdp.send("Network.emulateNetworkConditions", {
        "offline": False,
        "latency": 50,        # 50ms latency
        "downloadThroughput": 200 * 1024,  # 200 KB/s — slow
        "uploadThroughput": 100 * 1024,
    })

    page.goto(URL_BASE, wait_until="domcontentloaded", timeout=30000)
    # Sample for up to 30s while the spinner is up.
    samples = []
    t0 = time.time()
    while time.time() - t0 < 30:
        try:
            state = page.evaluate("""() => {
                const sp = document.querySelector('.loading-progress');
                const spt = document.querySelector('.loading-progress-text');
                const err = document.getElementById('blazor-error-ui');
                // Try to read the SVG circle's stroke-dashoffset to estimate progress.
                let pct = null;
                if (sp) {
                    const circles = sp.querySelectorAll('circle');
                    if (circles.length >= 1) {
                        // The first circle is the background, the second is the progress.
                        const c = circles[circles.length - 1];
                        const c2 = circles[0];
                        // The progress circle uses stroke-dasharray to draw an arc.
                        const da = c.getAttribute('stroke-dasharray') || c.style.strokeDasharray;
                        if (da) {
                            const parts = da.split(/[ ,]+/).map(parseFloat);
                            if (parts.length === 2) {
                                pct = Math.round(100 * parts[0] / (parts[0] + parts[1]));
                            }
                        }
                    }
                }
                return {
                    spText: spt ? spt.textContent.trim() : null,
                    spVisible: sp && getComputedStyle(sp).display !== 'none' && sp.getBoundingClientRect().width > 0,
                    errVisible: err && getComputedStyle(err).display !== 'none' && err.getBoundingClientRect().width > 0,
                    pct,
                };
            }""")
        except Exception as ex:
            state = {"_err": str(ex)}
        samples.append({"t_ms": int((time.time() - t0) * 1000), **state})
        if not state.get("spVisible") and not state.get("errVisible"):
            break
        page.wait_for_timeout(300)

    # Pick a mid-loading sample and screenshot it.
    if samples:
        mid = samples[len(samples) // 3]
        page.wait_for_timeout(200)
        ss_path = f"{SCREENSHOT_DIR}\\cardscape-slow.png"
        page.screenshot(path=ss_path, full_page=True)
        print(f"Mid-loading screenshot: {ss_path}")
    browser.close()

# Report
print("=== SAMPLES (first 30) ===")
for s in samples[:30]:
    print(f"  t={s.get('t_ms', 0):>5}ms  spinner='{(s.get('spText') or ''):<6}'  vis={s.get('spVisible')}  err={s.get('errVisible')}  pct={s.get('pct')}")

print(f"\nTotal samples: {len(samples)}")
pcts = [s.get('pct') for s in samples if s.get('pct') is not None]
if pcts:
    print(f"Progress pct seen: min={min(pcts)} max={max(pcts)}")
