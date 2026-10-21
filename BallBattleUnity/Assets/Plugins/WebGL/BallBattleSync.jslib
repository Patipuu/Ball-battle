// WebGL: Application.persistentDataPath lives in an in-memory file system that is only copied to IndexedDB on FS.syncfs.
// FileSaveStore calls this after every save so progress survives a page reload (itch.io players would otherwise lose it).
mergeInto(LibraryManager.library, {
  BB_SyncPersistentData: function () {
    if (typeof FS === 'undefined' || !FS.syncfs) return;
    FS.syncfs(false, function (err) { if (err) console.warn('[BallBattle] save sync failed: ' + err); });
  }
});
