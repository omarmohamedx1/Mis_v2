import json, os, re, time, threading, urllib.request, urllib.error
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path
from selenium import webdriver
from selenium.webdriver.common.by import By
from selenium.webdriver.support.ui import WebDriverWait

out=Path('.runtime/visual-qa'); inventory=json.loads((out/'inventory.json').read_text())
options=webdriver.ChromeOptions();options.add_argument('--headless=new');options.set_capability('goog:loggingPrefs',{'browser':'ALL'})
driver=webdriver.Chrome(options=options);wait=WebDriverWait(driver,30)
phase=os.environ.get('MIS_QA_PHASE','after')
results=[];overlays=[];accessibility=[];requests=[];links=set();base='http://127.0.0.1:5179/qa/index.html'
class Proxy(BaseHTTPRequestHandler):
    def log_message(self,*args):pass
    def headers_out(self,status):
        self.send_response(status);self.send_header('Access-Control-Allow-Origin','http://127.0.0.1:5179');self.send_header('Access-Control-Allow-Headers','Content-Type, Authorization, Accept-Language');self.send_header('Access-Control-Allow-Methods','GET, POST, OPTIONS');self.send_header('Content-Type','application/json');self.end_headers()
    def do_OPTIONS(self):self.headers_out(204)
    def proxy(self):
        if self.command!='GET' and self.path!='/api/auth/login':self.headers_out(405);return
        data=self.rfile.read(int(self.headers.get('Content-Length',0))) if self.command=='POST' else None
        headers={k:v for k,v in self.headers.items() if k.lower() in ['authorization','content-type','accept-language']}
        try:
            request=urllib.request.Request('https://mis-api.runasp.net'+self.path,data=data,headers=headers,method=self.command)
            with urllib.request.urlopen(request,timeout=20) as response:self.headers_out(response.status);self.wfile.write(response.read())
        except urllib.error.HTTPError as e:self.headers_out(e.code);self.wfile.write(e.read())
        except Exception:self.headers_out(502);self.wfile.write(b'{"message":"QA proxy could not reach API"}')
    do_GET=proxy;do_POST=proxy
if os.environ.get('MIS_QA_RESUME') == '1':
    for name,target in [('app',results),('overlays',overlays),('accessibility',accessibility),('app-requests',requests)]:
        artifact=out/(f'app-{phase}-requests.json' if name=='app-requests' else f'{name}-{phase}.json')
        if artifact.exists():target.extend(json.loads(artifact.read_text(encoding='utf-8')))
    for name in ['dynamic-final-coverage.json','dynamic-coverage.json']:
        artifact=out/name
        if artifact.exists():links.update(x['route'] for x in json.loads(artifact.read_text(encoding='utf-8'))['visited'])
    artifact=out/f'links-{phase}.json'
    if artifact.exists():links.update(json.loads(artifact.read_text(encoding='utf-8')))
retest=set(filter(None,os.environ.get('MIS_QA_RETEST_ROUTES','').split(',')))
results[:]=[x for x in results if x['route'] not in retest]
overlays[:]=[x for x in overlays if x['route'] not in retest]
accessibility[:]=[x for x in accessibility if x['route'] not in retest]
completed={x['route'] for x in results}
capture_only=os.environ.get('MIS_QA_CAPTURE_ONLY')=='1'
captures=out/'screenshots';captures.mkdir(exist_ok=True)

proxy=ThreadingHTTPServer(('127.0.0.1',0),Proxy);threading.Thread(target=proxy.serve_forever,daemon=True).start()
driver.execute_cdp_cmd('Page.addScriptToEvaluateOnNewDocument',{'source':f'window.__qaApiOverride="http://127.0.0.1:{proxy.server_port}/api";'})
def resize(w):driver.execute_cdp_cmd('Emulation.setDeviceMetricsOverride',{'width':w,'height':950,'deviceScaleFactor':1,'mobile':False})
geometry='''
const visible=e=>{const r=e.getBoundingClientRect(),s=getComputedStyle(e);return r.width>1&&r.height>1&&s.visibility!=='hidden'&&s.display!=='none'&&Number(s.opacity)!==0&&!e.closest('[aria-hidden=true],[inert]')};
const scroller=e=>{let n=e.parentElement;while(n&&n!==document.body){const s=getComputedStyle(n);if(['auto','scroll'].includes(s.overflowX))return true;n=n.parentElement;}return false;};
const bad=[...document.querySelectorAll('main *')].filter(visible).filter(e=>e.innerText?.trim()||e.matches('input,select,textarea,button')).filter(e=>{const r=e.getBoundingClientRect();return (r.left < -2 || r.right > innerWidth+2)&&!scroller(e)&&!['SCRIPT','STYLE'].includes(e.tagName)});
return {viewport:innerWidth,scroll:document.documentElement.scrollWidth,title:document.querySelector('main h1,h1')?.innerText??'',overflow:bad.slice(0,12).map(e=>({tag:e.tagName,cls:String(e.className).slice(0,200),text:(e.innerText||'').slice(0,60),left:Math.round(e.getBoundingClientRect().left),right:Math.round(e.getBoundingClientRect().right)})),links:[...document.querySelectorAll('a[href]')].map(x=>x.getAttribute('href')),dialogs:document.querySelectorAll('[role=dialog],[role=alertdialog]').length,errors:window.__qa.errors.slice(-3)};
'''
def settle():
    start=time.monotonic();stable=0
    while time.monotonic()-start<20:
        pending=driver.execute_script('return window.__qa?.pending??0');stable=stable+1 if pending==0 else 0
        if stable>=3:return
        time.sleep(.1)
def inspect(route):
    if route in completed:return
    if not driver.execute_script('return !!window.__qa?.navigate'):
        driver.get(base+'?mode=app&route='+route);wait.until(lambda d:d.execute_script('return window.__qa?.ready'))
    driver.execute_script('window.__qa.navigate(arguments[0])',route)
    start=time.monotonic();stable=0
    while time.monotonic()-start<12:
        pending=driver.execute_script('return window.__qa.pending');stable=stable+1 if pending==0 else 0
        if stable>=3:break
        time.sleep(.2)
    requests.extend(driver.execute_script('return window.__qa.requests.splice(0)'))
    ax=driver.execute_cdp_cmd('Accessibility.getFullAXTree',{})['nodes']
    unnamed=[n for n in ax if not n.get('ignored') and n.get('role',{}).get('value') in ['button','link'] and not n.get('name',{}).get('value','').strip()]
    entries=[]
    for n in unnamed:
        try:
            node=driver.execute_cdp_cmd('DOM.describeNode',{'backendNodeId':n['backendDOMNodeId']})['node']
            entries.append({'tag':node['nodeName'],'attributes':node.get('attributes',[])})
        except Exception:entries.append({'role':n.get('role',{}).get('value')})
    accessibility.append({'route':route,'unnamedActions':entries})
    (out/f'app-{phase}-requests.json').write_text(json.dumps(requests,indent=2))
    (out/f'accessibility-{phase}.json').write_text(json.dumps(accessibility,ensure_ascii=False,indent=2),encoding='utf-8')
    page=[]
    for lang in ['en','ar']:
        driver.execute_script('window.__qa.language(arguments[0])',lang);settle()
        for width in ([1440 if lang=='en' else 390] if capture_only else [320,360,390,768,1024,1440]):
            resize(width);time.sleep(.25);r=driver.execute_script(geometry);links.update(x for x in r.pop('links') if x and x.startswith('/'))
            r.update(route=route,actual=driver.execute_script('return location.pathname'),lang=lang,width=width);page.append(r)
            if capture_only:
                driver.save_screenshot(str(captures/(lang+'-'+re.sub(r'[^a-zA-Z0-9]+','-',route)+'.png')))
            if r['overflow'] and width in [320,1024]:driver.save_screenshot(str(out/('app-'+phase+'-'+re.sub(r'[^a-zA-Z0-9]+','-',route)+'-'+lang+'-'+str(width)+'.png')))
    if os.environ.get('MIS_QA_OVERLAYS') == '1':
        driver.execute_script("window.__qa.language('en')");resize(1440);time.sleep(.2)
        settle()
        texts=driver.execute_script("return [...document.querySelectorAll('button')].filter(e=>e.getClientRects().length&&!e.disabled&&/^(New |Add |Create |Edit\\b|View\\b|Schedule\\b|Record\\b|Details$)/.test(e.innerText)).map(e=>e.innerText)")
        for text in list(dict.fromkeys(texts))[:4]:
            try:
                button=WebDriverWait(driver,5).until(lambda d:d.execute_script("return [...document.querySelectorAll('button')].find(e=>e.getClientRects().length&&!e.disabled&&e.innerText===arguments[0])??null",text))
                driver.execute_script('arguments[0].scrollIntoView({block:"center"})',button);button.click();time.sleep(.35)
                if not driver.find_elements(By.CSS_SELECTOR,'[role=dialog],[role=alertdialog]'):continue
                for lang in ['en','ar']:
                    driver.execute_script('window.__qa.language(arguments[0])',lang);time.sleep(.15)
                    for width in [320,390,768,1440]:
                        resize(width);time.sleep(.25)
                        boxes=driver.execute_script("return [...document.querySelectorAll('[role=dialog],[role=alertdialog]')].map(e=>{const r=e.getBoundingClientRect();return {left:r.left,right:r.right,top:r.top,bottom:r.bottom,fit:r.left>=-1&&r.right<=innerWidth+1&&r.top>=-1&&r.bottom<=innerHeight+1}})")
                        overlays.append({'route':route,'opener':text,'lang':lang,'width':width,'boxes':boxes})
                for attempt in range(4):
                    if not driver.find_elements(By.CSS_SELECTOR,'[role=dialog],[role=alertdialog]'):break
                    driver.switch_to.active_element.send_keys('\ue00c');time.sleep(.1)
                if driver.find_elements(By.CSS_SELECTOR,'[role=dialog],[role=alertdialog]'):
                    driver.execute_script('window.__qa.navigate("/modules")');time.sleep(.15);driver.execute_script('window.__qa.navigate(arguments[0])',route);time.sleep(.3)
            except Exception as e:overlays.append({'route':route,'opener':text,'error':type(e).__name__})
        (out/f'overlays-{phase}.json').write_text(json.dumps(overlays,ensure_ascii=False,indent=2),encoding='utf-8')
    results.extend(page);completed.add(route)
    (out/f'links-{phase}.json').write_text(json.dumps(sorted(links)))
    (out/f'app-{phase}.json').write_text(json.dumps(results,ensure_ascii=False,indent=2),encoding='utf-8')
    print(json.dumps({'route':route,'samples':len(page),'overflowSamples':sum(bool(x['overflow']) for x in page),'actual':page[-1]['actual'],'errors':page[-1]['errors']}),flush=True)
try:
    resize(1440);driver.get(base);wait.until(lambda d:d.execute_script('return window.__qa?.ready'))
    auth=driver.execute_async_script('''
      const [username,password,done]=arguments;
      window.__qa.api.post('/auth/login',{username,password}).then(r=>{sessionStorage.setItem('mis.auth',JSON.stringify(r.data));done({ok:true});}).catch(e=>done({ok:false,status:e.response?.status??0}));
    ''',os.environ.get('MIS_QA_USERNAME','admin'),os.environ['MIS_QA_PASSWORD'])
    if not auth['ok']:raise RuntimeError('Login failed: '+str(auth.get('status')))
    driver.get(base+'?mode=app&route=/modules');wait.until(lambda d:d.execute_script('return window.__qa?.ready'))
    driver.execute_async_script("const done=arguments[0];Promise.all([document.fonts.load('14px \"IBM Plex Sans Arabic\"'),document.fonts.load('14px \"IBM Plex Sans\"')]).then(()=>document.fonts.ready).then(()=>done());")
    routes=list(dict.fromkeys(x['path'] for x in inventory['routes'] if not x['redirect'] and not x['dynamic'] and '*' not in x['path'] and x['path'] not in ['/','/change-password','/login','/unauthorized']))
    for route in routes:inspect(route)
    patterns=list(dict.fromkeys(x['path'] for x in inventory['routes'] if not x['redirect'] and x['dynamic']))
    visited=[]
    for pattern in patterns:
        regex='^'+re.sub(r':[a-zA-Z]+','[^/?]+',pattern)+'$'
        match=next((x for x in sorted(links) if re.match(regex,x)),None)
        if match:inspect(match);visited.append({'pattern':pattern,'route':match})
    # Both organization kinds share a workspace but require separate shell checks.
    company=next((x for x in visited if x['pattern']=='/installment-companies/:companyId/:primary/:secondary'),None)
    if company:
        for pattern in patterns:
            if pattern.startswith('/installment-companies/') and pattern not in [x['pattern'] for x in visited] and '/customers/' not in pattern:
                route=pattern.replace('/installment-companies/:companyId/:primary/:secondary',company['route'])
                inspect(route);visited.append({'pattern':pattern,'route':route})
    # Audit missing detail routes using explicit synthetic responses only in the QA entry.
    fixtures=json.loads(Path('frontend/qa/detail-fixtures.json').read_text(encoding='utf-8'))
    fid=fixtures['id'];arname=fixtures['longArabic'];enname=fixtures['longEnglish'];token=fixtures['unbroken']
    journal=fixtures['journal'];journal.update(description=token,createdBy=enname,postedBy=arname)
    journal['lines']=[{'id':str(i),'lineNumber':i+1,'accountCode':'1234567890','accountNameArabic':arname,'accountNameEnglish':enname,'description':token,'debit':9999999999999.99 if i==0 else 0,'credit':9999999999999.99 if i==1 else 0} for i in range(2)]
    client=fixtures['client'];client.update(customerNameArabic=arname,customerNameEnglish=enname,organizationName=enname,address=token,fields={'long-field':token})
    legal=fixtures['legal'];legal.update(customerName=arname,organizationName=enname,portfolioName=token,receivedByName=enname,notes=token)
    visit=fixtures['visit'];visit.update(employeeName=arname,customer=enname,organization=enname,address=token,visitNotes=token,visitCreatedBy=enname)
    data={f'/finance/journals/{fid}':journal,f'/data-entry/clients/{fid}':client,f'/data-entry/clients/{fid}/documents':[],f'/legal/cases/{fid}':legal,f'/hr/excuses/visits/{fid}':visit}
    driver.execute_script('Object.assign(window.__qa.fixtures,arguments[0])',data)
    for pattern in ['/finance/journals/:id','/data-entry/clients/:id','/legal/cases/:id','/hr/excuses/visits/:visitId']:
        route=re.sub(r':[a-zA-Z]+',fid,pattern);inspect(route);visited.append({'pattern':pattern,'route':route,'synthetic':True})
    for kind in ['banks','installment-companies']:
        existing=next((x for x in visited if x['route'].startswith('/'+kind+'/') and x['route'].endswith('/customers')),None)
        if existing:
            route=existing['route']+'/import';inspect(route)
            pattern=next((x for x in patterns if x.startswith('/'+kind+'/') and x.endswith('/customers/import')),None)
            if pattern:visited.append({'pattern':pattern,'route':route})
            customerRoute=existing['route']+'/'+fid
            organizationId=existing['route'].split('/')[2]
            customer={'id':fid,'customerCode':token,'customerName':arname,'organizationName':enname,'organizationType':'BANK','address':token,'totalOriginalAmount':9999999999999.99,'totalOutstandingAmount':-9999999999999.99,'totalPaidAmount':0,'totalRemainingAmount':9999999999999.99,'totalOverdueAmount':9999999999999.99,'cases':[],'payments':[],'promises':[],'visits':[],'complaints':[],'activities':[],'timeline':[]}
            driver.execute_script('window.__qa.fixtures[arguments[0]]=arguments[1]',f'/banks/{organizationId}/customers/{fid}',customer)
            inspect(customerRoute)
            pattern=next((x for x in patterns if x.startswith('/'+kind+'/') and x.endswith('/customers/:customerId')),None)
            if pattern:visited.append({'pattern':pattern,'route':customerRoute,'synthetic':True})
    driver.execute_script('window.__qa.navigate("/finance/journals")');time.sleep(.8)
    navigation=[]
    for lang in ['en','ar']:
        driver.execute_script('window.__qa.language(arguments[0])',lang)
        for width in [320,360,390,768]:
            resize(width);time.sleep(.2)
            menu=driver.find_element(By.CSS_SELECTOR,'header button[aria-expanded]');menu.click();time.sleep(.2)
            nav=driver.find_element(By.CSS_SELECTOR,'aside.module-sidebar')
            locked=driver.execute_script('return document.body.style.overflow==="hidden"')
            box=driver.execute_script('const r=arguments[0].getBoundingClientRect();return r.left>=-1&&r.right<=innerWidth+1',nav)
            driver.switch_to.active_element.send_keys('\ue00c');time.sleep(.2)
            restored=driver.switch_to.active_element==menu
            inert=nav.get_attribute('inert') is not None
            navigation.append({'lang':lang,'width':width,'contained':box,'scrollLocked':locked,'focusRestored':restored,'closedInert':inert})
        resize(390);driver.find_element(By.CSS_SELECTOR,'header button[aria-expanded]').click();resize(1024);time.sleep(.3)
        navigation.append({'lang':lang,'width':1024,'resizeUnlocksScroll':driver.execute_script('return document.body.style.overflow!=="hidden"')})
        driver.execute_script('window.__qa.navigate("/modules")');time.sleep(.2);driver.execute_script('window.__qa.navigate("/finance/journals")');time.sleep(.5)
    (out/f'navigation-{phase}.json').write_text(json.dumps(navigation,indent=2))
    inspect('/');inspect('/unauthorized')
    driver.execute_script('const a=JSON.parse(sessionStorage.getItem("mis.auth"));a.user.mustChangePassword=true;sessionStorage.setItem("mis.auth",JSON.stringify(a));')
    driver.get(base+'?mode=app&route=/change-password');wait.until(lambda d:d.execute_script('return window.__qa?.ready'))
    inspect('/change-password')
    driver.execute_script('sessionStorage.removeItem("mis.auth");localStorage.removeItem("mis.auth")')
    driver.get(base+'?mode=app&route=/login');wait.until(lambda d:d.execute_script('return window.__qa?.ready'))
    inspect('/login')

    (out/f'dynamic-{phase}-coverage.json').write_text(json.dumps({'patterns':patterns,'visited':visited,'unreachable':[p for p in patterns if p not in [x['pattern'] for x in visited]]},indent=2))
    (out/f'app-{phase}-requests.json').write_text(json.dumps(requests+driver.execute_script('return window.__qa.requests'),indent=2))
    print('COMPLETE',len(results),'viewport/language samples',flush=True)
finally:driver.quit();proxy.shutdown();proxy.server_close()
