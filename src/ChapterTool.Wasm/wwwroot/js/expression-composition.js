const listeners = new WeakMap();

export function attach(element, callback) {
  const onStart = () => callback.invokeMethodAsync('CompositionStarted');
  const onEnd = () => callback.invokeMethodAsync('CompositionEnded');
  element.addEventListener('compositionstart', onStart);
  element.addEventListener('compositionend', onEnd);
  listeners.set(element, [onStart, onEnd]);
  element.dataset.compositionReady = 'true';
}

export function detach(element) {
  const registered = listeners.get(element);
  if (!registered) return;
  element.removeEventListener('compositionstart', registered[0]);
  element.removeEventListener('compositionend', registered[1]);
  listeners.delete(element);
  delete element.dataset.compositionReady;
}
