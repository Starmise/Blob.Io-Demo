mergeInto(LibraryManager.library, {
  MobileControls_IsMobileBrowser: function () {
    if (typeof navigator === "undefined") return 0;
    var ua = navigator.userAgent || "";
    if (/Android|webOS|iPhone|iPad|iPod|BlackBerry|IEMobile|Opera Mini/i.test(ua)) return 1;
    if (typeof window !== "undefined" && window.innerWidth <= 1024 && navigator.maxTouchPoints > 0) return 1;
    return 0;
  }
});
