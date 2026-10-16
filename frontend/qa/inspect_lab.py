import json, time, os
from pathlib import Path
from selenium import webdriver
from selenium.webdriver.common.by import By
from selenium.webdriver.common.keys import Keys
from selenium.webdriver.support.ui import WebDriverWait

out=Path('.runtime/visual-qa'); out.mkdir(exist_ok=True)
options=webdriver.ChromeOptions(); options.add_argument('--headless=new'); options.set_capability('goog:loggingPrefs',{'browser':'ALL'})
driver=webdriver.Chrome(options=options); wait=WebDriverWait(driver,25)
phase=os.environ.get('MIS_QA_PHASE','before')
results=[]
def resize(w,h=950): driver.execute_cdp_cmd('Emulation.setDeviceMetricsOverride',{'width':w,'height':h,'deviceScaleFactor':1,'mobile':False})
def btn(text): return next(x for x in driver.find_elements(By.TAG_NAME,'button') if x.text==text and x.is_displayed())
geometry='''
const visible=e=>{const r=e.getBoundingClientRect(),s=getComputedStyle(e);return r.width>1&&r.height>1&&s.visibility!=='hidden'&&s.display!=='none'&&Number(s.opacity)!==0};
const items=[...document.querySelectorAll('body *')].filter(visible);
const overflow=items.filter(e=>{const r=e.getBoundingClientRect();return (r.left < -1 || r.right > innerWidth+1) && !e.closest('.table-scroll,.overflow-x-auto,[role=tablist],.module-sidebar') && !['HTML','BODY','SCRIPT','STYLE'].includes(e.tagName)}).map(e=>({tag:e.tagName,cls:e.className,text:(e.innerText||'').slice(0,90),left:e.getBoundingClientRect().left,right:e.getBoundingClientRect().right}));
return {viewport:innerWidth,scroll:document.documentElement.scrollWidth,overflow:overflow.slice(0,30),controls:[...document.querySelectorAll('[data-qa=fields] input:not([type=hidden]),[data-qa=fields] button[aria-haspopup]')].filter(visible).map(e=>({label:e.getAttribute('aria-label')||e.id,height:e.getBoundingClientRect().height,width:e.getBoundingClientRect().width,cls:e.className}))};
'''
try:
    for lang in ['en','ar']:
        driver.get('http://127.0.0.1:5179/qa/index.html?lang='+lang)
        wait.until(lambda d:d.find_elements(By.TAG_NAME,'h1'))
        for width in [320,360,390,768,1024,1440]:
            resize(width); time.sleep(.3)
            result=driver.execute_script(geometry);result.update(lang=lang,width=width);results.append(result)
            driver.save_screenshot(str(out/f'lab-{phase}-{lang}-{width}.png'))
        resize(390)
        before={name:driver.find_element(By.CSS_SELECTOR,f'[data-qa="{name}"]').rect['width'] for name in ['loading-no-icon','loading-right-icon']}
        btn('Toggle loading').click()
        after={name:driver.find_element(By.CSS_SELECTOR,f'[data-qa="{name}"]').rect['width'] for name in before}
        results.append({'lang':lang,'loadingWidth':{'before':before,'after':after}})
        btn('Toggle loading').click()
        driver.find_element(By.ID,'qa-time').click()
        choose='الساعة' if lang=='ar' else 'Hour'
        driver.find_element(By.CSS_SELECTOR,f'button[aria-label="{choose}"]').click()
        results.append({'lang':lang,'nestedTimeOptionCount':len(driver.find_elements(By.CSS_SELECTOR,'[role=option]'))})
        driver.save_screenshot(str(out/f'time-nested-{phase}-{lang}.png'))
        driver.find_element(By.TAG_NAME,'body').send_keys(Keys.ESCAPE)
        driver.find_element(By.TAG_NAME,'body').send_keys(Keys.ESCAPE)
        resize(320,480)
        # Open datetime calendar at a deliberately short viewport.
        buttons=driver.find_elements(By.CSS_SELECTOR,'button[aria-label="'+('فتح التقويم' if lang=='ar' else 'Open calendar')+'"]')
        driver.execute_script('arguments[0].scrollIntoView({block:"center"})',buttons[1]); buttons[1].click()
        driver.save_screenshot(str(out/f'calendar-{phase}-{lang}-320.png'))
        results.append({'lang':lang,'calendar':driver.execute_script("return [...document.querySelectorAll('.fixed')].filter(x=>getComputedStyle(x).zIndex==='100').map(x=>({top:x.getBoundingClientRect().top,bottom:x.getBoundingClientRect().bottom,height:x.getBoundingClientRect().height,viewport:innerHeight}))")})
        driver.find_element(By.TAG_NAME,'body').send_keys(Keys.ESCAPE)
    (out/f'lab-{phase}.json').write_text(json.dumps(results,ensure_ascii=False,indent=2),encoding='utf-8')
    print(json.dumps({'samples':len(results),'overflowSamples':sum(bool(x.get('overflow')) for x in results),'nestedTimeOptions':[x['nestedTimeOptionCount'] for x in results if 'nestedTimeOptionCount' in x]},indent=2))
    errors=[x for x in driver.get_log('browser') if x['level']=='SEVERE' and 'favicon' not in x['message']]
    print('Browser errors:',json.dumps(errors[:3]))
finally: driver.quit()
