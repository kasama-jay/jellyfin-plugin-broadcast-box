(() => {
  if (window.__broadcastBoxWeb) return;
  window.__broadcastBoxWeb = true;
  const log = (...args) => console.info('[Broadcast Box]', ...args);
  const params = () => new URLSearchParams((location.hash.split('?')[1] || ''));
  const itemId = () => params().get('id');
  const request = (type, path, data) => ApiClient.fetch({ type, url: ApiClient.getUrl(path), data: data && JSON.stringify(data), contentType: 'application/json' })
    .then(result => result && typeof result.json === 'function' ? result.json() : result);

  function openControl() {
    const id = itemId();
    if (!id) return;
    location.hash = `#/details?id=${encodeURIComponent(id)}&serverId=${encodeURIComponent(params().get('serverId') || '')}&broadcastBox=true`;
  }

  function addMenuItem() {
    const sheet = document.querySelector('.actionSheet');
    if (!sheet || sheet.querySelector('.broadcastBoxMenuItem')) return;
    const target = sheet.querySelector('.actionSheetContent');
    if (!target || !itemId()) return;
    const button = document.createElement('button');
    button.type = 'button';
    button.className = 'actionSheetMenuItem emby-button broadcastBoxMenuItem';
    button.innerHTML = '<span class="material-icons">live_tv</span><span class="actionSheetItemText">Broadcast</span>';
    button.onclick = openControl;
    target.appendChild(button);
    log('Broadcast item added to More menu', itemId());
  }

  function renderControl() {
    if (params().get('broadcastBox') !== 'true') return false;
    const id = itemId();
    const detail = document.querySelector('#itemDetailPage');
    if (!id || !detail) return false;
    let page = document.querySelector('#broadcastBoxControlPage');
    if (!page) {
      page = document.createElement('div');
      page.id = 'broadcastBoxControlPage';
      page.className = 'page libraryPage';
      page.innerHTML = `<div class="padded pageContainer"><h1>Broadcast</h1><p>Item: <code>${id}</code></p><p id="broadcastBoxLink"></p><button is="emby-button" id="broadcastBoxPlay" class="raised button-submit">Play</button> <button is="emby-button" id="broadcastBoxPause" class="raised">Pause</button> <button is="emby-button" id="broadcastBoxStop" class="raised button-cancel">Stop</button><pre id="broadcastBoxStatus"></pre></div>`;
      detail.parentElement.appendChild(page);
      page.querySelector('#broadcastBoxPlay').onclick = () => ApiClient.getPluginConfiguration('b4d3d127-a4cb-4d11-aab9-8b29fe6b74f4').then(config => request('POST', 'BroadcastBox/session', { itemId: id, preset: config.DefaultPreset ?? 1 })).then(refresh).catch(showError);
      page.querySelector('#broadcastBoxPause').onclick = () => request('POST', `BroadcastBox/items/${id}/pause`).then(refresh).catch(showError);
      page.querySelector('#broadcastBoxStop').onclick = () => request('DELETE', 'BroadcastBox/session').then(refresh).catch(showError);
    }
    detail.style.display = 'none'; page.style.display = '';
    refresh(); return true;
  }
  function showError(error) { document.querySelector('#broadcastBoxStatus').textContent = error.message || String(error); }
  function refresh() { request('GET', 'BroadcastBox/session').then(session => { const status=document.querySelector('#broadcastBoxStatus'); if (status) status.textContent=JSON.stringify(session,null,2); const link=document.querySelector('#broadcastBoxLink'); if(link && session.viewerUrl) link.innerHTML=`Watch: <a target="_blank" href="${session.viewerUrl}">${session.viewerUrl}</a>`; }).catch(showError); }
  document.addEventListener('click', event => { if (event.target.closest('.btnMoreCommands')) setTimeout(addMenuItem, 150); });
  const observer = new MutationObserver(() => { addMenuItem(); renderControl(); });
  observer.observe(document.documentElement, { childList: true, subtree: true });
  window.addEventListener('hashchange', () => { document.querySelector('#broadcastBoxControlPage')?.remove(); const detail=document.querySelector('#itemDetailPage'); if(detail) detail.style.display=''; renderControl(); });
  log('Web integration loaded');
})();
