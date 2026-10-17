"""Exercise the real collections shell and dashboard against offline fixtures."""
import json
import re
from pathlib import Path
from selenium import webdriver
from selenium.webdriver.common.by import By
from selenium.webdriver.common.keys import Keys
from selenium.webdriver.support.ui import WebDriverWait

out = Path('.runtime/collections-workspace-qa')
out.mkdir(exist_ok=True)
options = webdriver.ChromeOptions()
options.add_argument('--headless=new')
options.set_capability('goog:loggingPrefs', {'browser': 'ALL'})
driver = webdriver.Chrome(options=options)
wait = WebDriverWait(driver, 20)
checks = []

def check(value, label):
    if not value:
        raise AssertionError(label)
    checks.append(label)

def resize(width):
    driver.execute_cdp_cmd('Emulation.setDeviceMetricsOverride', {'width': width, 'height': 950, 'deviceScaleFactor': 1, 'mobile': False})
    driver.execute_async_script('setTimeout(arguments[0], 250)')

def load(language='ar', scenario='', role='Admin', scoped=False):
    driver.get(f'http://127.0.0.1:5181/qa/company-style.html?page=workspace&lang={language}&scenario={scenario}&role={role}' + ('&organizationId=1' if scoped else ''))
    wait.until(lambda d: len(d.execute_script('return window.__companyStyleQa?.requests ?? []')) >= 2)
    wait.until(lambda d: d.find_elements(By.CSS_SELECTOR, '.overview-stat') or d.find_elements(By.CSS_SELECTOR, '[role=alert]'))
    driver.execute_async_script('document.fonts.ready.then(()=>arguments[0]())')

def errors():
    return driver.execute_script('return window.__companyStyleQa.errors')

try:
    for language in ['ar', 'en']:
        resize(1440)
        load(language)
        check(len(driver.find_elements(By.CSS_SELECTOR, 'nav .module-sidebar-link')) == 14, language + ': admin navigation retains all 14 destinations')
        check(len(driver.find_elements(By.CSS_SELECTOR, '.module-sidebar-group')) == 4, language + ': navigation uses four formal groups')
        for width in [320, 390, 768, 1024, 1280, 1440, 1920]:
            resize(width)
            check(driver.execute_script('return document.documentElement.scrollWidth<=innerWidth+1'), f'{language} {width}: workspace fits viewport')
            check(driver.execute_script("return [...document.querySelectorAll('.collection-metric-value')].every(e=>e.scrollWidth<=e.clientWidth+1)"), f'{language} {width}: KPI values fit their cards')
            check(driver.execute_script("return [...document.querySelectorAll('.overview-table th,.overview-table td:not(:first-child)')].every(e=>getComputedStyle(e).whiteSpace==='nowrap')"), f'{language} {width}: table headings and amounts never split')
            check(driver.execute_script("return [...document.querySelectorAll('.overview-table th')].every((h,i)=>{const c=h.closest('table').tBodies[0].rows[0].cells[i];return getComputedStyle(h).textAlign===getComputedStyle(c).textAlign&&getComputedStyle(h).paddingLeft===getComputedStyle(c).paddingLeft&&getComputedStyle(h).paddingRight===getComputedStyle(c).paddingRight})"), f'{language} {width}: table headings align with body cells')
            check(not re.search(r'[\u0660-\u0669\u06f0-\u06f9]', driver.find_element(By.TAG_NAME, 'main').text), f'{language} {width}: all displayed numbers use Latin digits')
            if width >= 1024:
                check(driver.execute_script("return [...document.querySelectorAll('nav .module-sidebar-label')].every(e=>e.scrollWidth<=e.clientWidth+1&&getComputedStyle(e).overflowWrap==='normal')"), f'{language} {width}: navigation labels are readable without clipping')
                check(driver.execute_script("const f=document.querySelector('.module-sidebar-footer').getBoundingClientRect();return f.height<180&&f.bottom<=innerHeight+1"), f'{language} {width}: compact footer leaves space for navigation')
            if width in [390, 1440, 1920]:
                driver.execute_script('window.scrollTo(0,0)')
                driver.save_screenshot(str(out / f'workspace-{language}-{width}.png'))
        resize(1440)
        group = driver.find_elements(By.CSS_SELECTOR, '.module-sidebar-group')[1]
        group.click()
        check(group.get_attribute('aria-expanded') == 'false', language + ': groups can be collapsed')
        driver.find_element(By.CSS_SELECTOR, '.module-sidebar-collapse').click()
        wait.until(lambda d: d.find_element(By.CSS_SELECTOR, '.module-shell').get_attribute('data-sidebar-collapsed') == 'true')
        check(all(e.is_displayed() for e in driver.find_elements(By.CSS_SELECTOR, 'nav .module-sidebar-link')), language + ': icon rail keeps destinations available when a group was closed')
        check(all(e.get_attribute('aria-label') for e in driver.find_elements(By.CSS_SELECTOR, 'nav .module-sidebar-link')), language + ': icon links retain accessible names')
        driver.save_screenshot(str(out / f'workspace-{language}-rail.png'))
        driver.find_element(By.CSS_SELECTOR, '.module-sidebar-collapse').click()
        group.click()
        resize(390)
        opener = driver.find_element(By.CSS_SELECTOR, '.module-content > header button')
        opener.click()
        wait.until(lambda d: not d.find_element(By.CSS_SELECTOR, '.module-sidebar').get_attribute('inert'))
        check(opener.get_attribute('aria-expanded') == 'true', language + ': mobile navigation opens')
        driver.save_screenshot(str(out / f'workspace-{language}-mobile-menu.png'))
        driver.find_element(By.TAG_NAME, 'body').send_keys(Keys.ESCAPE)
        wait.until(lambda d: d.find_element(By.CSS_SELECTOR, '.module-sidebar').get_attribute('inert'))
        check(driver.execute_script('return document.activeElement===arguments[0]', opener), language + ': Escape closes drawer and restores focus')
        check(not errors(), language + ': no browser errors')
        print('PASS', language, 'responsive workspace, sidebar and keyboard drawer', flush=True)

    resize(1440)
    load(scenario='busy')
    check(len(driver.find_elements(By.CSS_SELECTOR, '.overview-priority')) == 4, 'Busy dashboard presents four priorities initially')
    toggle = driver.find_element(By.CSS_SELECTOR, '.overview-priority + button')
    toggle.click()
    check(len(driver.find_elements(By.CSS_SELECTOR, '.overview-priority')) == 8, 'All eight priorities are accessible')
    toggle.click()
    check(len(driver.find_elements(By.CSS_SELECTOR, '.overview-priority')) == 4, 'Priority expansion can be reversed')
    check(len(driver.find_elements(By.CSS_SELECTOR, '.overview-trend > div')) == 7, 'Real collection trend displays seven dates')
    table = driver.find_element(By.CSS_SELECTOR, '.collection-summary-table')
    check(len(table.find_elements(By.CSS_SELECTOR, 'tbody tr')) == 6, 'Portfolio preview is limited to six organizations')
    check('CLIENT_7' in table.find_elements(By.CSS_SELECTOR, 'tbody tr')[0].text, 'Organizations are sorted by outstanding balance')
    search = driver.find_element(By.CSS_SELECTOR, 'input[type=search]')
    search.send_keys('CLIENT_2')
    check(len(table.find_elements(By.CSS_SELECTOR, 'tbody tr')) == 1 and 'CLIENT_2' in table.text, 'Organization search filters by code')
    search.send_keys(Keys.CONTROL, 'a')
    search.send_keys('NO_MATCH')
    wait.until(lambda d: not d.find_elements(By.CSS_SELECTOR, '.collection-summary-table'))
    check('لا توجد' in driver.find_element(By.TAG_NAME, 'main').text, 'Search shows an informative empty state')
    search.send_keys(Keys.CONTROL, 'a')
    search.send_keys(Keys.BACKSPACE)
    driver.find_elements(By.CSS_SELECTOR, '.overview-segments button')[1].click()
    rows = driver.find_elements(By.CSS_SELECTOR, '.collection-summary-table tbody tr')
    check(len(rows) == 4 and all('بنك' in row.text for row in rows), 'Bank filter excludes finance companies')
    driver.find_elements(By.CSS_SELECTOR, '.overview-segments button')[2].click()
    rows = driver.find_elements(By.CSS_SELECTOR, '.collection-summary-table tbody tr')
    check(len(rows) == 4 and all('شركة التمويل' in row.text for row in rows), 'Finance filter excludes banks')
    queue = driver.find_elements(By.CSS_SELECTOR, '.overview-table:not(.collection-summary-table) tbody tr')
    check(len(queue) == 3, 'Personal queue removes duplicate cases across follow-up types')
    check('إخلال' in queue[0].text, 'Broken promise receives precedence in personal queue')
    check(all('/collections/cases/case-' in row.find_element(By.TAG_NAME, 'a').get_attribute('href') for row in queue), 'Queue items open their case')
    driver.execute_script('window.scrollTo(0,0)')
    driver.save_screenshot(str(out / 'workspace-ar-busy.png'))
    check(not errors(), 'Busy fixture has no browser errors')

    load(scenario='queue-error')
    check(len(driver.find_elements(By.CSS_SELECTOR, '.overview-stat')) == 4, 'Personal queue failure preserves dashboard indicators')
    check('تعذر' in driver.find_elements(By.CSS_SELECTOR, '.overview-panel')[-1].text, 'Queue failure offers a retry')
    load(scenario='empty')
    check(len(driver.find_elements(By.CSS_SELECTOR, '.overview-stat')) == 4, 'Empty portfolio keeps meaningful zero indicators')
    check(all(e.get_attribute('aria-valuenow') == '0' for e in driver.find_elements(By.CSS_SELECTOR, '[role=progressbar]')), 'Empty portfolio avoids division by zero')
    check(not driver.find_elements(By.CSS_SELECTOR, '.overview-priority,.collection-summary-table'), 'Empty dashboard omits zero priorities and empty tables')
    driver.save_screenshot(str(out / 'workspace-ar-empty.png'))
    load(scenario='summary-error')
    check(not driver.find_elements(By.CSS_SELECTOR, '.overview-stat'), 'Failed dashboard does not invent zero balances')
    check('تعذر' in driver.find_element(By.TAG_NAME, 'main').text, 'Failed dashboard displays a retry state')

    for role, expected in [('CollectionsCollector', 10), ('CollectionsSupervisor', 12), ('CollectionsAuditor', 11)]:
        load(role=role)
        nav = driver.find_elements(By.CSS_SELECTOR, 'nav .module-sidebar-link')
        check(len(nav) == expected, role + ': menu retains role boundaries')
        check(not any('/collections/settings' in item.get_attribute('href') for item in nav), role + ': operations settings remain hidden')
        if role == 'CollectionsCollector':
            check('collectorId=unassigned' in driver.find_elements(By.CSS_SELECTOR, '.overview-stat')[-1].get_attribute('href'), 'Collectors open unassigned cases without an assignment management link')
    load(scoped=True)
    check('organizationId=organization-7' in driver.find_elements(By.CSS_SELECTOR, '.overview-stat')[-1].get_attribute('href'), 'Scoped assignment action preserves the organization')
    check('collectorId=unassigned' in driver.find_elements(By.CSS_SELECTOR, '.overview-stat')[-1].get_attribute('href'), 'Scoped assignment action targets unassigned cases')
    check(not errors(), 'Scoped dashboard has no browser errors')
    (out / 'checks.json').write_text(json.dumps(checks, ensure_ascii=False, indent=2), encoding='utf-8')
    print('COMPLETE', len(checks), 'workspace assertions', flush=True)
except Exception:
    driver.save_screenshot(str(out / 'failure.png'))
    (out / 'failure-geometry.json').write_text(json.dumps(driver.execute_script("return {width:innerWidth,scroll:document.documentElement.scrollWidth,overflow:[...document.querySelectorAll('body *')].map(e=>({tag:e.tagName,classes:e.className,left:e.getBoundingClientRect().left,right:e.getBoundingClientRect().right,width:e.clientWidth,scroll:e.scrollWidth})).filter(e=>e.left < -1||e.right>innerWidth+1)}"), ensure_ascii=False, indent=2), encoding='utf-8')
    (out / 'failure-log.json').write_text(json.dumps(driver.get_log('browser'), ensure_ascii=False, indent=2), encoding='utf-8')
    raise
finally:
    driver.quit()
