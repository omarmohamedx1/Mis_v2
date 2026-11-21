"""Exercise the real page/modal against an offline Axios adapter only."""
from selenium import webdriver
from selenium.webdriver.common.by import By
from selenium.webdriver.support.ui import WebDriverWait

options = webdriver.EdgeOptions()
options.add_argument('--headless=new')
driver = webdriver.Edge(options=options)
driver.set_window_size(1440, 1000)
wait = WebDriverWait(driver, 20)
checks = 0

def button(text):
    return next(x for x in driver.find_elements(By.TAG_NAME, 'button') if x.is_displayed() and x.text == text)

try:
    for lang in ['en', 'ar']:
        for scenario in ['ok', '401', '403', '404', '500', 'network', 'malformed', 'wrong-id', 'readonly', 'save-error']:
            driver.get(f'http://127.0.0.1:5187/qa/legal-client-edit.html?lang={lang}&scenario={scenario}')
            wait.until(lambda d: len(d.find_elements(By.CSS_SELECTOR, 'tbody tr')) == 2)
            trigger = driver.find_elements(By.CSS_SELECTOR, 'button[aria-haspopup="menu"]')[1]
            driver.execute_script('arguments[0].scrollIntoView({block:"center",inline:"center"})', trigger)
            trigger.click()
            button('Edit' if lang == 'en' else 'تعديل').click()
            wait.until(lambda d: d.find_elements(By.CSS_SELECTOR, '[role="dialog"] textarea,[role="alert"]'))
            save = button('Save Client File' if lang == 'en' else 'حفظ الملف')
            requests = driver.execute_script('return window.legalEditQa.requests')
            assert requests[-1]['url'].endswith('00000000-0000-0000-0000-000000000002')
            assert requests[-1]['method'] == 'get'
            if scenario in ['ok', 'save-error', 'readonly']:
                field = driver.find_element(By.TAG_NAME, 'textarea')
                assert field.get_attribute('value') == 'Second notes'
                if scenario == 'readonly':
                    assert not save.is_enabled()
                else:
                    assert save.is_enabled()
                    field.clear()
                    field.send_keys('Updated second notes')
                    save.click()
                    if scenario == 'save-error':
                        wait.until(lambda d: d.find_elements(By.CSS_SELECTOR, '[role="dialog"] [role="alert"]'))
                        assert field.get_attribute('value') == 'Updated second notes'
                    else:
                        wait.until(lambda d: not d.find_elements(By.CSS_SELECTOR, '[role="dialog"]'))
                        requests = driver.execute_script('return window.legalEditQa.requests')
                        put = [r for r in requests if r['method'] == 'put']
                        assert len(put) == 1 and put[0]['url'].endswith('000002')
                        assert requests[-1]['url'] == '/legal/clients'
                        assert driver.execute_script('return window.legalEditQa.notes[window.legalEditQa.rows[0].customerId]') == 'First notes'
            else:
                assert not save.is_enabled()
                message = driver.find_element(By.CSS_SELECTOR, '[role="alert"]').text
                expected = {'401': ('session has expired', 'انتهت جلسة'), '403': ('permission', 'صلاحية'), '404': ('not found', 'غير موجود'), '500': ('server could not', 'خطأ في الخادم'), 'network': ('connect', 'الاتصال'), 'malformed': ('incompatible', 'غير متوافقة'), 'wrong-id': ('incompatible', 'غير متوافقة')}
                assert expected[scenario][0 if lang == 'en' else 1] in message
                assert '/unauthorized' not in driver.current_url
            checks += 1
    print(f'PASS: {checks} Legal edit browser scenarios; synthetic records only.')
finally:
    driver.quit()
