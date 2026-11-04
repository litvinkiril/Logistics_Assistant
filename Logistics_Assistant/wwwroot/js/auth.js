export function createAuth({ main, escape, icon, navigate, onUserChange }) {
  const auth = { user: null, checked: false, returnTo: '#/products', busy: false };

  async function request(path, options = {}) {
    const controller = new AbortController();
    const timer = setTimeout(() => controller.abort(), 15000);
    try {
      const response = await fetch(path.startsWith('/api/') ? path : `/api/auth/${path}`, {
        ...options, signal: controller.signal, credentials: 'same-origin', cache: 'no-store',
        headers: { Accept: 'application/json', ...options.headers }
      });
      const content = response.status === 204 ? '' : await response.text();
      let body;
      try { body = content ? JSON.parse(content) : null; } catch { body = content; }
      if (!response.ok) {
        const message = (typeof body === 'string' && response.status < 500 ? body : null) || body?.message || (body?.errors ? Object.values(body.errors).flat().join(' ') : null);
        const error = new Error(message || (response.status === 400 ? 'Обновите страницу и повторите попытку.' : 'Не удалось выполнить запрос. Попробуйте ещё раз.'));
        error.status = response.status;
        throw error;
      }
      return body;
    } catch (error) {
      if (error.name === 'AbortError' || error instanceof TypeError) throw new Error('Нет соединения с приложением. Повторите попытку.');
      throw error;
    } finally { clearTimeout(timer); }
  }

  async function mutation(path, method, data) {
    // Fetch a token for the current identity, including after login/logout in another tab.
    const { token } = await request('csrf');
    return request(path, { method, headers: { 'Content-Type': 'application/json', 'X-CSRF-TOKEN': token }, body: JSON.stringify(data ?? {}) });
  }
  const post = (path, data) => mutation(path, 'POST', data);

  auth.mutate = async (path, method, data) => {
    if (!path.startsWith('/api/') || !['POST', 'PUT', 'PATCH', 'DELETE'].includes(method)) throw new Error('Некорректный запрос.');
    try { return await mutation(path, method, data); }
    catch (error) { if (error.status === 401) auth.expire(); throw error; }
  };

  auth.check = async () => {
    try { auth.user = await request('me'); }
    catch (error) { if (error.status !== 401) throw error; auth.user = null; }
    auth.checked = true;
    onUserChange(auth.user);
  };

  auth.expire = () => {
    auth.user = null;
    auth.checked = true;
    onUserChange(null);
    if (!/^#\/(login|register)(?:[/?]|$)/.test(location.hash)) auth.returnTo = location.hash || '#/products';
    navigate('#/login');
  };

  auth.render = mode => {
    const register = mode === 'register';
    document.title = `${register ? 'Регистрация' : 'Вход'} — Логистический помощник`;
    main.innerHTML = `<section class="panel auth-panel" aria-labelledby="auth-title"><span class="auth-logo">${icon('truck')}</span><div class="eyebrow">ЛОГИСТИЧЕСКИЙ ПОМОЩНИК</div><h1 id="auth-title">${register ? 'Создать аккаунт' : 'С возвращением'}</h1><p class="auth-intro">${register ? 'Зарегистрируйтесь и сразу приступайте к работе.' : 'Войдите, чтобы работать с товарами, складами и рейсами.'}</p><form id="auth-form" data-mode="${mode}"><label class="field-label" for="auth-email">Email</label><input class="auth-input" id="auth-email" name="email" type="email" autocomplete="username" maxlength="254" placeholder="you@example.ru" required><label class="field-label" for="auth-password">Пароль</label><input class="auth-input" id="auth-password" name="password" type="password" autocomplete="${register ? 'new-password' : 'current-password'}" ${register ? 'minlength="8"' : ''} maxlength="128" required aria-describedby="auth-hint auth-error"><p class="auth-hint" id="auth-hint">${register ? 'От 8 символов. Подтверждение почты не требуется.' : 'Используйте email и пароль вашего аккаунта.'}</p>${register ? '' : '<label class="auth-remember"><input type="checkbox" name="rememberMe">Запомнить меня</label>'}<p class="field-error" id="auth-error" role="alert"></p><button class="button primary full" type="submit">${register ? 'Создать аккаунт' : 'Войти'} ${icon('arrow')}</button></form><p class="auth-switch">${register ? 'Уже есть аккаунт? <a href="#/login">Войти</a>' : 'Нет аккаунта? <a href="#/register">Зарегистрироваться</a>'}</p></section>`;
  };

  auth.submit = async form => {
    if (auth.busy) return;
    const error = form.querySelector('#auth-error');
    const button = form.querySelector('button[type="submit"]');
    const email = form.elements.email.value.trim();
    const password = form.elements.password.value;
    const register = form.dataset.mode === 'register';
    error.textContent = '';
    if (!email || !password || (register && password.length < 8)) {
      error.textContent = register ? 'Введите email и пароль от 8 символов.' : 'Введите email и пароль.';
      return;
    }
    auth.busy = true;
    button.disabled = true;
    button.textContent = register ? 'Создаём аккаунт…' : 'Входим…';
    try {
      auth.user = await post(register ? 'register' : 'login', { email, password, rememberMe: Boolean(form.elements.rememberMe?.checked) });
      auth.checked = true;
      onUserChange(auth.user);
      form.reset();
      if (form.isConnected) navigate(auth.returnTo);
    } catch (failure) {
      if (form.isConnected) error.textContent = failure.message;
    } finally {
      auth.busy = false;
      button.disabled = false;
      button.innerHTML = `${register ? 'Создать аккаунт' : 'Войти'} ${icon('arrow')}`;
    }
  };

  auth.logout = async () => {
    if (auth.busy) return;
    auth.busy = true;
    try {
      await post('logout');
      auth.returnTo = '#/products';
      auth.expire();
    } catch (error) {
      if (error.status === 401) auth.expire();
      else throw error;
    } finally { auth.busy = false; }
  };
  return auth;
}
