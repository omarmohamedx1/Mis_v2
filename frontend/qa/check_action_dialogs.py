import json,time
from pathlib import Path
from selenium import webdriver
from selenium.webdriver.common.by import By
from selenium.webdriver.common.keys import Keys
from selenium.webdriver.support.ui import WebDriverWait
options=webdriver.ChromeOptions();options.add_argument('--headless=new');driver=webdriver.Chrome(options=options);wait=WebDriverWait(driver,25);checks=[]
def check(ok,label):
 if not ok:raise AssertionError(label)
 checks.append(label);print('PASS',label,flush=True)
def button(text):return next(x for x in driver.find_elements(By.TAG_NAME,'button') if x.text==text and x.is_displayed())
try:
 for lang in ['en','ar']:
  for width in [320,360,390,768,1024,1440]:
   driver.execute_cdp_cmd('Emulation.setDeviceMetricsOverride',{'width':width,'height':480,'deviceScaleFactor':1,'mobile':False})
   driver.get('http://127.0.0.1:5179/qa/dialogs.html?lang='+lang);wait.until(lambda d:d.find_elements(By.TAG_NAME,'button'))
   button('Prompt').click();wait.until(lambda d:d.find_elements(By.CSS_SELECTOR,'[aria-modal=true]'))
   check(driver.execute_script('const r=document.querySelector("[aria-modal=true]").getBoundingClientRect();return r.left>=0&&r.right<=innerWidth&&r.top>=0&&r.bottom<=innerHeight'),f'{lang} prompt fits {width}x480')
   driver.switch_to.active_element.send_keys('  reason / \u0633\u0628\u0628  ',Keys.ENTER);wait.until(lambda d:not d.find_elements(By.CSS_SELECTOR,'[aria-modal=true]'))
   check(json.loads(driver.find_element(By.ID,'result').text)=='  reason / \u0633\u0628\u0628  ',f'{lang} prompt preserves exact input {width}')
  button('Prompt').click();wait.until(lambda d:d.find_elements(By.TAG_NAME,'input'));driver.switch_to.active_element.send_keys(Keys.ESCAPE);time.sleep(.1)
  check(driver.find_element(By.ID,'result').text=='null',lang+' prompt Escape retains null cancellation contract')
  button('Prompt').click();wait.until(lambda d:d.find_elements(By.TAG_NAME,'input'));driver.switch_to.active_element.send_keys(Keys.ENTER);time.sleep(.1)
  check(driver.find_element(By.ID,'result').text=='""',lang+' empty confirmation retains empty-string contract')
  button('Confirm').click();wait.until(lambda d:d.find_elements(By.CSS_SELECTOR,'[role=alertdialog]'));driver.switch_to.active_element.send_keys(Keys.ESCAPE);time.sleep(.1)
  check(driver.find_element(By.ID,'result').text=='false',lang+' confirmation Escape returns false')
  button('Confirm').click();wait.until(lambda d:d.find_elements(By.CSS_SELECTOR,'[role=alertdialog]'));driver.find_element(By.CSS_SELECTOR,'[role=alertdialog] footer button:last-child').click();time.sleep(.1)
  check(driver.find_element(By.ID,'result').text=='true',lang+' explicit confirmation returns true')
 Path('.runtime/visual-qa/action-dialog-checks.json').write_text(json.dumps(checks,indent=2));print('COMPLETE',len(checks),'action-dialog assertions',flush=True)
finally:driver.quit()
