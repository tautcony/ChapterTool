window.chapterToolDemo = {
  downloadText: function (fileName, content) {
    const blob = new Blob([content], { type: 'text/plain;charset=utf-8' });
    const url = URL.createObjectURL(blob);
    const anchor = document.createElement('a');
    anchor.href = url;
    anchor.download = fileName || 'chapters.txt';
    anchor.click();
    URL.revokeObjectURL(url);
  },
  click: function (elementId) {
    const el = document.getElementById(elementId);
    if (el) {
      el.click();
    }
  },
  alertDropHint: function () {
    // Keep quiet: Load button is the Avalonia-equivalent entry point.
  }
};
