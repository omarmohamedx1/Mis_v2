$ErrorActionPreference = 'Stop'
$origin = 'http://127.0.0.1:5173'
$debugPort = 9233
$profile = 'C:\Users\omarm\MIS\.tmp-select-chrome-' + [DateTimeOffset]::UtcNow.ToUnixTimeMilliseconds()
$chrome = 'C:\Program Files\Google\Chrome\Application\chrome.exe'

$secretLine = dotnet user-secrets list --project 'C:\Users\omarm\MIS\backend\MIS.API' | Where-Object { $_ -like 'Seed:HrPassword = *' } | Select-Object -First 1
if (-not $secretLine) { throw 'HR test credential was not found.' }
$password = $secretLine.Substring($secretLine.IndexOf(' = ') + 3)
$body = @{ username = 'hr.user'; password = $password } | ConvertTo-Json
$auth = Invoke-RestMethod -Method Post -Uri 'http://127.0.0.1:5000/api/auth/login' -ContentType 'application/json' -Body $body
$auth64 = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes(($auth | ConvertTo-Json -Depth 20 -Compress)))

$browser = Start-Process -FilePath $chrome -ArgumentList @('--headless=new','--disable-gpu','--disable-crash-reporter',"--remote-debugging-port=$debugPort","--user-data-dir=$profile",'--window-size=1440,1000',"$origin/login") -WindowStyle Hidden -PassThru
try {
  $version = $null
  for ($i=0; $i -lt 50 -and -not $version; $i++) { Start-Sleep -Milliseconds 200; try { $version=Invoke-RestMethod "http://127.0.0.1:$debugPort/json/version" } catch {} }
  if (-not $version) { throw 'Chrome did not start.' }
  $tab = (Invoke-RestMethod "http://127.0.0.1:$debugPort/json") | Where-Object { $_.type -eq 'page' -and $_.url -like "$origin*" } | Select-Object -First 1
  $ws=[Net.WebSockets.ClientWebSocket]::new(); $ws.ConnectAsync([Uri]$tab.webSocketDebuggerUrl,[Threading.CancellationToken]::None).GetAwaiter().GetResult() | Out-Null
  $script:id=0
  function Cdp([string]$method,[hashtable]$params=@{}) {
    $script:id++; $id=$script:id; $json=@{id=$id;method=$method;params=$params}|ConvertTo-Json -Depth 30 -Compress; $bytes=[Text.Encoding]::UTF8.GetBytes($json)
    $ws.SendAsync([ArraySegment[byte]]::new($bytes),[Net.WebSockets.WebSocketMessageType]::Text,$true,[Threading.CancellationToken]::None).GetAwaiter().GetResult() | Out-Null
    while($true){$stream=[IO.MemoryStream]::new();do{$buffer=New-Object byte[] 65536;$received=$ws.ReceiveAsync([ArraySegment[byte]]::new($buffer),[Threading.CancellationToken]::None).GetAwaiter().GetResult();$stream.Write($buffer,0,$received.Count)}while(-not $received.EndOfMessage);$message=[Text.Encoding]::UTF8.GetString($stream.ToArray())|ConvertFrom-Json;if($message.id -eq $id){return $message}}
  }
  function Js([string]$expression){$response=Cdp 'Runtime.evaluate' @{expression=$expression;returnByValue=$true;awaitPromise=$true};if($response.result.exceptionDetails){throw "$($response.result.exceptionDetails.exception.description) EXPR=$expression"};return $response.result.result.value}
  function WaitJs([string]$expression){for($i=0;$i -lt 80;$i++){Start-Sleep -Milliseconds 150;if(Js $expression){return}};throw "Timed out: $expression"}
  function Shot([string]$path){$capture=Cdp 'Page.captureScreenshot' @{format='png';fromSurface=$true};[IO.File]::WriteAllBytes($path,[Convert]::FromBase64String($capture.result.data))}

  WaitJs "location.href.startsWith('$origin') && document.readyState === 'complete'"
  Js "sessionStorage.setItem('mis.auth',atob('$auth64'));localStorage.setItem('mis.language','ar');localStorage.setItem('mis.hr.sidebar','expanded');true" | Out-Null
  Cdp 'Page.navigate' @{url="$origin/hr/employees"} | Out-Null
  WaitJs "document.querySelectorAll('button[aria-haspopup=listbox]').length > 0"
  Start-Sleep -Milliseconds 500
  Js "document.querySelector('button[aria-haspopup=listbox]')?.click();true" | Out-Null
  WaitJs "Boolean(document.querySelector('[role=listbox]'))"
  Start-Sleep -Milliseconds 300
  $desktop = Js @"
(() => { const menu=document.querySelector('[role="listbox"]').closest('section'); const r=menu.getBoundingClientRect(); return {dir:document.documentElement.dir,viewport:innerWidth,documentWidth:document.documentElement.scrollWidth,nativeSelects:[...document.querySelectorAll('select')].filter(x=>x.getBoundingClientRect().width>2).length,triggers:document.querySelectorAll('button[aria-haspopup="listbox"]').length,options:document.querySelectorAll('[role="option"]').length,menu:{left:Math.round(r.left),right:Math.round(r.right),top:Math.round(r.top),bottom:Math.round(r.bottom)},search:Boolean(menu.querySelector('input'))}; })()
"@
  Shot 'C:\Users\omarm\MIS\.tmp-select-desktop.png'
  Js "document.querySelectorAll('[role=option]')[1]?.click();true" | Out-Null
  WaitJs "!document.querySelector('[role=listbox]')"
  Start-Sleep -Milliseconds 250
  $selection = Js "({trigger:document.querySelector('button[aria-haspopup=listbox]')?.innerText.trim(),value:document.querySelector('select')?.value})"
  Cdp 'Emulation.setDeviceMetricsOverride' @{width=390;height=844;deviceScaleFactor=1;mobile=$true} | Out-Null
  Cdp 'Page.navigate' @{url="$origin/hr/employees"} | Out-Null
  WaitJs "document.querySelectorAll('button[aria-haspopup=listbox]').length > 0"
  Js "document.querySelector('button[aria-haspopup=listbox]')?.click();true" | Out-Null
  WaitJs "Boolean(document.querySelector('[role=listbox]'))"
  Start-Sleep -Milliseconds 300
  $mobile = Js @"
(() => { const menu=document.querySelector('[role="listbox"]').closest('section'); const r=menu.getBoundingClientRect(); return {dir:document.documentElement.dir,viewport:innerWidth,documentWidth:document.documentElement.scrollWidth,nativeSelects:[...document.querySelectorAll('select')].filter(x=>x.getBoundingClientRect().width>2).length,options:document.querySelectorAll('[role="option"]').length,menu:{left:Math.round(r.left),right:Math.round(r.right),top:Math.round(r.top),bottom:Math.round(r.bottom)},backdrop:Boolean(document.querySelector('button.fixed.inset-0.bg-slate-950\\/45'))}; })()
"@
  Shot 'C:\Users\omarm\MIS\.tmp-select-mobile.png'

  Js "document.querySelector('button.fixed.inset-0')?.click();localStorage.setItem('mis.language','en');true" | Out-Null
  Cdp 'Emulation.setDeviceMetricsOverride' @{width=1440;height=1000;deviceScaleFactor=1;mobile=$false} | Out-Null
  Cdp 'Page.navigate' @{url="$origin/hr/employees"} | Out-Null
  WaitJs "document.documentElement.dir === 'ltr' && document.querySelectorAll('button[aria-haspopup=listbox]').length > 0"
  $english = Js "({dir:document.documentElement.dir,documentWidth:document.documentElement.scrollWidth,viewport:innerWidth,nativeSelects:[...document.querySelectorAll('select')].filter(x=>x.getBoundingClientRect().width>2).length})"
  [pscustomobject]@{desktopArabic=$desktop;selection=$selection;mobileArabic=$mobile;desktopEnglish=$english}|ConvertTo-Json -Depth 10
  Cdp 'Browser.close' | Out-Null; $ws.Dispose()
} finally {
  if(-not $browser.HasExited){Stop-Process -Id $browser.Id -Force -ErrorAction SilentlyContinue}
  Start-Sleep -Milliseconds 300
  if(Test-Path -LiteralPath $profile){Remove-Item -LiteralPath $profile -Recurse -Force}
}
