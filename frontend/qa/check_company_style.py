"""Offline UI checks for Latin digits, bundled typography and column alignment."""
import json
import re
from pathlib import Path
from selenium import webdriver
from selenium.webdriver.common.by import By
from selenium.webdriver.common.keys import Keys
from selenium.webdriver.support.ui import WebDriverWait

out = Path('.runtime/company-style-qa')
out.mkdir(exist_ok=True)
options = webdriver.ChromeOptions()
options.add_argument('--headless=new')
options.set_capability('goog:loggingPrefs', {'browser': 'ALL'})
driver = webdriver.Chrome(options=options)
wait = WebDriverWait(driver, 20)
checks, measurements = [], []

def check(value, label):
    if not value:
        raise AssertionError(label)
    checks.append(label)

def font_families(selector):
    root = driver.execute_cdp_cmd('DOM.getDocument', {})['root']['nodeId']
    node = driver.execute_cdp_cmd('DOM.querySelector', {'nodeId': root, 'selector': selector})['nodeId']
    return driver.execute_cdp_cmd('CSS.getPlatformFontsForNode', {'nodeId': node})['fonts']

def resize(width):
    driver.execute_cdp_cmd('Emulation.setDeviceMetricsOverride', {'width': width, 'height': 950, 'deviceScaleFactor': 1, 'mobile': False})

alignment = '''
return [...document.querySelectorAll('main table')].flatMap((table,index)=>{
 const headers=[...table.tHead.rows[0].cells], row=table.tBodies[0]?.rows[0];
 if(!row||headers.length!==row.cells.length)return [];
 return headers.flatMap((header,column)=>{
   if(!header.textContent.trim()||column===headers.length-1&&row.cells[column].querySelector('button'))return [];
   const cell=row.cells[column],h=getComputedStyle(header),c=getComputedStyle(cell),hr=header.getBoundingClientRect(),cr=cell.getBoundingClientRect();
   return [{table:index,column,header:header.textContent.trim(),headerAlign:h.textAlign,cellAlign:c.textAlign,headerPadding:[h.paddingLeft,h.paddingRight],cellPadding:[c.paddingLeft,c.paddingRight],leftDiff:Math.abs(hr.left-cr.left),rightDiff:Math.abs(hr.right-cr.right)}];
 });
});
'''

try:
    driver.execute_cdp_cmd('DOM.enable', {})
    driver.execute_cdp_cmd('CSS.enable', {})
    for language in ['en', 'ar']:
        resize(1440)
        driver.get(f'http://127.0.0.1:5181/qa/company-style.html?lang={language}')
        wait.until(lambda d: d.find_elements(By.CSS_SELECTOR, '[data-qa=payroll] tbody tr'))
        driver.execute_async_script('document.fonts.ready.then(()=>arguments[0]())')
        for module in ['accounting', 'finance', 'legal', 'data-entry', 'collections']:
            text = driver.find_element(By.CSS_SELECTOR, f'[data-module="{module}"]').text
            check(not re.search(r'[\u0660-\u0669\u06f0-\u06f9]', text), f'{language} {module}: numbers, currency, dates and times use Latin digits')
            check('2026' in text and re.search(r'1,234,567', text), f'{language} {module}: grouping and date values are preserved')
        check(not re.search(r'[\u0660-\u0669\u06f0-\u06f9]', driver.find_element(By.CSS_SELECTOR, '[data-qa=payroll]').text), language + ': actual payroll page uses Latin digits')
        check(driver.execute_script('return document.documentElement.dir') == ('rtl' if language == 'ar' else 'ltr'), language + ': navigation keeps its language direction')
        if language == 'ar':
            fonts = font_families('main h1')
            check(any('Noto Sans Arabic' in font['familyName'] and font['isCustomFont'] for font in fonts), 'Arabic headings render with the bundled Noto font')
            check('أكتوبر' in driver.find_element(By.CSS_SELECTOR, '[data-module=accounting]').text, 'Arabic month names remain Arabic')
        fonts = font_families('[data-module=finance] td:nth-child(2)')
        check(any(font['familyName'] == 'Inter' and font['isCustomFont'] for font in fonts), language + ': numbers render with bundled Inter')
        check(not driver.execute_script("return performance.getEntriesByType('resource').some(item=>/fonts\\.(googleapis|gstatic)\\.com/.test(item.name))"), language + ': fonts load without an external font request')
        for width in [320, 360, 390, 768, 1024, 1440]:
            resize(width)
            check(driver.execute_script('return document.documentElement.scrollWidth<=innerWidth+1'), f'{language} {width}: page fits viewport')
            cells = driver.execute_script(alignment)
            measurements.append({'language': language, 'width': width, 'cells': cells})
            check(all(cell['headerAlign'] == cell['cellAlign'] for cell in cells), f'{language} {width}: headings and values align in every tested column')
            check(all(cell['headerPadding'] == cell['cellPadding'] for cell in cells), f'{language} {width}: heading and body insets match')
            check(all(cell['leftDiff']<1 and cell['rightDiff']<1 for cell in cells), f'{language} {width}: headings share column boundaries with their values')
            if width in [390, 1440]:
                driver.save_screenshot(str(out / f'{language}-{width}.png'))
        # Custom calendar day/year labels must also use 0–9 in Arabic.
        opener = driver.find_element(By.CSS_SELECTOR, 'button[aria-label="' + ('فتح التقويم' if language == 'ar' else 'Open calendar') + '"]')
        driver.execute_script('arguments[0].scrollIntoView({block:"center"})', opener)
        opener.click()
        wait.until(lambda d: d.find_elements(By.CSS_SELECTOR, '.date-panel'))
        text = driver.find_element(By.CSS_SELECTOR, '.date-panel').text
        check(not re.search(r'[\u0660-\u0669\u06f0-\u06f9]', text) and '2026' in text, language + ': calendar labels and day numbers use Latin digits')
        driver.find_element(By.TAG_NAME, 'body').send_keys(Keys.ESCAPE)
        check(not driver.execute_script('return window.__companyStyleQa.errors'), language + ': no browser errors')
        print('PASS', language, 'formatters, payroll, typography, calendar and six table widths', flush=True)
    # Exercise the real distribution tables after the common styles changed.
    for kind in ['bank', 'installment', 'general']:
        for language in ['en', 'ar']:
            resize(1440)
            driver.get(f'http://127.0.0.1:5181/qa/distribution.html?kind={kind}&lang={language}')
            wait.until(lambda d: d.find_elements(By.CSS_SELECTOR, 'main tbody tr'))
            driver.execute_async_script('document.fonts.ready.then(()=>arguments[0]())')
            check(not re.search(r'[\u0660-\u0669\u06f0-\u06f9]', driver.find_element(By.TAG_NAME, 'main').text), f'{kind} {language}: real distribution page uses Latin digits')
            for width in [320, 390, 768, 1440]:
                resize(width)
                cells = driver.execute_script(alignment)
                check(all(cell['headerAlign'] == cell['cellAlign'] and cell['headerPadding'] == cell['cellPadding'] for cell in cells), f'{kind} {language} {width}: actual case table alignment')
                check(driver.execute_script('return document.documentElement.scrollWidth<=innerWidth+1'), f'{kind} {language} {width}: actual page fits viewport')
            check(not driver.execute_script('return window.__distributionQa.errors'), f'{kind} {language}: no browser errors')
    (out / 'checks.json').write_text(json.dumps(checks, ensure_ascii=False, indent=2), encoding='utf-8')
    (out / 'table-measurements.json').write_text(json.dumps(measurements, ensure_ascii=False, indent=2), encoding='utf-8')
    print('COMPLETE', len(checks), 'company interface assertions', flush=True)
except Exception:
    driver.save_screenshot(str(out / 'failure.png'))
    (out / 'failure-log.json').write_text(json.dumps(driver.get_log('browser'), ensure_ascii=False, indent=2), encoding='utf-8')
    raise
finally:
    driver.quit()
