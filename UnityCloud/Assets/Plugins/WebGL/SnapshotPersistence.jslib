mergeInto(LibraryManager.library, {
  RlsObserverReceipt__deps: ['$UTF8ToString'],
  RlsObserverReceipt: function(pointer) { window.rlsObserverReceipt = JSON.parse(UTF8ToString(pointer)); },
  RlsSyncIdbfs__deps: ['$FS'],
  RlsSyncIdbfs: function() {
    FS.syncfs(false, function(error) {
      if (error) console.error('RLS snapshot persistence failed', error);
      else console.log('RLS_SNAPSHOT_PERSISTED');
    });
  }
});
