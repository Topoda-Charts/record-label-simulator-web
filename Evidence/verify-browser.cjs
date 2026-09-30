const { chromium } = require('playwright');
const fs = require('node:fs');
const path = require('node:path');
const url = process.argv[2] || 'http://localhost:8080';
const checkpoint = process.argv[3] || 'CU-05';
if (!/^CU-\d{2}$/.test(checkpoint)) throw new Error('Expected a CU-NN checkpoint');
const directory = path.join(__dirname, 'Captures', checkpoint);
fs.mkdirSync(directory,{recursive:true});
(async()=>{
 const browser = await chromium.launch({executablePath:'C:/Program Files/Google/Chrome/Application/chrome.exe',headless:true,args:['--enable-webgl','--enable-unsafe-swiftshader']});
 const deadline = setTimeout(()=>browser.close().catch(()=>{}),15*60*1000);
 const errors=[],messages=[],errorCounts={};
 const timings={},startedAt=Date.now();
 let browserGraphics;
 const recordError=text=>{errorCounts[text]=(errorCounts[text]||0)+1;if(!errors.includes(text))errors.push(text);};
 const page = await browser.newPage({viewport:{width:1600,height:900}});
 page.on('console',msg=>{if(messages.length<2000)messages.push({type:msg.type(),text:msg.text()});if(msg.type()==='error')recordError(msg.text());});
 page.on('pageerror',err=>recordError(err.message));
 const send = async method=>page.evaluate(method=>window.rlsUnityInstance.SendMessage('RLS Observer Application',method,''),method);
 const summary = async()=>{await send('Summary');return page.evaluate(()=>window.rlsObserverReceipt);};
 const interactions = [];
 const click = async text=>{
  const receipt = await summary();
  const control = receipt.controls?.find(control=>control.text===text && control.x>0 && control.x<1600 && control.y>0 && control.y<900);
  if(!control) throw new Error('Visible Unity control missing: '+text);
  await page.mouse.click(control.x,control.y);
  await page.waitForTimeout(450);
 };
 const capture = async name=>{
  await page.screenshot({path:path.join(directory,name+'.png')});
  process.stdout.write('RLS_CAPTURE_READY '+name+'.png\n');
 };
 const verifyUi = Number(checkpoint.slice(3))>=6;
 try {
  await page.goto(url,{waitUntil:'domcontentloaded',timeout:60000});
  await page.waitForFunction(()=>window.rlsUnityInstance,{timeout:240000});
  timings.playerReadySeconds=(Date.now()-startedAt)/1000;
  browserGraphics=await page.evaluate(()=>{
   const canvas=document.querySelector('canvas');
   const gl=canvas?.getContext('webgl2')||canvas?.getContext('webgl');
   if(!gl)return null;
   const ext=gl.getExtension('WEBGL_debug_renderer_info');
   return {renderer:ext?gl.getParameter(ext.UNMASKED_RENDERER_WEBGL):gl.getParameter(gl.RENDERER)};
  });
  await page.waitForTimeout(2500);
  await page.screenshot({path:path.join(directory,'01-menu.png')});
  process.stdout.write('RLS_CAPTURE_READY 01-menu.png\n');
  if(verifyUi) await click('Watch the World Grow'); else await send('Watch');
  await page.waitForTimeout(4000);
  const january=await summary();
  if(january.state!=='Observer'||!january.date.startsWith('2425-01-01')||january.visibleRenderers<10)throw new Error('Unity January observer readback failed');
  await page.screenshot({path:path.join(directory,'02-january-world.png')});
  fs.writeFileSync(path.join(__dirname,checkpoint+'-january.json'),JSON.stringify(january,null,2));
  process.stdout.write('RLS_CAPTURE_READY 02-january-world.png\n');
  if(verifyUi) {
   await click('Play');
   await page.waitForTimeout(1600);
   let playing=await summary();
   if(playing.paused)throw new Error('Visible Play button did not start the observer');
   await click('Pause');
   const paused=await summary();
   await click('Step');
   const stepped=await summary();
   if(!stepped.paused || Date.parse(stepped.date)-Date.parse(paused.date)!==300000)throw new Error('Visible Step did not advance exactly one paused five-minute tick');
   interactions.push({name:'play-pause-step',passed:true});
   await page.mouse.move(650,360);
   let before=await summary();
   await page.mouse.wheel(0,-120);await page.waitForTimeout(900);
   let zoomed=await summary();
   if(!(zoomed.cameraDistance<before.cameraDistance-0.5 && zoomed.cameraDistance>before.cameraDistance*0.65))throw new Error('One wheel notch did not produce a controlled zoom in');
   await page.mouse.wheel(0,120);await page.waitForTimeout(900);
   const reversed=await summary();
   if(Math.abs(reversed.cameraDistance-before.cameraDistance)>1.5)throw new Error('Opposite wheel notch did not restore the previous scale');
   interactions.push({name:'wheel-zoom',before:before.cameraDistance,zoomed:zoomed.cameraDistance,reversed:reversed.cameraDistance,passed:true});
   await click('Inspect');
   before=await summary();await page.mouse.move(1450,350);await page.mouse.wheel(0,120);await page.waitForTimeout(650);
   const hudScrolled=await summary();
   if(Math.abs(hudScrolled.cameraDistance-before.cameraDistance)>0.1)throw new Error('Scrolling the inspector moved the camera');
   await capture('05-label-inspector');
   await click('Inspect');
   let members=await summary();
   const candidates=members.memberPoints.filter(point=>point.x>120&&point.x<1180&&point.y>130&&point.y<750).sort((a,b)=>Math.abs(a.x-750)+Math.abs(a.y-400)-Math.abs(b.x-750)-Math.abs(b.y-400));
   let selected;
   for(const point of candidates.slice(0,4)) {
    await page.mouse.click(point.x,point.y);await page.waitForTimeout(450);selected=await summary();
    if(selected.selectedMember)break;
   }
   if(!selected?.selectedMember)throw new Error('Mouse click could not select a visible member');
   await click('Follow');await page.waitForTimeout(1200);
   const following=await summary();
   if(following.followingMember!==selected.selectedMember||following.cameraDistance>16||following.followingOccluded)throw new Error('Follow button did not produce an unobstructed member-follow view');
   await capture('06-member-follow');
   await page.mouse.move(650,360);await page.mouse.wheel(0,120);await page.waitForTimeout(750);
   const cancelled=await summary();
   if(cancelled.followingMember)throw new Error('Manual zoom did not cancel member following');
   interactions.push({name:'member-selection-follow-cancel',member:selected.selectedMember,passed:true});
   await click('Inspect');
   await page.mouse.click(500,250);await page.keyboard.press('Home');await page.waitForTimeout(500);
  }
  await send('September');
  await page.waitForTimeout(3000);
  const september=await summary();
  if(!september.date.startsWith('2425-09-30')||september.tracks<1||september.events<=january.events)throw new Error('September simulation progression failed');
  await page.screenshot({path:path.join(directory,'03-september-world.png')});
  process.stdout.write('RLS_CAPTURE_READY 03-september-world.png\n');
  if(verifyUi) {
   await click('Production');
   let production=await summary();
   const completed=production.controls.find(control=>control.text.includes('\n')&&control.text.includes('Track'));
   if(completed){await page.mouse.click(completed.x,completed.y);await page.waitForTimeout(400);production=await summary();}
   if(!production.hudText.includes('Production chain'))throw new Error('Production tray did not show the actual output chain');
   await capture('07-production-chain');await click('Production');
   await click('History');await click('Release');await capture('08-release-history');await click('Latest');
   await click('Snapshots');await click('Create named snapshot');await click('Close');
   interactions.push({name:'production-history-named-snapshot-controls',passed:true});
  }
  await send('RoundTrip');
  const saved=await summary();
  if(!saved.snapshotRoundTrip)throw new Error('Browser snapshot round-trip failed');
  await page.waitForFunction(()=>true,{timeout:1000});
  await page.waitForTimeout(4000);
  await page.reload({waitUntil:'domcontentloaded'});
  await page.waitForFunction(()=>window.rlsUnityInstance,{timeout:240000});
  if(verifyUi){await page.waitForTimeout(2500);await click('Watch the World Grow');}else await send('Watch');
  await page.waitForTimeout(4000);
  await send('LoadLatest');await page.waitForTimeout(1500);
  const reloaded=await summary();
  if(reloaded.digest!==saved.digest)throw new Error('Browser reload lost the persisted snapshot');
  await page.screenshot({path:path.join(directory,'04-reloaded-snapshot.png')});
  timings.totalCheckSeconds=(Date.now()-startedAt)/1000;
  const result={checkpoint,url,timings,browserGraphics,january,september,saved,reloaded,interactions,errors,errorCounts,passed:errors.length===0};
  fs.writeFileSync(path.join(__dirname,checkpoint+'-browser.json'),JSON.stringify(result,null,2));
  fs.writeFileSync(path.join(__dirname,checkpoint+'-browser-console.json'),JSON.stringify(messages,null,2));
  process.stdout.write(JSON.stringify({checkpoint,passed:result.passed,timings,browserGraphics,january:january.date,september:september.date,tracks:september.tracks,reloadDigestMatches:reloaded.digest===saved.digest,interactions,errors},null,2)+'\n');
  if(errors.length)process.exitCode=1;
 } catch(error) {
  fs.writeFileSync(path.join(__dirname,checkpoint+'-browser-failure.json'),JSON.stringify({url,error:error.stack,errors,messages},null,2));
  await page.screenshot({path:path.join(directory,'failure.png')}).catch(()=>{});
  throw error;
 } finally {
  clearTimeout(deadline);
  fs.writeFileSync(path.join(__dirname,checkpoint+'-browser-console.json'),JSON.stringify(messages,null,2));
  await browser.close();
 }
})().catch(error=>{process.stderr.write(error.stack+'\n');process.exitCode=1;});
