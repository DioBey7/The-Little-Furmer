// Browser-side helpers for the itch.io web build.
//
// Why localStorage and not Application.persistentDataPath:
// on WebGL, persistentDataPath is "/idbfs/<hash of the page URL>". itch.io serves
// every upload from a new URL (html-classic.itch.zone/html/<uploadId>/), so file
// saves are orphaned each time a new build is uploaded. localStorage is keyed by
// origin, so it survives re-uploads. The origin is shared with other itch games,
// so every key is namespaced by the C# side (see WebStorage.cs).
mergeInto(LibraryManager.library, {

  WebStorage_Save: function (keyPtr, valuePtr) {
    try {
      window.localStorage.setItem(UTF8ToString(keyPtr), UTF8ToString(valuePtr));
      return 1;
    } catch (e) {
      console.warn("[WebStorage] save failed: " + e);
      return 0;
    }
  },

  WebStorage_Load: function (keyPtr) {
    var value = null;
    try {
      value = window.localStorage.getItem(UTF8ToString(keyPtr));
    } catch (e) {
      console.warn("[WebStorage] load failed: " + e);
    }
    if (value === null || value === undefined) return 0; // null string in C#
    var size = lengthBytesUTF8(value) + 1;
    var buffer = _malloc(size);
    stringToUTF8(value, buffer, size);
    return buffer;
  },

  WebStorage_HasKey: function (keyPtr) {
    try {
      return window.localStorage.getItem(UTF8ToString(keyPtr)) !== null ? 1 : 0;
    } catch (e) {
      return 0;
    }
  },

  WebStorage_Delete: function (keyPtr) {
    try {
      window.localStorage.removeItem(UTF8ToString(keyPtr));
    } catch (e) {
      console.warn("[WebStorage] delete failed: " + e);
    }
  },

  // Calls the C# callback when the tab is hidden or the page is being closed,
  // which is the last reliable moment to save in a browser.
  WebPlatform_RegisterPageHide: function (callback) {
    var fire = function () {
      try { {{{ makeDynCall('v', 'callback') }}}(); } catch (e) { console.warn(e); }
      // Flush anything written to the IndexedDB-backed file system (PlayerPrefs, files).
      try { if (typeof FS !== "undefined" && FS.syncfs) FS.syncfs(false, function () {}); } catch (e) {}
    };
    document.addEventListener("visibilitychange", function () {
      if (document.visibilityState === "hidden") fire();
    });
    window.addEventListener("pagehide", fire);
  },

  WebPlatform_IsMobile: function () {
    var ua = navigator.userAgent || "";
    var touchMac = /Macintosh/.test(ua) && navigator.maxTouchPoints > 1; // iPadOS
    return (/Android|iPhone|iPad|iPod|Mobile/i.test(ua) || touchMac) ? 1 : 0;
  }
});
