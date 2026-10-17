"""Synthetic end-to-end checks for distribution scope, review, and responsive layout."""
import json
from pathlib import Path
from selenium import webdriver
from selenium.webdriver.common.by import By
from selenium.webdriver.common.keys import Keys
from selenium.webdriver.support.ui import WebDriverWait

out = Path('.runtime/distribution-qa')
out.mkdir(exist_ok=True)
options = webdriver.ChromeOptions()
options.add_argument('--headless=new')
options.set_capability('goog:loggingPrefs', {'browser': 'ALL'})
driver = webdriver.Chrome(options=options)
wait = WebDriverWait(driver, 15)
checks = []

def check(value, label):
    if not value:
        raise AssertionError(label)
    checks.append(label)

def button(text, parent=None):
    return next(item for item in (parent or driver).find_elements(By.TAG_NAME, 'button') if item.is_displayed() and item.text.strip() == text)

def click(item):
    driver.execute_script('arguments[0].scrollIntoView({block:"center"})', item)
    item.click()

def dialog():
    return driver.find_element(By.CSS_SELECTOR, '[role=dialog]')

def rows_ready():
    wait.until(lambda d: d.find_elements(By.CSS_SELECTOR, 'main tbody tr'))

def close():
    driver.find_element(By.TAG_NAME, 'body').send_keys(Keys.ESCAPE)
    wait.until(lambda d: not d.find_elements(By.CSS_SELECTOR, '[role=dialog]'))

def requests():
    return driver.execute_script('return window.__distributionQa.requests')

def reset(kind, language):
    driver.get(f'http://127.0.0.1:5181/qa/distribution.html?kind={kind}&lang={language}')
    rows_ready()

def fit(label):
    check(driver.execute_script('return document.documentElement.scrollWidth <= innerWidth + 1'), label + ' page fits')
    check(driver.execute_script('return [...document.querySelectorAll("[role=dialog]")].every(e=>{const r=e.getBoundingClientRect();return r.left>=-1&&r.right<=innerWidth+1&&r.top>=-1&&r.bottom<=innerHeight+1})'), label + ' dialog fits')

try:
    for kind in ['bank', 'installment', 'general']:
        for language in ['en', 'ar']:
            ar = language == 'ar'
            auto = 'التوزيع التلقائي' if ar else 'Automatic distribution'
            selected_auto = 'توزيع تلقائي' if ar else 'Auto Distribute'
            select_all = 'تحديد كل المحصلين' if ar else 'Select all collectors'
            clear_all = 'إلغاء تحديد المحصلين' if ar else 'Clear collector selection'
            preview_label = ('معاينة التوزيع' if ar else 'Preview assignment') if kind == 'general' else ('مراجعة' if ar else 'Review')
            # Use actual translated labels from rendered controls where capitalisation differs.
            reset(kind, language)
            check(len(driver.find_elements(By.CSS_SELECTOR, 'main tbody tr')) == 20, f'{kind} {language}: page contains 20 cases')
            check(not driver.find_elements(By.CSS_SELECTOR, 'main textarea'), f'{kind} {language}: setup is outside case list')
            click(driver.find_element(By.CSS_SELECTOR, 'main tbody input[type=checkbox]'))
            action = next(item for item in driver.find_elements(By.TAG_NAME, 'button') if item.is_displayed() and item.text.strip().lower() in ['auto distribute', 'توزيع تلقائي'])
            click(action)
            wait.until(lambda d: d.find_elements(By.CSS_SELECTOR, '[role=dialog]'))
            check(driver.find_element(By.CSS_SELECTOR, '[role=dialog] input[type=radio]').is_selected(), f'{kind} {language}: selected-case action opens selected scope')
            for width in [320, 390, 768, 1440]:
                driver.execute_cdp_cmd('Emulation.setDeviceMetricsOverride', {'width': width, 'height': 800, 'deviceScaleFactor': 1, 'mobile': False})
                fit(f'{kind} {language} {width} setup')
            footer_button = dialog().find_elements(By.CSS_SELECTOR, 'footer button')[-1]
            check(not footer_button.is_enabled(), f'{kind} {language}: collector selection is required')
            links = dialog().find_elements(By.CSS_SELECTOR, 'fieldset button')
            click(links[0])
            check(all(item.is_selected() for item in dialog().find_elements(By.CSS_SELECTOR, 'input[type=checkbox]')), f'{kind} {language}: select all collectors')
            click(dialog().find_elements(By.CSS_SELECTOR, 'fieldset button')[1])
            check(not dialog().find_elements(By.CSS_SELECTOR, 'footer button')[-1].is_enabled(), f'{kind} {language}: clearing collectors stays cleared')
            click(dialog().find_elements(By.CSS_SELECTOR, 'fieldset button')[0])
            if kind == 'general':
                capacity = dialog().find_element(By.CSS_SELECTOR, 'input[type=number]')
                capacity.send_keys(Keys.CONTROL, 'a')
                capacity.send_keys('0')
                check(not dialog().find_elements(By.CSS_SELECTOR, 'footer button')[-1].is_enabled(), f'{kind} {language}: invalid capacity blocked')
                capacity.send_keys(Keys.CONTROL, 'a')
                capacity.send_keys('500')
            click(dialog().find_elements(By.CSS_SELECTOR, 'footer button')[-1])
            wait.until(lambda d: d.find_elements(By.CSS_SELECTOR, '[aria-current=step]')[-1].text.startswith('2'))
            preview_request = [item for item in requests() if item['method'] == 'post'][-1]
            check(preview_request['payload']['caseIds'] == ['case-0'], f'{kind} {language}: preview uses only selected cases')
            check(len([item for item in requests() if item['method'] == 'post']) == 1, f'{kind} {language}: preview does not save')
            for width in [320, 390, 768, 1440]:
                driver.execute_cdp_cmd('Emulation.setDeviceMetricsOverride', {'width': width, 'height': 800, 'deviceScaleFactor': 1, 'mobile': False})
                fit(f'{kind} {language} {width} review')
            driver.save_screenshot(str(out / f'{kind}-{language}-review.png'))
            click(dialog().find_elements(By.CSS_SELECTOR, 'footer button')[-1])
            wait.until(lambda d: not d.find_elements(By.CSS_SELECTOR, '[role=dialog]'))
            confirmation = [item for item in requests() if item['method'] == 'post'][-1]
            check(confirmation['payload']['caseIds'] == preview_request['payload']['caseIds'], f'{kind} {language}: confirm preserves reviewed cases')
            # Dismiss the success notification before interacting with pagination underneath it.
            for notification in driver.find_elements(By.CSS_SELECTOR, 'button[aria-label="Dismiss notification"],button[aria-label="إغلاق الإشعار"]'):
                click(notification)
            if kind == 'installment':
                check(confirmation['path'].startswith('/installment-companies/'), f'{kind} {language}: correct organization endpoint')
            rows_ready()
            # A filtered queue must never expand into the full queue.
            search = driver.find_element(By.CSS_SELECTOR, 'main input:not([type])')
            search.send_keys('FILTER')
            wait.until(lambda d: len(d.find_elements(By.CSS_SELECTOR, 'main tbody tr')) == 3)
            cards = driver.find_elements(By.CSS_SELECTOR, 'section[aria-label] > button')
            click(cards[1])
            wait.until(lambda d: d.find_elements(By.CSS_SELECTOR, '[role=dialog]'))
            check(dialog().find_elements(By.CSS_SELECTOR, 'input[type=radio]')[1].is_selected(), f'{kind} {language}: queue action shows explicit scope')
            click(dialog().find_elements(By.CSS_SELECTOR, 'fieldset button')[0])
            click(dialog().find_elements(By.CSS_SELECTOR, 'footer button')[-1])
            wait.until(lambda d: d.find_elements(By.CSS_SELECTOR, '[aria-current=step]')[-1].text.startswith('2'))
            payload = [item for item in requests() if item['method'] == 'post'][-1]['payload']
            check(payload['caseIds'] == ['case-0', 'case-1', 'case-2'], f'{kind} {language}: automatic queue honors search')
            close()
            search = driver.find_element(By.CSS_SELECTOR, 'main input:not([type])')
            search.send_keys(Keys.CONTROL, 'a')
            search.send_keys(Keys.BACKSPACE)
            wait.until(lambda d: len(d.find_elements(By.CSS_SELECTOR, 'main tbody tr')) == 20)
            # Pagination must send a new page request and reset selection.
            click(driver.find_element(By.CSS_SELECTOR, 'main tbody input[type=checkbox]'))
            next_page = driver.find_elements(By.CSS_SELECTOR, 'main nav button')[-1]
            click(next_page)
            wait.until(lambda d: d.find_elements(By.CSS_SELECTOR, 'main tbody tr') and 'CASE-020' in d.find_elements(By.CSS_SELECTOR, 'main tbody tr')[0].text)
            check(not any(item.is_selected() for item in driver.find_elements(By.CSS_SELECTOR, 'main tbody input[type=checkbox]')), f'{kind} {language}: page change clears selection')
            click(driver.find_elements(By.CSS_SELECTOR, 'section[aria-label] > button')[1])
            click(dialog().find_elements(By.CSS_SELECTOR, 'fieldset button')[0])
            click(dialog().find_elements(By.CSS_SELECTOR, 'footer button')[-1])
            wait.until(lambda d: d.find_elements(By.CSS_SELECTOR, '[aria-current=step]')[-1].text.startswith('2'))
            check(len([item for item in requests() if item['method'] == 'post'][-1]['payload']['caseIds']) == 125, f'{kind} {language}: queue spans matching pages')
            close()
            for width in [320, 390, 768, 1440]:
                driver.execute_cdp_cmd('Emulation.setDeviceMetricsOverride', {'width': width, 'height': 900, 'deviceScaleFactor': 1, 'mobile': False})
                fit(f'{kind} {language} {width} main')
            driver.execute_script('window.scrollTo(0,0)')
            driver.save_screenshot(str(out / f'{kind}-{language}-main.png'))
            check(not driver.execute_script('return window.__distributionQa.errors'), f'{kind} {language}: no browser errors')
            print(f'PASS {kind} {language}', flush=True)
    (out / 'checks.json').write_text(json.dumps(checks, ensure_ascii=False, indent=2), encoding='utf-8')
    print(f'COMPLETE {len(checks)} distribution assertions', flush=True)
except Exception:
    driver.save_screenshot(str(out / 'failure.png'))
    raise
finally:
    driver.quit()
