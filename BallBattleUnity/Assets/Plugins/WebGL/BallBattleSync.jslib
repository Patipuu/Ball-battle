// WebGL: Application.persistentDataPath lives in an in-memory file system that is only copied to IndexedDB on FS.syncfs.
// FileSaveStore calls this after every save so progress survives a page reload (itch.io players would otherwise lose it).
mergeInto(LibraryManager.library, {
  BB_SyncPersistentData: function () {
    if (typeof FS === 'undefined' || !FS.syncfs) return;
    FS.syncfs(false, function (err) { if (err) console.warn('[BallBattle] save sync failed: ' + err); });
  },

  // TextEntry (account name / password): an HTML box over the canvas so phones open their own keyboard.
  // The answer goes back with SendMessage(objectName, 'OnTextEntry' | 'OnTextEntryCancel', value).
  BB_OpenTextInput: function (objectName, title, value, password, maxLength) {
    var obj = UTF8ToString(objectName), head = UTF8ToString(title), start = UTF8ToString(value);
    var old = document.getElementById('bb-text-entry');
    if (old) old.parentNode.removeChild(old);
    var shade = document.createElement('div');
    shade.id = 'bb-text-entry';
    shade.style.cssText = 'position:fixed;inset:0;z-index:99999;background:rgba(10,10,18,0.78);display:flex;align-items:center;justify-content:center;font-family:monospace';
    var box = document.createElement('div');
    box.style.cssText = 'background:#181826;border:3px solid #f4c542;padding:16px;width:min(86vw,360px);color:#fff;box-sizing:border-box';
    var label = document.createElement('div');
    label.textContent = head;
    label.style.cssText = 'font-size:16px;margin-bottom:10px;letter-spacing:1px';
    var input = document.createElement('input');
    input.type = password ? 'password' : 'text';
    input.value = start;
    input.maxLength = maxLength;
    input.autocomplete = password ? 'current-password' : 'username';
    input.autocapitalize = password ? 'off' : 'characters';
    input.spellcheck = false;
    input.style.cssText = 'width:100%;font-size:20px;padding:8px;box-sizing:border-box;background:#0d0d16;color:#fff;border:2px solid #888';
    var row = document.createElement('div');
    row.style.cssText = 'display:flex;gap:10px;margin-top:12px';
    function button(text, primary) {
      var b = document.createElement('button');
      b.textContent = text;
      b.style.cssText = 'flex:1;font-size:16px;padding:10px;font-family:monospace;cursor:pointer;border:2px solid ' + (primary ? '#f4c542' : '#888') +
        ';background:' + (primary ? '#f4c542' : '#222') + ';color:' + (primary ? '#111' : '#ddd');
      return b;
    }
    var cancel = button('X', false), ok = button('OK', true);
    function close(send, method, v) {
      if (shade.parentNode) shade.parentNode.removeChild(shade);
      SendMessage(obj, method, v);
    }
    ok.onclick = function () { close(true, 'OnTextEntry', input.value); };
    cancel.onclick = function () { close(true, 'OnTextEntryCancel', ''); };
    input.onkeydown = function (e) {
      e.stopPropagation();
      if (e.key === 'Enter') ok.onclick();
      else if (e.key === 'Escape') cancel.onclick();
    };
    input.onkeyup = input.onkeypress = function (e) { e.stopPropagation(); };
    row.appendChild(cancel); row.appendChild(ok);
    box.appendChild(label); box.appendChild(input); box.appendChild(row);
    shade.appendChild(box);
    document.body.appendChild(shade);
    setTimeout(function () { input.focus(); }, 30);
  }
});
