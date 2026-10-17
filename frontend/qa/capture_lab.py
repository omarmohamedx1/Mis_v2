"""Capture synthetic, shareable full-page component evidence. Run from repo root."""
import time
from pathlib import Path
from selenium import webdriver
from selenium.webdriver.support.ui import WebDriverWait
import base64

out=Path('.runtime/visual-qa/full-lab');out.mkdir(parents=True,exist_ok=True)
options=webdriver.ChromeOptions();options.add_argument('--headless=new')
driver=webdriver.Chrome(options=options);wait=WebDriverWait(driver,25)
try:
 for lang in ['en','ar']:
  for width in [320,390,768,1440]:
   driver.execute_cdp_cmd('Emulation.setDeviceMetricsOverride',{'width':width,'height':950,'deviceScaleFactor':1,'mobile':False})
   driver.get('http://127.0.0.1:5179/qa/index.html?lang='+lang);wait.until(lambda d:d.execute_script('return window.__qa?.ready'))
   driver.execute_async_script('document.fonts.ready.then(()=>arguments[0]())');time.sleep(.25)
   height=driver.execute_script('return Math.max(document.body.scrollHeight,document.documentElement.scrollHeight)')
   result=driver.execute_cdp_cmd('Page.captureScreenshot',{'format':'png','captureBeyondViewport':True,'clip':{'x':0,'y':0,'width':width,'height':height,'scale':1}})
   (out/f'{lang}-{width}.png').write_bytes(base64.b64decode(result['data']))
 print('Captured 8 full-page synthetic component screenshots',flush=True)
finally:driver.quit()
