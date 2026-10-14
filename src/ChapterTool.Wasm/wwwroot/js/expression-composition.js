const listeners = new WeakMap();

export function attach(element, callback) {
  const onStart = () => callback.invokeMethodAsync('CompositionStarted');
  const onEnd = () => callback.invokeMethodAsync('CompositionEnded');
  const onKey = (event) => {
    if (event.isComposing) return;
    if ((event.ctrlKey || event.metaKey) && event.code === 'Space') {
      event.preventDefault(); event.stopPropagation();
      callback.invokeMethodAsync('EditorKey', 'Complete', element.selectionStart);
    } else if (element.dataset.completionOpen === 'true' && ['Enter', 'Tab', 'Escape', 'ArrowUp', 'ArrowDown'].includes(event.key)) {
      event.preventDefault(); event.stopPropagation();
      callback.invokeMethodAsync('EditorKey', event.key, element.selectionStart);
    }
  };
  const onScroll = () => {
    const highlight = element.parentElement.querySelector('.lua-highlight');
    if (highlight) { highlight.scrollTop = element.scrollTop; highlight.scrollLeft = element.scrollLeft; }
  };
  element.addEventListener('compositionstart', onStart);
  element.addEventListener('compositionend', onEnd);
  element.addEventListener('keydown', onKey);
  element.addEventListener('scroll', onScroll);
  listeners.set(element, [onStart, onEnd, onKey, onScroll]);
  element.dataset.compositionReady = 'true';
}

export function detach(element) {
  const registered = listeners.get(element);
  if (!registered) return;
  element.removeEventListener('compositionstart', registered[0]);
  element.removeEventListener('compositionend', registered[1]);
  element.removeEventListener('keydown', registered[2]);
  element.removeEventListener('scroll', registered[3]);
  listeners.delete(element);
  delete element.dataset.compositionReady;
}

export function caret(element) { return element.selectionStart; }
export function completionState(element, open) { element.dataset.completionOpen = String(open); }
export function select(element, start, length) { element.focus(); element.setSelectionRange(start, start + length); }
export function replace(element, start, length, text) {
  select(element, start, length);
  // insertText preserves the browser's native text undo stack.
  if (!document.execCommand('insertText', false, text)) {
    element.setRangeText(text, start, start + length, 'end');
    element.dispatchEvent(new Event('input', { bubbles: true }));
  }
}
