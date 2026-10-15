import sys,os,re,json
from playwright.sync_api import sync_playwright
OUT=sys.argv[1]; LANG=sys.argv[2]; THEME=sys.argv[3] if len(sys.argv)>3 else 'dark'
os.makedirs(OUT,exist_ok=True)
B='http://localhost:5291'
with sync_playwright() as p:
    b=p.chromium.launch(channel='chrome'); ctx=b.new_context(viewport={'width':1440,'height':900},device_scale_factor=1)
    ctx.add_init_script("localStorage.setItem('Cardscape.Culture','%s')"%LANG)
    pg=ctx.new_page()
    pg.goto(B+'/login'); pg.wait_for_timeout(4000)
    i=pg.query_selector_all('input'); i[0].fill('ada.lovelace@nexora.example'); i[1].fill('Nexora!Demo-2026'); pg.keyboard.press('Enter'); pg.wait_for_timeout(5000)
    def dismiss():
        try: pg.click('.rz-alert .rz-alert-close, button[aria-label*="lose"], button[aria-label*="errar"]',timeout=800)
        except Exception: pass
    def shot(name,path,wait=5000):
        pg.goto(B+path); pg.wait_for_timeout(wait); dismiss(); pg.wait_for_timeout(500); pg.screenshot(path=f'{OUT}/{name}.png'); print(name,pg.url)
    shot('home','/')
    links=dict((n,h) for h,n in pg.eval_on_selector_all('a[href^="boards/"]','els=>els.map(e=>[e.getAttribute("href"),e.innerText.trim().split(String.fromCharCode(10))[0]])'))
    eng=links['Engineering']; ws=pg.eval_on_selector_all('a[href^="workspaces/"]','els=>els.map(e=>e.getAttribute("href").split("?")[0])')[1]
    bid=eng.split('/')[1]
    names={'en':{},'es':{}}
    shot('01-board','/'+eng,8000)
    pg.click('.rz-card >> nth=0') if False else None
    # open first card
    try:
        pg.locator('text=Migrate auth pipeline').first.click(); pg.wait_for_timeout(5000)
    except Exception as e: print('card fail',e)
    pg.screenshot(path=f'{OUT}/02-card.png'); print('card',pg.url)
    shot('03-dashboard','/'+eng+'/dashboard',7000)
    shot('04-calendar','/calendar',6000)
    shot('05-inbox','/inbox')
    shot('06-automation','/'+eng+'/automation')
    shot('07-members','/'+ws+'/members')
    shot('08-integrations','/'+ws+'/integrations')
    shot('09-tokens','/account/api-tokens')
    shot('10-themes','/settings/appearance')
    if THEME=='dark':
        pg.click('text=Light' if LANG=='en' else 'text=Claro'); pg.wait_for_timeout(2500)
        shot('11-light-board','/'+eng,8000)
    b.close()
