"""Synthetic interaction checks; never submits business-data mutations."""
import json
import time
from pathlib import Path
from selenium import webdriver
from selenium.webdriver.common.by import By
from selenium.webdriver.common.keys import Keys
from selenium.webdriver.support.ui import WebDriverWait

options = webdriver.ChromeOptions()
options.add_argument('--headless=new')
options.set_capability('goog:loggingPrefs', {'browser': 'ALL'})
driver = webdriver.Chrome(options=options)
wait = WebDriverWait(driver, 20)
out = Path('.runtime/visual-qa/action-menus')
out.mkdir(parents=True, exist_ok=True)
checks = []

def check(condition, label):
    assert condition, label
    checks.append(label)
    print('PASS', label, flush=True)

def visible(selector):
    return [x for x in driver.find_elements(By.CSS_SELECTOR, selector) if x.is_displayed()]

def trigger(label='Row actions'):
    return driver.find_element(By.CSS_SELECTOR, f'button[aria-haspopup="menu"][aria-label="{label}"]')

def click(element):
    driver.execute_script('arguments[0].scrollIntoView({block:"center",inline:"center"})', element)
    element.click()
    time.sleep(.1)

def menu():
    return wait.until(lambda _: next(iter(visible('[role=menu]')), None))

def items():
    return menu().find_elements(By.CSS_SELECTOR, '[role=menuitem]')

def escape():
    driver.switch_to.active_element.send_keys(Keys.ESCAPE)
    time.sleep(.1)

def resize(width, height=900):
    driver.execute_cdp_cmd('Emulation.setDeviceMetricsOverride', {'width': width, 'height': height, 'deviceScaleFactor': 1, 'mobile': False})
    time.sleep(.15)

try:
    for lang in ['en', 'ar']:
        resize(1440)
        driver.get('http://127.0.0.1:5179/qa/index.html?lang=' + lang)
        wait.until(lambda _: driver.find_elements(By.CSS_SELECTOR, '[data-qa=action-menus]'))
        check(not driver.find_elements(By.CSS_SELECTOR, '[data-qa=empty-actions] button'), lang + ' empty permissions hide the trigger')
        check(not visible('[role=menu]'), lang + ' actions initially collapsed')
        for width in [320, 360, 390, 768, 1024, 1440]:
            resize(width, 480 if width == 320 else 900)
            click(trigger())
            panel = menu()
            check(driver.execute_script('const r=arguments[0].getBoundingClientRect();return r.left>=0&&r.top>=0&&r.right<=innerWidth+1&&r.bottom<=innerHeight+1', panel), f'{lang} {width} menu remains inside viewport')
            check(panel.get_attribute('dir') == ('rtl' if lang == 'ar' else 'ltr'), f'{lang} {width} menu direction')
            check(len(items()) == 6 and all(x.text.strip() for x in items()), f'{lang} {width} native, shared, icon and custom actions have visible labels')
            check(driver.execute_script('return [...arguments[0].querySelectorAll("[role=menuitem]")].every(e=>e.scrollWidth<=e.clientWidth+1)', panel), f'{lang} {width} long labels wrap')
            if width in [390, 1440]:
                driver.save_screenshot(str(out / f'{lang}-{width}.png'))
            escape()
            check(trigger().get_attribute('aria-expanded') == 'false' and driver.switch_to.active_element == trigger(), f'{lang} {width} Escape closes and restores focus')
        resize(1440)
        trigger().send_keys(Keys.ARROW_DOWN)
        check(driver.switch_to.active_element == items()[0], lang + ' ArrowDown opens at first action')
        driver.switch_to.active_element.send_keys(Keys.ARROW_DOWN)
        check(driver.switch_to.active_element == items()[1], lang + ' ArrowDown moves focus')
        driver.switch_to.active_element.send_keys(Keys.ARROW_DOWN)
        check(driver.switch_to.active_element == items()[3], lang + ' disabled action is skipped')
        driver.switch_to.active_element.send_keys(Keys.END)
        check(driver.switch_to.active_element == items()[-1], lang + ' End moves to last action')
        driver.switch_to.active_element.send_keys(Keys.HOME)
        check(driver.switch_to.active_element == items()[0], lang + ' Home moves to first action')
        driver.switch_to.active_element.send_keys(Keys.ENTER)
        time.sleep(.1)
        check(not visible('[role=menu]') and driver.find_element(By.CSS_SELECTOR, '[data-qa=action-result]').text == 'view', lang + ' Enter invokes action and closes menu')
        check(driver.find_element(By.CSS_SELECTOR, '[data-qa=row-clicks]').text == '0', lang + ' trigger and action do not activate table row')
        trigger().send_keys(Keys.ARROW_UP)
        check(driver.switch_to.active_element == items()[-1], lang + ' ArrowUp opens at last action')
        driver.switch_to.active_element.send_keys(Keys.TAB)
        time.sleep(.1)
        check(not visible('[role=menu]'), lang + ' Tab dismisses menu')
        click(trigger())
        # The open popup can physically cover the next row; use keyboard focus to switch.
        trigger('Second row actions').send_keys(Keys.ENTER)
        time.sleep(.1)
        check(len(visible('[role=menu]')) == 1 and trigger().get_attribute('aria-expanded') == 'false', lang + ' opening another row dismisses previous menu')
        click(driver.find_element(By.CSS_SELECTOR, '[data-qa=action-menus] > button'))
        check(not visible('[role=menu]') and driver.find_element(By.CSS_SELECTOR, '[data-qa=action-result]').text == 'outside', lang + ' outside click dismisses and activates target')
        click(trigger('LTR actions'))
        check(menu().get_attribute('dir') == 'ltr', lang + ' explicit local LTR direction respected')
        escape()
        click(trigger())
        click(items()[3])
        check(driver.execute_script('return location.hash') == '#menu-link' and not visible('[role=menu]'), lang + ' router link navigation preserved')
        click(trigger())
        click(items()[1])
        wait.until(lambda _: visible('[role=dialog]'))
        check(not visible('[role=menu]'), lang + ' action with stopPropagation closes and opens dialog')
        check(driver.execute_script('return !!document.activeElement.closest("[role=dialog]")'), lang + ' dialog receives focus')
        click(trigger('Dialog actions'))
        escape()
        check(len(visible('[role=dialog]')) == 1 and not visible('[role=menu]'), lang + ' first Escape closes only nested menu')
        escape()
        check(not visible('[role=dialog]') and driver.switch_to.active_element == trigger(), lang + ' second Escape closes dialog and restores trigger')
        click(trigger())
        click(items()[-1])
        check(bool(visible('[role=alertdialog]')) and not visible('[role=menu]'), lang + ' stateful delete confirmation remains mounted after menu closes')
        escape()
        check(not visible('[role=alertdialog]'), lang + ' deletion can be cancelled without mutation')
        check(not driver.execute_script('return window.__qa.errors.length') and not driver.execute_script('return window.__qa.blocked.length'), lang + ' no runtime errors or mutation requests')
    (out / 'checks.json').write_text(json.dumps(checks, ensure_ascii=False, indent=2), encoding='utf-8')
    print(f'{len(checks)} checks passed', flush=True)
finally:
    driver.quit()
