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

money_geometry = '''
return [...document.querySelectorAll(arguments[0])].flatMap(element=>{
 const node=element.firstChild;
 if(!node||node.nodeType!==Node.TEXT_NODE)return [];
 const text=node.textContent,number=/-?\\d[\\d,.]*/.exec(text),unit=/(?:مليون|مليار|ألف|آلاف|تريليون)/.exec(text),currency=text.indexOf('ج.م');
 if(!number||!unit||currency<0)return [];
 const range=document.createRange();
 const bounds=(start,length)=>{range.setStart(node,start);range.setEnd(node,start+length);const r=range.getBoundingClientRect();return {left:r.left,right:r.right,top:r.top,bottom:r.bottom,lines:range.getClientRects().length};};
 return [{text,amount:bounds(number.index,number[0].length),unit:bounds(unit.index,unit[0].length),currency:bounds(currency,3)}];
});
'''

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
        if language == 'ar':
            order = driver.execute_script('''
              return [...document.querySelectorAll('[data-money-value]:not([data-currency])')].map(element=>{
                const node=element.firstChild,text=node.textContent,number=/-?\\d[\\d,.]*/.exec(text),currency=text.indexOf('ج.م');
                const range=document.createRange();
                range.setStart(node,number.index);range.setEnd(node,number.index+number[0].length);const amount=range.getBoundingClientRect();
                range.setStart(node,currency);range.setEnd(node,currency+3);const unit=range.getBoundingClientRect();
                return {module:element.dataset.moduleName,context:element.dataset.context,amount:element.dataset.amount,numberLeft:amount.left,currencyRight:unit.right};
              });
            ''')
            check(len(order) == 50, 'Currency order covers five module formatters, five amounts and both text directions')
            for item in order:
                check(item['currencyRight'] <= item['numberLeft'] + 1, f"{item['module']} {item['context']} {item['amount']}: currency follows the number in Arabic reading order")
            currency_text = driver.find_element(By.CSS_SELECTOR, '[data-qa=currency-order]').text
            check(not re.search(r'[\u0660-\u0669\u06f0-\u06f9]', currency_text), 'Currency ordering preserves Latin digits')
            for currency in ['USD', 'SAR']:
                text = driver.find_element(By.CSS_SELECTOR, f'[data-currency="{currency}"]').text
                check('ج.م' not in text and '55.00' in text, currency + ': other currency labels and decimal places are preserved')
            compact = driver.execute_script(money_geometry, '[data-money-value],[data-compact-money]')
            check(len(compact) >= 12, 'Compact regression covers thousand, million, billion and negative values in both directions')
            for index, item in enumerate(compact):
                check(item['unit']['right'] <= item['amount']['left'] + 1 and item['currency']['right'] <= item['unit']['left'] + 1, f'compact {index}: number, scale and currency follow Arabic reading order')
            quantities = driver.execute_script('''
              return [...document.querySelectorAll('[data-quantity-value]')].map(element=>{
                const node=element.firstChild,text=node.textContent,number=/-?\\d[\\d,.]*/.exec(text),unit=text.indexOf(element.dataset.unit),range=document.createRange();
                range.setStart(node,number.index);range.setEnd(node,number.index+number[0].length);const amount=range.getBoundingClientRect();
                range.setStart(node,unit);range.setEnd(node,unit+element.dataset.unit.length);const label=range.getBoundingClientRect();
                return {numberLeft:amount.left,unitRight:label.right};
              });
            ''')
            check(len(quantities) == 4 and all(item['unitRight'] <= item['numberLeft'] + 1 for item in quantities), 'Counts and percentages keep their translated label after the value in both directions')
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
    # Use the actual dashboard components and the amounts/counts in the reported screenshot.
    for page in ['dashboard', 'creditors', 'collectors']:
        for language in ['ar', 'en']:
            resize(1440)
            driver.get(f'http://127.0.0.1:5181/qa/company-style.html?lang={language}&page={page}')
            wait.until(lambda d: d.find_elements(By.CSS_SELECTOR, 'main .collection-metric-value'))
            driver.execute_async_script('document.fonts.ready.then(()=>arguments[0]())')
            for width in [320, 390, 768, 1024, 1280, 1440]:
                resize(width)
                check(driver.execute_script('return document.documentElement.scrollWidth<=innerWidth+1'), f'{page} {language} {width}: page fits viewport')
                check(driver.execute_script("return [...document.querySelectorAll('.collection-metric-value')].every(e=>e.scrollWidth<=e.clientWidth+1)"), f'{page} {language} {width}: metric amounts stay within their cards')
                if language == 'ar':
                    values = driver.execute_script(money_geometry, '.collection-metric-value,[data-bidi=ltr]')
                    check(len(values) >= 3, f'{page} {width}: actual page contains compact amounts')
                    check(all(item['unit']['right'] <= item['amount']['left'] + 1 and item['currency']['right'] <= item['unit']['left'] + 1 for item in values), f'{page} {width}: actual compact amounts read number then scale then currency')
                    check(all(item['amount']['lines'] == 1 and item['unit']['lines'] == 1 and item['currency']['lines'] == 1 and abs(item['amount']['top']-item['unit']['top']) < 8 for item in values), f'{page} {width}: compact amounts never split across lines')
                if page != 'collectors':
                    check(driver.execute_script("return [...document.querySelectorAll('.collection-summary-table th,.collection-summary-table td:not(:first-child)')].every(e=>getComputedStyle(e).whiteSpace==='nowrap')"), f'{page} {language} {width}: summary table preserves headers and numeric cells')
                    check(driver.execute_script("return [...document.querySelectorAll('.collection-summary-table tbody tr')].every(row=>[...row.cells].slice(1).every(cell=>{const r=document.createRange();r.selectNodeContents(cell);return [...r.getClientRects()].every(rect=>rect.height<40)}))"), f'{page} {language} {width}: case counts and amounts remain readable')
                if width in [390, 1440]:
                    driver.execute_script('window.scrollTo(0,0)')
                    driver.save_screenshot(str(out / f'{page}-{language}-{width}.png'))
            check(not driver.execute_script('return window.__companyStyleQa.errors'), f'{page} {language}: no browser errors')
            print('PASS', page, language, 'compact amounts and dashboard layouts', flush=True)
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
