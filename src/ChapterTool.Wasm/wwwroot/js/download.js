window.chapterToolWasm = {
  downloadText: function (fileName, content, encodingId, emitBom) {
    const bytes = encodeText(content || '', encodingId || 'utf8', emitBom !== false);
    const blob = new Blob([bytes], { type: 'text/plain' });
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
  copyText: async function (text) {
    if (navigator.clipboard && window.isSecureContext) {
      await navigator.clipboard.writeText(text || '');
      return;
    }
    const area = document.createElement('textarea');
    area.value = text || '';
    area.style.position = 'fixed';
    area.style.opacity = '0';
    document.body.appendChild(area);
    area.focus();
    area.select();
    document.execCommand('copy');
    area.remove();
  },
  getLocalStorage: function (key) {
    return window.localStorage.getItem(key);
  },
  setLocalStorage: function (key, value) {
    window.localStorage.setItem(key, value);
  },
  removeLocalStorage: function (key) {
    window.localStorage.removeItem(key);
  },
  applyAppearance: function (themeId, language, uiFont, monospaceFont) {
    const themes = {
      'avalonia-default': ['#F0F0F0', '#F7F7F7', '#FFFFFF', '#111111', '#595959', '#0067C0', '#FFFFFF', '#767676', '#D6E9F8', '#CCE4F7', '#111111', '#0F7A2F', '#B42318'],
      'solarized-light': ['#FDF6E3', '#EEE8D5', '#FFFDF5', '#002B36', '#586E75', '#006C75', '#FFFFFF', '#657B83', '#E1DABF', '#C8BE9F', '#002B36', '#268BD2', '#CB4B16'],
      'solarized-dark': ['#002B36', '#073642', '#0B414D', '#FDF6E3', '#93A1A1', '#D9A400', '#002B36', '#839496', '#155766', '#1E6675', '#FDF6E3', '#6EE7A0', '#FF8A80'],
      'gruvbox-light': ['#FBF1C7', '#EBDBB2', '#FFF8D9', '#282828', '#665C54', '#9D0006', '#FFFFFF', '#7C6F64', '#D5C4A1', '#BDAE93', '#282828', '#79740E', '#9D0006'],
      'gruvbox-dark': ['#282828', '#3C3836', '#32302F', '#FBF1C7', '#BDAE93', '#D79921', '#1D2021', '#A89984', '#504945', '#665C54', '#FBF1C7', '#B8BB26', '#FB4934'],
      'ayu-light': ['#FAFAFA', '#F3F4F5', '#FFFFFF', '#242936', '#5C6773', '#006D77', '#FFFFFF', '#6C7680', '#E1E7EA', '#CBD3D8', '#242936', '#86B300', '#D73737'],
      'ayu-mirage': ['#1F2430', '#242936', '#2A3141', '#F3F4F5', '#A6ACB9', '#FFCC66', '#1F2430', '#8A919F', '#343D50', '#475268', '#F3F4F5', '#BAE67E', '#F28779'],
      'ayu-dark': ['#0B0E14', '#11151C', '#151A23', '#E6E1CF', '#9DA5B4', '#FFB454', '#0B0E14', '#7A8494', '#242B38', '#343D4D', '#E6E1CF', '#B8E673', '#F07178']
    };
    const palette = themes[themeId] || themes['avalonia-default'];
    const root = document.documentElement;
    ['--window-bg', '--panel-bg', '--control-bg', '--control-fg', '--muted', '--accent', '--control-bg', '--border', '--hover', '--active', '--frame-neutral', '--frame-accurate', '--frame-inexact']
      .forEach((name, index) => root.style.setProperty(name, palette[index]));
    root.style.setProperty('--status-bg', palette[1]);
    root.style.setProperty('--accent-frame-ok', palette[11]);
    root.style.setProperty('--accent-frame-bad', palette[12]);
    root.style.setProperty('--empty', palette[4]);
    root.style.setProperty('--font', uiFont || 'system-ui');
    root.style.setProperty('--mono', monospaceFont || 'ui-monospace, SFMono-Regular, Menlo, Consolas, monospace');
    root.lang = language || 'en-US';
  },
  isEditableTarget: function () {
    const el = document.activeElement;
    if (!el) {
      return false;
    }
    const tag = (el.tagName || '').toUpperCase();
    if (tag === 'INPUT' || tag === 'TEXTAREA' || tag === 'SELECT') {
      return true;
    }
    return !!el.isContentEditable;
  },
  registerDropZone: function (elementId, dotNetRef) {
    const el = document.getElementById(elementId);
    if (!el || el.dataset.dropRegistered === '1') {
      return;
    }
    el.dataset.dropRegistered = '1';
    const maxBytes = 64 * 1024 * 1024;

    const setOver = (value) => {
      el.classList.toggle('drag-over', value);
      dotNetRef.invokeMethodAsync('OnDragStateChanged', value);
    };

    el.addEventListener('dragenter', (event) => {
      event.preventDefault();
      setOver(true);
    });
    el.addEventListener('dragover', (event) => {
      event.preventDefault();
      if (event.dataTransfer) {
        event.dataTransfer.dropEffect = 'copy';
      }
      setOver(true);
    });
    el.addEventListener('dragleave', (event) => {
      if (!el.contains(event.relatedTarget)) {
        setOver(false);
      }
    });
    el.addEventListener('drop', async (event) => {
      event.preventDefault();
      setOver(false);
      const file = event.dataTransfer && event.dataTransfer.files && event.dataTransfer.files[0];
      if (!file) {
        await dotNetRef.invokeMethodAsync('OnBrowserFileDropped', '', new Uint8Array());
        return;
      }
      if (file.size > maxBytes) {
        await dotNetRef.invokeMethodAsync('OnBrowserFileDropRejected', 'too-large');
        return;
      }
      try {
        const buffer = new Uint8Array(await file.arrayBuffer());
        await dotNetRef.invokeMethodAsync('OnBrowserFileDropped', file.name || 'dropped.bin', buffer);
      } catch (error) {
        await dotNetRef.invokeMethodAsync('OnBrowserFileDropRejected', 'blocked');
      }
    });
  }
};

function encodeText(text, encodingId, emitBom) {
  if (encodingId === 'utf8' || !encodingId) {
    const body = Array.from(new TextEncoder().encode(text));
    return new Uint8Array((emitBom ? [0xEF, 0xBB, 0xBF] : []).concat(body));
  }

  const bytes = [];
  if (encodingId === 'utf16le' || encodingId === 'utf16be') {
    if (emitBom) bytes.push(...(encodingId === 'utf16le' ? [0xFF, 0xFE] : [0xFE, 0xFF]));
    for (let i = 0; i < text.length; i++) {
      const value = text.charCodeAt(i);
      if (encodingId === 'utf16le') bytes.push(value & 0xFF, value >> 8);
      else bytes.push(value >> 8, value & 0xFF);
    }
    return new Uint8Array(bytes);
  }

  if (emitBom) bytes.push(...(encodingId === 'utf32le' ? [0xFF, 0xFE, 0x00, 0x00] : [0x00, 0x00, 0xFE, 0xFF]));
  for (const character of text) {
    const value = character.codePointAt(0);
    if (encodingId === 'utf32le') bytes.push(value & 0xFF, (value >> 8) & 0xFF, (value >> 16) & 0xFF, value >> 24);
    else bytes.push(value >> 24, (value >> 16) & 0xFF, (value >> 8) & 0xFF, value & 0xFF);
  }
  return new Uint8Array(bytes);
}
