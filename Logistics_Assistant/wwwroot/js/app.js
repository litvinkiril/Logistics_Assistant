import { deliveryProducts, cargoState } from './delivery-data.js';
import { createAuth } from './auth.js';

const $ = (selector, root = document) => root.querySelector(selector);
const main = $('#main');
const historyKey = 'logistics-assistant:searches:v1';
const modes = { name: { label: 'Название', placeholder: 'Например, стеллаж', hint: 'Найдём все совпадения по части названия.', icon: 'search' }, id: { label: 'ID', placeholder: 'Введите ID товара', hint: 'Укажите положительный числовой ID товара.', icon: 'hash' }, url: { label: 'URL', placeholder: 'https://…', hint: 'Вставьте полную ссылку на страницу товара.', icon: 'link' } };
let navigation, view = {};
const assigningDeliveries = new Set();
let savedSearchRoute = '#/products', savedPointRoute = '#/points', savedDeliveryRoute = '#/deliveries';
const escape = value => String(value ?? '').replace(/[&<>"']/g, char => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' })[char]);
const icon = name => `<svg class="icon" aria-hidden="true"><use href="/assets/icons.svg#${name}"/></svg>`;
const number = value => new Intl.NumberFormat('ru-RU').format(value);
const plural = (n, forms) => forms[n % 100 > 10 && n % 100 < 20 ? 2 : n % 10 === 1 ? 0 : n % 10 > 1 && n % 10 < 5 ? 1 : 2];
const pointType = p => p.type === 'pvz' ? 'ПВЗ' : p.type === 'warehouse' ? 'Склад' : p.type || 'Точка';
const pointIcon = p => p.type === 'pvz' ? 'store' : 'warehouse';
const typeBadge = p => `<span class="badge ${p.type === 'pvz' ? 'pvz' : ''}">${escape(pointType(p))}</span>`;
const activeBadge = p => `<span class="badge ${p.isActive ? 'active' : 'inactive'}"><i class="status-dot"></i>${p.isActive ? 'Активен' : 'Неактивен'}</span>`;
const pointAvatar = p => `<span class="point-avatar ${p.type === 'pvz' ? 'pvz' : ''}">${icon(pointIcon(p))}</span>`;
const itemArt = () => `<span class="item-art">${icon('box')}</span>`;
const art = name => `<div class="empty-art">${icon(name)}</div>`;
const notice = text => `<div class="notice">${icon('info')}<span>${text}</span></div>`;
const loading = (label = 'Загружаем данные') => `<div class="loading-state" role="status" aria-label="${label}"><div class="skeleton"></div><div class="skeleton"></div><div class="skeleton"></div><p class="loading-label">${label}…</p></div>`;
const empty = (name, title, text, extra = '') => `<div class="empty-state">${art(name)}<h2>${title}</h2><p>${text}</p>${extra}</div>`;
const heading = (title, text, refresh = true) => `<div class="page-heading"><div><div class="eyebrow">ВАШ ЛОГИСТИЧЕСКИЙ ПОМОЩНИК</div><h1>${title}</h1><p>${text}</p></div>${refresh ? `<button class="button" data-action="refresh" aria-label="Обновить данные">${icon('refresh')}Обновить</button>` : ''}</div>`;
const auth = createAuth({ main, escape, icon, navigate, onUserChange: renderAccount });
function renderAccount(user) {
  setConnection('online', 'Подключено к приложению');
  $('#account-controls').innerHTML = user
    ? `<span class="account-email" title="${escape(user.email)}">${escape(user.email)}</span><button class="button" type="button" data-action="logout">Выйти</button>`
    : '<a class="button" href="#/login">Войти</a>';
  $('#account-error').textContent = '';
}
function setConnection(state, label) {
  $('#connection').className = `connection ${state}`;
  $('#connection').innerHTML = `<i></i><span>${label}</span>`;
}
class ApiError extends Error {
  constructor(status) { super(status === 404 ? 'Запись не найдена.' : status === 400 ? 'Проверьте параметры запроса.' : status === 401 || status === 403 ? 'Доступ к данным ограничен.' : 'Не удалось загрузить данные. Проверьте, что приложение и база данных запущены.'); this.status = status; }
}
async function api(path, signal) {
  const controller = new AbortController(), abort = () => controller.abort();
  signal?.addEventListener('abort', abort, { once: true });
  if (signal?.aborted) controller.abort();
  const timer = setTimeout(abort, 15000);
  try {
    const response = await fetch(path, { signal: controller.signal, headers: { Accept: 'application/json' }, cache: 'no-store' });
    if (response.status === 401 && !signal?.aborted) auth.expire();
    if (!response.ok) { if (!signal?.aborted) setConnection(response.status >= 500 ? 'offline' : 'online', response.status >= 500 ? 'Ошибка загрузки' : 'Подключено к приложению'); throw new ApiError(response.status); }
    // Preserve Int64 IDs using the source token supported by modern browsers.
    const data = JSON.parse(await response.text(), (key, value, context) => {
      if ((key !== 'id' && !key.endsWith('Id')) || typeof value !== 'number') return value;
      if (!context?.source && !Number.isSafeInteger(value)) throw new ApiError(0);
      return context?.source ?? String(value);
    });
    if (!signal?.aborted) setConnection('online', 'Подключено к приложению');
    return data;
  } catch (error) {
    if (signal?.aborted) throw new DOMException('Навигация отменена', 'AbortError');
    if (error instanceof ApiError) throw error;
    setConnection('offline', 'Нет соединения'); throw new ApiError(0);
  } finally { clearTimeout(timer); signal?.removeEventListener('abort', abort); }
}
function errorMarkup(error, action = 'refresh') {
  return `<div class="empty-state error-state" role="alert">${art(error.status === 404 ? 'search' : 'alert')}<h2>${error.status === 404 ? 'Ничего не найдено' : 'Данные пока недоступны'}</h2><p>${escape(error.message)}</p><button class="button" data-action="${action}">${icon('refresh')}Попробовать ещё раз</button></div>`;
}
function readHistory() {
  try { const entries = JSON.parse(localStorage.getItem(historyKey) || '[]'); return Array.isArray(entries) ? entries.filter(x => x && Object.hasOwn(modes, x.mode) && typeof x.query === 'string').slice(0, 6) : []; } catch { return []; }
}
function remember(mode, query) {
  try { localStorage.setItem(historyKey, JSON.stringify([{ mode, query }, ...readHistory().filter(x => x.mode !== mode || x.query !== query)].slice(0, 6))); } catch { /* Storage may be disabled. */ }
  renderHistory();
}
function renderHistory() {
  if (!$('#search-history')) return;
  const entries = readHistory();
  $('#search-history').innerHTML = `<div class="section-line"><h2>Недавние запросы</h2>${entries.length ? '<button class="text-button" data-action="clear-history">Очистить все</button>' : ''}</div>${entries.length ? `<ul class="history-list">${entries.map((x, i) => `<li><button data-action="history" data-index="${i}">${icon('clock')}<span class="history-text">${escape(x.query)}<span class="history-label">Поиск по ${x.mode === 'name' ? 'названию' : x.mode === 'id' ? 'ID' : 'ссылке'}</span></span>${icon('arrow')}</button></li>`).join('')}</ul>` : '<p class="history-empty">Здесь появятся ваши запросы.<br>К любому из них можно вернуться в один клик.</p>'}`;
}
function segmented(mode, action = 'search-mode') {
  return `<div class="segmented" role="group" aria-label="Способ поиска">${Object.entries(modes).map(([key, value]) => `<button type="button" data-action="${action}" data-mode="${key}" class="${mode === key ? 'active' : ''}" aria-pressed="${mode === key}">${value.label}</button>`).join('')}</div>`;
}
function searchInput(value, mode, id = 'product-query') {
  return `<div class="search-input">${icon(modes[mode].icon)}<input id="${id}" name="query" value="${escape(value)}" placeholder="${modes[mode].placeholder}" aria-label="${mode === 'name' ? 'Название товара' : mode === 'id' ? 'ID товара' : 'Ссылка на товар'}" ${mode === 'id' ? 'inputmode="numeric"' : mode === 'url' ? 'inputmode="url"' : ''} maxlength="2048" autocomplete="off" aria-describedby="search-error"><button type="button" class="icon-button" data-action="clear-input" aria-label="Очистить поле">${icon('close')}</button></div>`;
}
function validate(mode, query) {
  if (!query.trim()) return 'Введите ' + (mode === 'name' ? 'название товара.' : mode === 'id' ? 'ID товара.' : 'ссылку на товар.');
  if (mode === 'id' && (!/^[1-9]\d*$/.test(query) || query.length > 19 || BigInt(query) > 9223372036854775807n)) return 'ID должен быть положительным целым числом до 9223372036854775807.';
  if (mode === 'url') { try { if (!['http:', 'https:'].includes(new URL(query).protocol)) throw new Error(); } catch { return 'Введите полную ссылку, начинающуюся с http:// или https://.'; } }
  return '';
}
function showValidation(form, text) {
  if ($('#search-error', form)) $('#search-error', form).textContent = text;
  const input = $('input[name="query"]', form);
  if (text) { input.setAttribute('aria-invalid', 'true'); input.focus(); } else input.removeAttribute('aria-invalid');
}
function searchRoute(mode, query, base = '/products') { return `#${base}?${new URLSearchParams({ mode, q: query })}`; }
async function searchProducts(mode, query, offset, signal) {
  const data = await api(mode === 'id' ? `/api/products/${query}` : mode === 'url' ? `/api/products/by-url?${new URLSearchParams({ url: query })}` : `/api/products/search?${new URLSearchParams({ name: query, offset })}`, signal);
  if (!Array.isArray(data)) throw new ApiError(0);
  return data;
}
function productCard(product, stock, pointId = '') {
  let line = '<span class="stock-line muted">Загружаем остатки…</span>';
  if (stock === null) line = '<span class="stock-line muted">Остатки недоступны</span>';
  else if (stock) {
    const total = (stock.warehouses || []).reduce((sum, x) => sum + x.quantity, 0), count = (stock.warehouses || []).filter(x => x.quantity > 0).length;
    line = pointId ? `<span class="stock-quantity">${number(total)} шт.</span>` : `<span class="stock-line ${total ? '' : 'muted'}">${total ? `${number(total)} шт. · в ${count} ${plural(count, ['точке', 'точках', 'точках'])}` : 'Нет в наличии'}</span>`;
  }
  return `<a class="product-card" href="#/products/${encodeURIComponent(product.id)}${pointId ? `?fromPoint=${encodeURIComponent(pointId)}` : ''}">${itemArt()}<span class="item-main"><span class="item-name">${escape(product.name)}</span><span class="item-meta" style="display:block">ID ${escape(product.id)}</span>${pointId ? '' : line}</span>${pointId ? line : ''}${icon('arrow')}</a>`;
}
function renderProductResults() {
  if (!$('#product-results')) return;
  const n = view.products.length;
  $('#product-results').innerHTML = `<div class="result-header"><h2>Результаты поиска</h2><span class="count-badge">${n}${view.hasMore ? '+' : ''} ${plural(n, ['товар', 'товара', 'товаров'])}</span></div>${n ? `<div class="product-list">${view.products.map(p => productCard(p, view.stocks.get(String(p.id)))).join('')}</div>${view.hasMore ? `<div class="list-bottom"><button class="button full" data-action="load-more" ${view.loadingMore ? 'disabled' : ''}>${view.loadingMore ? 'Загружаем…' : 'Показать ещё 10'}</button></div>` : ''}<p class="result-caption">Нажмите на товар, чтобы посмотреть наличие по всем точкам.</p>` : empty('search', 'Товары не найдены', 'Попробуйте другое название, проверьте ID или ссылку на товар.')}`;
}
async function loadProducts(signal, append = false) {
  if (append && view.loadingMore) return;
  const current = view;
  current.loadingMore = append;
  if (append) renderProductResults(); else $('#product-results').innerHTML = `<div class="result-header"><h2>Результаты поиска</h2></div>${loading('Ищем товары')}`;
  try {
    const products = await searchProducts(current.searchMode, current.query, append ? current.offset : 0, signal);
    if (signal.aborted) return;
    current.offset = (append ? current.offset : 0) + products.length;
    current.products = append ? [...current.products, ...products] : products;
    current.hasMore = current.searchMode === 'name' && products.length === 10;
    current.loadingMore = false; renderProductResults();
    const stocks = await Promise.allSettled(products.map(p => api(`/api/products/${p.id}/stock`, signal)));
    if (signal.aborted) return;
    stocks.forEach((r, i) => current.stocks.set(String(products[i].id), r.status === 'fulfilled' ? r.value : null));
    renderProductResults();
  } catch (error) {
    if (signal.aborted) return;
    current.loadingMore = false;
    if (error.status === 404) { current.products = []; current.hasMore = false; renderProductResults(); }
    else if (append) { renderProductResults(); $('.list-bottom').insertAdjacentHTML('beforeend', `<p class="field-error" role="alert">${escape(error.message)}</p>`); }
    else $('#product-results').innerHTML = errorMarkup(error);
  }
}
function productSearchForm(mode, query) {
  return `${segmented(mode)}<label class="field-label" for="product-query">${mode === 'name' ? 'Название товара' : mode === 'id' ? 'ID товара' : 'Ссылка на товар'}</label>${searchInput(query, mode)}<p class="field-error" id="search-error" role="alert"></p><button class="button primary full" type="submit">${icon('search')}Найти товар</button><p class="search-hint">${modes[mode].hint}</p>`;
}
function renderProducts(params, signal) {
  const mode = Object.hasOwn(modes, params.get('mode')) ? params.get('mode') : 'name', query = params.get('q') || '';
  view = { section: 'products', mode, searchMode: mode, query, products: [], stocks: new Map(), offset: 0, hasMore: false };
  main.innerHTML = `${heading('Товары', 'Найдите товар и проверьте его наличие на складах и в ПВЗ.')}<div class="search-layout"><div class="search-column"><section class="panel search-panel" aria-labelledby="search-title"><div class="panel-title">${icon('search')}<h2 id="search-title">Поиск товара</h2></div><form id="product-search" novalidate>${productSearchForm(mode, query)}</form></section><section class="panel history-panel" id="search-history" aria-label="История поиска"></section>${notice('По названию — список совпадений.<br>По ID или ссылке — конкретный товар.')}</div><section class="panel result-panel" id="product-results" aria-label="Результаты поиска" aria-live="polite"><div class="result-header"><h2>Результаты поиска</h2><span class="count-badge">Начните с поиска</span></div>${empty('box', 'Нужный товар — в пару кликов', 'Введите название, ID или ссылку. Мы найдём товар и покажем, где он есть в наличии.', '<div class="steps"><span><b>1</b> Найдите товар</span><span><b>2</b> Выберите точку</span><span><b>3</b> Проверьте остатки</span></div>')}</section></div>`;
  renderHistory();
  if (query) { const error = validate(mode, query); if (error) showValidation($('#product-search'), error); else { remember(mode, query); loadProducts(signal); } }
}
function safeUrl(value) { try { const url = new URL(value); return ['http:', 'https:'].includes(url.protocol) ? url.href : ''; } catch { return ''; } }
function stockRow(item) {
  const p = item.warehouse;
  return `<a class="stock-row" href="#/points/${encodeURIComponent(p.id)}?${new URLSearchParams({ mode: 'id', q: view.productId })}">${pointAvatar(p)}<span class="item-main"><span class="item-name">${escape(p.name)}</span><span style="display:block">${typeBadge(p)}${!p.isActive ? ' <span class="badge inactive">Неактивен</span>' : ''}</span></span><span class="stock-quantity">${number(item.quantity)} шт.</span>${icon('arrow')}</a>`;
}
async function renderProduct(id, params, signal) {
  view = { section: 'products', productId: id };
  const fromPoint = params.get('fromPoint');
  const back = fromPoint && /^[1-9]\d*$/.test(fromPoint) ? `#/points/${fromPoint}` : savedSearchRoute;
  const backLink = `<a class="back-link" href="${escape(back)}">${icon('back')}${fromPoint ? 'К логистической точке' : 'К поиску товаров'}</a>`;
  main.innerHTML = `${backLink}<section class="panel">${loading('Загружаем товар')}</section>`;
  try {
    const data = await api(`/api/products/${id}/stock`, signal);
    if (signal.aborted) return;
    const p = data.product, stocks = data.warehouses || [];
    const total = stocks.reduce((sum, x) => sum + x.quantity, 0), count = stocks.filter(x => x.quantity > 0).length, url = safeUrl(p.url);
    $('#breadcrumb').textContent = p.name; document.title = `${p.name} — Логистический помощник`;
    main.innerHTML = `${backLink}<div class="detail-layout"><article class="panel detail-panel"><div class="product-heading">${itemArt()}<div><h1>${escape(p.name)}</h1><div class="item-meta">ID ${escape(p.id)}</div>${url ? `<a class="product-link" href="${escape(url)}" target="_blank" rel="noopener noreferrer">Открыть страницу товара ${icon('external')}</a>` : ''}</div></div><div class="availability">${icon('layers')}<div><strong>${number(total)} шт.</strong><p>${count ? `Доступно в ${count} ${plural(count, ['точке', 'точках', 'точках'])}` : 'Нет в наличии на логистических точках'}</p></div></div><h2 class="detail-section-title">Наличие по точкам</h2>${stocks.length ? `<div class="stock-list">${stocks.map(stockRow).join('')}</div>` : '<div class="inline-empty">Для этого товара пока нет данных об остатках.</div>'}</article><aside class="detail-aside"><section class="panel aside-card"><h2>${icon('box')}Информация о товаре</h2><dl class="facts"><div><dt>ID товара</dt><dd>${escape(p.id)}</dd></div><div><dt>Всего в наличии</dt><dd>${number(total)} шт.</dd></div><div><dt>Точек с остатками</dt><dd>${count}</dd></div><div><dt>Страница товара</dt><dd>${url ? `<a href="${escape(url)}" target="_blank" rel="noopener noreferrer">Открыть ↗</a>` : 'Не указана'}</dd></div></dl></section><section class="panel aside-card"><h2>${icon('info')}Проверка наличия</h2><p>Выберите точку в списке, чтобы посмотреть адрес, статус и проверить наличие товара в ней.</p><p>Остатки загружены при открытии карточки.</p><button class="button quiet" data-action="refresh">${icon('refresh')}Обновить остатки</button></section></aside></div>`;
  } catch (error) { if (!signal.aborted) main.innerHTML = `${backLink}<section class="panel">${errorMarkup(error)}</section>`; }
}
function pointCard(p) {
  return `<a class="point-card" href="#/points/${encodeURIComponent(p.id)}"><div class="point-card-top">${pointAvatar(p)}${typeBadge(p)}</div><h3>${escape(p.name)}</h3><p class="point-address">${escape([p.city, p.address].filter(Boolean).join(', ') || 'Адрес не указан')}</p><div class="point-card-footer">${activeBadge(p)}${icon('arrow')}</div></a>`;
}
function renderPointsList() {
  const query = view.pointQuery.toLocaleLowerCase('ru');
  const points = view.allPoints.filter(p => (!view.type || p.type === view.type) && (!query || [p.name, p.city, p.address].some(x => String(x || '').toLocaleLowerCase('ru').includes(query))));
  $('#points-content').innerHTML = `<div class="points-result-meta"><span>${points.length} ${plural(points.length, ['точка', 'точки', 'точек'])}${query || view.type ? ' по вашему запросу' : ' в справочнике'}</span><span class="updated-label">${icon('check')}Данные загружены</span></div>${points.length ? `<div class="points-grid">${points.map(pointCard).join('')}</div>` : empty('warehouse', 'Точки не найдены', 'Попробуйте другой адрес, город или тип точки.', '<button class="button" data-action="reset-points">Сбросить фильтры</button>')}`;
  document.querySelectorAll('[data-action="point-filter"]').forEach(button => { const active = button.dataset.type === view.type; button.classList.toggle('active', active); button.setAttribute('aria-pressed', active); });
}
async function renderPoints(params, signal) {
  view = { section: 'points', allPoints: [], type: ['warehouse', 'pvz'].includes(params.get('type')) ? params.get('type') : '', pointQuery: params.get('q') || '' };
  main.innerHTML = `${heading('Склады и ПВЗ', 'Все логистические точки, их адреса и наличие товаров.')}<div class="summary-row" id="points-summary">${['warehouse', 'store', 'check'].map(name => `<div class="summary-item">${icon(name)}<div><strong>—</strong><small>${name === 'warehouse' ? 'Склады' : name === 'store' ? 'Пункты выдачи' : 'Активные точки'}</small></div></div>`).join('')}</div><section class="panel"><div class="point-toolbar"><form id="point-search" class="toolbar-search"><div class="search-input">${icon('search')}<input name="query" aria-label="Поиск точки по названию, городу или адресу" placeholder="Название, город или адрес" value="${escape(view.pointQuery)}" maxlength="512" autocomplete="off"><button type="button" class="icon-button" data-action="clear-point-query" aria-label="Очистить поиск точек">${icon('close')}</button></div><button class="button" type="submit">Найти</button></form><div class="segmented" role="group" aria-label="Тип точки">${[['', 'Все'], ['warehouse', 'Склады'], ['pvz', 'ПВЗ']].map(([type, label]) => `<button data-action="point-filter" data-type="${type}" type="button" class="${type === view.type ? 'active' : ''}" aria-pressed="${type === view.type}">${label}</button>`).join('')}</div></div><div id="points-content" aria-live="polite">${loading('Загружаем точки')}</div></section>`;
  try {
    const points = await api('/api/logistics-points', signal);
    if (signal.aborted) return;
    if (!Array.isArray(points)) throw new ApiError(0);
    view.allPoints = points;
    $('#points-summary').innerHTML = [[points.filter(p => p.type === 'warehouse').length, 'Склады', 'warehouse'], [points.filter(p => p.type === 'pvz').length, 'Пункты выдачи', 'store'], [points.filter(p => p.isActive).length, 'Активные точки', 'check']].map(([value, label, name]) => `<div class="summary-item">${icon(name)}<div><strong>${number(value)}</strong><small>${label}</small></div></div>`).join('');
    renderPointsList();
  } catch (error) { if (!signal.aborted) $('#points-content').innerHTML = errorMarkup(error); }
}
function pointSearchForm(mode, query) {
  return `<form id="point-product-search" novalidate>${segmented(mode, 'point-product-mode')}<div class="toolbar-search">${searchInput(query, mode, 'point-product-query')}<button class="button primary" type="submit">${icon('search')}Найти</button></div><p class="field-error" id="search-error" role="alert"></p></form>`;
}
async function loadPointProducts(signal, append = false) {
  const current = view;
  if (current.loadingMore) return;
  const error = validate(current.searchMode, current.query);
  showValidation($('#point-product-search'), error);
  if (error) return;
  const target = $('#point-products');
  current.loadingMore = true;
  target.innerHTML = `<div class="loading-inline" role="status"><span class="spinner"></span>Проверяем наличие в этой точке…</div>`;
  try {
    const products = await searchProducts(current.searchMode, current.query, append ? current.offset : 0, signal);
    const stocks = await Promise.allSettled(products.map(p => api(`/api/products/${p.id}/stock/${current.pointId}`, signal)));
    if (signal.aborted) return;
    current.offset = (append ? current.offset : 0) + products.length;
    const entries = products.map((product, i) => ({ product, stock: stocks[i].status === 'fulfilled' ? stocks[i].value : null }));
    current.entries = append ? [...current.entries, ...entries] : entries;
    const hasMore = current.searchMode === 'name' && products.length === 10;
    target.innerHTML = current.entries.length ? `<div class="section-line"><h2 class="detail-section-title">Найденные товары</h2><span class="count-badge">${current.entries.length}${hasMore ? '+' : ''}</span></div><div class="product-list">${current.entries.map(x => productCard(x.product, x.stock, current.pointId)).join('')}</div>${hasMore ? '<div style="margin-top:14px"><button class="button full" data-action="point-load-more">Показать ещё 10</button></div>' : ''}<p class="search-hint">Показано наличие найденных товаров в выбранной точке, включая нулевые остатки.</p>` : '<div class="inline-empty">Товары не найдены. Попробуйте другое название, ID или ссылку.</div>';
  } catch (error) { if (!signal.aborted) target.innerHTML = error.status === 404 ? '<div class="inline-empty">Товар не найден. Проверьте ID.</div>' : errorMarkup(error, 'retry-point-products'); }
  finally { current.loadingMore = false; }
}
async function renderPoint(id, params, signal) {
  const mode = Object.hasOwn(modes, params.get('mode')) ? params.get('mode') : 'name';
  view = { section: 'points', pointId: id, mode, searchMode: mode, query: params.get('q') || '', offset: 0, entries: [] };
  const back = `<a class="back-link" href="${escape(savedPointRoute)}">${icon('back')}Все склады и ПВЗ</a>`;
  main.innerHTML = `${back}<section class="panel">${loading('Загружаем информацию о точке')}</section>`;
  try {
    const p = await api(`/api/logistics-points/${id}`, signal);
    if (signal.aborted) return;
    $('#breadcrumb').textContent = p.name; document.title = `${p.name} — Логистический помощник`;
    const lat = Number(p.latitude), lon = Number(p.longitude);
    const hasCoordinates = p.latitude != null && p.longitude != null && Number.isFinite(lat) && Number.isFinite(lon) && Math.abs(lat) <= 90 && Math.abs(lon) <= 180;
    const mapUrl = `https://yandex.ru/maps/?${new URLSearchParams({ ll: `${lon},${lat}`, z: '16', pt: `${lon},${lat},pm2blm` })}`;
    main.innerHTML = `${back}<div class="detail-layout"><article class="panel detail-panel"><div class="detail-title-row">${pointAvatar(p)}<div><h1>${escape(p.name)}</h1>${typeBadge(p)}${activeBadge(p)}</div></div><div class="address-block"><div class="address-label">${icon('pin')}АДРЕС ТОЧКИ</div><p>${escape([p.city, p.address].filter(Boolean).join(', ') || 'Адрес не указан')}</p></div>${hasCoordinates ? `<a href="${escape(mapUrl)}" target="_blank" rel="noopener noreferrer" class="map-card" aria-label="Открыть расположение точки в Яндекс Картах"><span class="map-pin">${icon('pin')}</span><span class="map-open">Расположение на карте ${icon('external')}</span></a><div class="coordinates">${icon('pin')}${lat.toFixed(6)}, ${lon.toFixed(6)}<span>· Схема условная</span></div>` : ''}<section class="warehouse-search" aria-labelledby="availability-title"><h2 id="availability-title">Проверить наличие товара</h2><p>Найдите нужный товар — покажем его остаток в этой точке.</p>${pointSearchForm(view.mode, view.query)}<div class="point-products" id="point-products" aria-live="polite">${notice('Введите название, ID или ссылку на товар для проверки наличия.')}</div></section></article><aside class="detail-aside"><section class="panel aside-card"><h2>${icon(pointIcon(p))}Информация о точке</h2><dl class="facts"><div><dt>ID точки</dt><dd>${escape(p.id)}</dd></div><div><dt>Тип</dt><dd>${escape(pointType(p))}</dd></div><div><dt>Город</dt><dd>${escape(p.city || 'Не указан')}</dd></div><div><dt>Статус</dt><dd>${p.isActive ? 'Активен' : 'Неактивен'}</dd></div></dl>${hasCoordinates ? `<a href="${escape(mapUrl)}" class="button full" style="margin-top:14px" target="_blank" rel="noopener noreferrer">${icon('pin')}Открыть на карте ${icon('external')}</a>` : ''}</section><section class="panel aside-card"><h2>${icon('info')}Наличие в выбранной точке</h2><p>Поиск проверяет остатки конкретных товаров. Полный перечень товаров склада пока недоступен в приложении.</p><a class="button quiet" href="#/products">Перейти к поиску товаров ${icon('arrow')}</a></section></aside></div>`;
    if (view.query) loadPointProducts(signal);
  } catch (error) { if (!signal.aborted) main.innerHTML = `${back}<section class="panel">${errorMarkup(error)}</section>`; }
}
function deliveryStatus(status) {
  const labels = { new: 'Новый', planned: 'Запланирован', assigned: 'Назначен', loading: 'Погрузка', in_transit: 'В пути', completed: 'Завершён', delivered: 'Доставлен', cancelled: 'Отменён' };
  return `<span class="badge">${escape(labels[status] || 'Статус не указан')}</span>`;
}
function deliveryDate(value) {
  const date = new Date(value);
  return value && !Number.isNaN(date.getTime()) ? new Intl.DateTimeFormat('ru-RU', { dateStyle: 'medium', timeStyle: 'short' }).format(date) : 'Не указана';
}
function deliveryOverview(order) {
  if (!order) return '';
  return `<section class="panel delivery-overview"><div id="delivery-feedback" role="status"></div><div class="delivery-vehicle"><span class="point-avatar">${icon('truck')}</span><div><strong>${escape(order.carPlate || 'Машина не указана')}</strong><small>Создан ${escape(deliveryDate(order.createdAt))}</small></div>${deliveryStatus(order.status)}</div>${order.contactPhone ? `<p class="delivery-contact">Контакт: ${escape(order.contactPhone)}</p>` : ''}<div id="delivery-assignment" class="delivery-assignment">${deliveryAssignment(order)}</div></section>`;
}
function deliveryAssignment(order) {
  if (order.responsibleUserId) {
    const mine = String(order.responsibleUserId) === String(auth.user?.id);
    return `<span class="assignment-status ${mine ? 'mine' : ''}" role="status">${icon(mine ? 'check' : 'info')}${mine ? 'Вы ответственный за рейс' : 'Ответственный уже назначен'}</span>${mine ? '<a class="button quiet assignment-mine-link" href="#/deliveries?tab=mine">Мои рейсы</a>' : ''}`;
  }
  const pending = assigningDeliveries.has(String(order.id));
  return `<button class="button primary assign-delivery" type="button" data-action="assign-delivery" data-delivery-id="${escape(order.id)}" ${pending ? 'disabled aria-busy="true"' : ''}>${icon('check')}${pending ? 'Назначаем…' : 'Взять рейс на себя'}</button>`;
}
function deliveryFeedback(message, error = false) {
  const feedback = $('#delivery-feedback');
  if (!feedback) return;
  feedback.setAttribute('role', error ? 'alert' : 'status');
  feedback.className = `delivery-feedback ${error ? 'is-error' : ''}`;
  feedback.textContent = message;
}
async function assignDelivery(id) {
  if (validate('id', id) || assigningDeliveries.has(id)) return;
  const current = view;
  const signal = navigation.signal;
  assigningDeliveries.add(id);
  deliveryFeedback('');
  document.querySelectorAll('[data-action="assign-delivery"]').forEach(button => {
    if (button.dataset.deliveryId === id) { button.disabled = true; button.setAttribute('aria-busy', 'true'); button.textContent = 'Назначаем…'; }
  });
  try {
    await auth.mutate(`/api/delivery-orders/${encodeURIComponent(id)}/assign`, 'PATCH');
    if (view !== current || signal.aborted) return;
    if (current.deliveryId && current.order) {
      current.order.responsibleUserId = auth.user.id;
      $('#delivery-assignment').innerHTML = deliveryAssignment(current.order);
      deliveryFeedback('Рейс назначен на вас.');
    } else navigate(`#/deliveries/${id}`);
  } catch (error) {
    if (view !== current || signal.aborted || error.status === 401) return;
    if (error.status === 409 || error.status === 404) {
      if (!current.deliveryId) {
        await loadDeliveries(signal);
      } else if (error.status === 409) {
        try {
          const data = await api(`/api/delivery-orders/${encodeURIComponent(id)}`, signal);
          if (view !== current || signal.aborted) return;
          current.order = data.order;
          if (current.order) $('#delivery-assignment').innerHTML = deliveryAssignment(current.order);
        } catch { /* Keep the server's assignment error if refreshing fails. */ }
      } else { route(); return; }
      if (view !== current || signal.aborted) return;
    }
    deliveryFeedback(error.status === 409 ? 'У рейса уже есть ответственный. Данные обновлены.' : error.message, true);
  } finally {
    assigningDeliveries.delete(id);
    if (view === current && !signal.aborted) document.querySelectorAll('[data-action="assign-delivery"]').forEach(button => {
      if (button.dataset.deliveryId === id) { button.disabled = false; button.removeAttribute('aria-busy'); button.innerHTML = `${icon('check')}Взять рейс на себя`; }
    });
  }
}
function deliveryLookup() {
  return `<section class="panel delivery-lookup"><div><h2>Открыть рейс</h2><p>Введите номер, чтобы проверить план и груз в машине.</p></div><form id="delivery-lookup" novalidate><label class="sr-only" for="delivery-id">Номер рейса</label><div class="delivery-lookup-fields"><div class="search-input">${icon('hash')}<input id="delivery-id" name="query" inputmode="numeric" placeholder="Номер рейса" maxlength="19" aria-describedby="search-error" autocomplete="off"></div><button class="button primary" type="submit">Открыть ${icon('arrow')}</button></div><p class="field-error" id="search-error" role="alert"></p></form></section>`;
}
function renderDeliveryCards() {
  const orders = view.orders;
  const mine = view.deliveryTab === 'mine';
  const emptyState = mine
    ? empty('truck', 'У вас пока нет рейсов', 'Выберите свободный рейс и возьмите его на себя.', '<button class="button primary" type="button" data-action="delivery-tab" data-tab="unassigned">Найти свободный рейс</button>')
    : empty('truck', 'Свободных рейсов пока нет', 'Ваши назначенные рейсы доступны во вкладке «Мои рейсы».');
  $('#delivery-list').innerHTML = `<div class="result-header"><h2>${mine ? 'Мои рейсы' : 'Рейсы без ответственного'}</h2><span class="count-badge">${orders.length}${view.hasMore ? '+' : ''}</span></div>${orders.length ? `<div class="delivery-grid">${orders.map(order => `<article class="delivery-card"><a class="delivery-card-main" href="#/deliveries/${encodeURIComponent(order.id)}${mine ? '?tab=mine' : ''}"><div class="delivery-card-heading"><span class="point-avatar">${icon('truck')}</span>${deliveryStatus(order.status)}</div><h3>Рейс №${escape(order.id)}</h3><p>${escape(order.carPlate || 'Машина не указана')}</p><div class="delivery-card-footer"><span>${escape(deliveryDate(order.createdAt))}</span><span>Груз ${icon('arrow')}</span></div></a><div class="delivery-card-actions">${mine ? `<span class="assignment-status mine">${icon('check')}Вы ответственный</span>` : deliveryAssignment(order)}</div></article>`).join('')}</div>${view.hasMore ? `<div class="list-bottom"><button class="button full" data-action="delivery-load-more" ${view.loadingMore ? 'disabled' : ''}>${view.loadingMore ? 'Загружаем…' : 'Показать ещё 10'}</button></div>` : ''}${mine && orders.length === 10 ? '<p class="result-caption">Показаны первые 10 ваших рейсов.</p>' : ''}` : emptyState}`;
}
async function loadDeliveries(signal, append = false) {
  if (view.loadingMore || (append && view.deliveryTab === 'mine')) return;
  const current = view;
  current.loadingMore = true;
  if (append) renderDeliveryCards();
  try {
    const path = current.deliveryTab === 'mine' ? '/api/delivery-orders/mine' : `/api/delivery-orders/unassigned?offset=${append ? current.offset : 0}`;
    const orders = await api(path, signal);
    if (signal.aborted) return;
    if (!Array.isArray(orders)) throw new ApiError(0);
    current.offset = (append ? current.offset : 0) + orders.length;
    current.orders = append ? [...current.orders, ...orders] : orders;
    current.hasMore = current.deliveryTab !== 'mine' && orders.length === 10;
    current.loadingMore = false;
    renderDeliveryCards();
  } catch (error) {
    if (signal.aborted) return;
    current.loadingMore = false;
    if (append) {
      renderDeliveryCards();
      $('.list-bottom', $('#delivery-list')).insertAdjacentHTML('beforeend', `<p class="field-error" role="alert">${escape(error.message)}</p>`);
    } else $('#delivery-list').innerHTML = errorMarkup(error);
  }
}
function renderDeliveries(params, signal) {
  const tab = params.get('tab') === 'mine' ? 'mine' : 'unassigned';
  savedDeliveryRoute = tab === 'mine' ? '#/deliveries?tab=mine' : '#/deliveries';
  view = { section: 'deliveries', deliveryTab: tab, orders: [], offset: 0, hasMore: false, loadingMore: false };
  const tabs = `<div class="delivery-tabs segmented" role="group" aria-label="Список рейсов">${[['unassigned', 'Свободные рейсы'], ['mine', 'Мои рейсы']].map(([key, label]) => `<button type="button" data-action="delivery-tab" data-tab="${key}" class="${key === tab ? 'active' : ''}" aria-pressed="${key === tab}">${label}</button>`).join('')}</div>`;
  main.innerHTML = `${heading('Доставки', 'Откройте рейс и сравните план с фактической загрузкой.')}<div id="delivery-feedback" role="status"></div>${deliveryLookup()}${tabs}<section class="panel" id="delivery-list" aria-live="polite">${loading(tab === 'mine' ? 'Загружаем ваши рейсы' : 'Загружаем свободные рейсы')}</section>`;
  loadDeliveries(signal);
}
function cargoBadge(product) {
  const state = cargoState(product);
  const labels = { unplanned: 'Вне плана', excess: `Сверх плана: ${number(product.inTruckQuantity - product.requiredQuantity)}`, shortage: `Не хватает: ${number(product.requiredQuantity - product.inTruckQuantity)}`, complete: 'По плану' };
  return `<span class="cargo-badge ${state}">${escape(labels[state])}</span>`;
}
function renderCargoRows() {
  const query = view.cargoQuery.toLocaleLowerCase('ru-RU');
  const products = view.cargo.filter(product =>
    (!query || product.productName.toLocaleLowerCase('ru-RU').includes(query) || product.productId.includes(query)) &&
    (view.cargoFilter === 'all' || (view.cargoFilter === 'differences' ? cargoState(product) !== 'complete' : cargoState(product) === view.cargoFilter)));
  $('#cargo-count').textContent = `${number(products.length)} из ${number(view.cargo.length)}`;
  document.querySelectorAll('[data-action="cargo-filter"]').forEach(button => {
    const active = button.dataset.filter === view.cargoFilter;
    button.classList.toggle('active', active);
    button.setAttribute('aria-pressed', String(active));
  });
  $('#cargo-results').innerHTML = products.length ? `<div class="cargo-table-wrap"><table class="cargo-table"><caption class="sr-only">План и фактическая загрузка рейса. Количество в коробках.</caption><thead><tr><th scope="col">Товар</th><th scope="col" class="cargo-number">Нужно</th><th scope="col" class="cargo-number">В машине</th><th scope="col">Загрузка</th></tr></thead><tbody>${products.map(product => `<tr><th scope="row"><a href="#/products/${encodeURIComponent(product.productId)}">${escape(product.productName)}</a><small>ID ${escape(product.productId)}</small></th><td class="cargo-number">${number(product.requiredQuantity)}</td><td class="cargo-number ${cargoState(product) === 'shortage' ? 'cargo-shortage' : ''}">${number(product.inTruckQuantity)}</td><td>${cargoBadge(product)}</td></tr>`).join('')}</tbody></table></div>` : empty('search', view.cargo.length ? 'Нет подходящих товаров' : 'В рейсе пока нет груза', view.cargo.length ? 'Измените название или выберите другой фильтр.' : 'Здесь появятся товары и количество коробок по плану и в машине.');
}
async function renderDelivery(id, params, signal) {
  view = { section: 'deliveries', deliveryId: id, cargo: [], cargoQuery: '', cargoFilter: 'all' };
  const backRoute = params.get('tab') === 'mine' ? '#/deliveries?tab=mine' : savedDeliveryRoute;
  const back = `<a class="back-link" href="${escape(backRoute)}">${icon('back')}${backRoute.includes('tab=mine') ? 'К моим рейсам' : 'К рейсам'}</a>`;
  main.innerHTML = `${back}<section class="panel">${loading('Загружаем груз рейса')}</section>`;
  try {
    const data = await api(`/api/delivery-orders/${encodeURIComponent(id)}`, signal);
    if (signal.aborted) return;
    if (!Array.isArray(data) && (!data?.order || !Array.isArray(data.items))) throw new ApiError(0);
    const order = Array.isArray(data) ? null : data.order;
    view.order = order;
    view.cargo = deliveryProducts(data);
    const totals = view.cargo.reduce((sum, product) => ({
      required: sum.required + product.requiredQuantity,
      inTruck: sum.inTruck + product.inTruckQuantity,
      shortage: sum.shortage + Math.max(0, product.requiredQuantity - product.inTruckQuantity)
    }), { required: 0, inTruck: 0, shortage: 0 });
    $('#breadcrumb').textContent = `Рейс №${id}`;
    document.title = `Рейс №${id} — Логистический помощник`;
    main.innerHTML = `${back}${heading(`Рейс №${escape(id)}`, 'План перевозки и фактический груз в машине.')}${deliveryOverview(order)}<div class="summary-row cargo-summary"><div class="panel summary-item">${icon('box')}<div><strong>${number(totals.required)}</strong><small>Нужно коробок</small></div></div><div class="panel summary-item">${icon('truck')}<div><strong>${number(totals.inTruck)}</strong><small>Коробок в машине</small></div></div><div class="panel summary-item">${icon('layers')}<div><strong>${number(totals.shortage)}</strong><small>Осталось загрузить</small></div></div></div><section class="panel cargo-panel" aria-labelledby="cargo-title"><div class="result-header"><div><h2 id="cargo-title">Товары рейса</h2><p class="cargo-unit">Количество в коробках · все упаковки товара вместе</p></div><span class="count-badge" id="cargo-count"></span></div><div class="cargo-toolbar"><div class="search-input">${icon('search')}<input id="cargo-query" type="search" placeholder="Название или ID товара" aria-label="Поиск по грузу" maxlength="256"></div><div class="segmented" role="group" aria-label="Фильтр груза">${[['all', 'Все'], ['shortage', 'Не хватает'], ['unplanned', 'Вне плана'], ['differences', 'Расхождения']].map(([filter, label]) => `<button type="button" data-action="cargo-filter" data-filter="${filter}" aria-pressed="false">${label}</button>`).join('')}</div></div><div id="cargo-results" aria-live="polite"></div></section>`;
    renderCargoRows();
  } catch (error) {
    if (!signal.aborted) main.innerHTML = `${back}<section class="panel">${errorMarkup(error instanceof ApiError ? error : new ApiError(0))}</section>`;
  }
}
async function route() {
  navigation?.abort(); navigation = new AbortController();
  const signal = navigation.signal, hash = location.hash.startsWith('#/') ? location.hash.slice(1) : '/products';
  const authPath = new URL(hash, location.origin).pathname;
  if (!auth.checked) {
    main.innerHTML = `<section class="panel">${loading('Проверяем вход')}</section>`;
    try { await auth.check(); }
    catch (error) {
      if (!signal.aborted) main.innerHTML = `<section class="panel">${errorMarkup(error)}</section>`;
      return;
    }
    if (signal.aborted) return;
  }
  if (authPath === '/login' || authPath === '/register') {
    if (auth.user) { navigate(auth.returnTo); return; }
    document.querySelectorAll('[data-nav]').forEach(link => { link.classList.remove('active'); link.removeAttribute('aria-current'); });
    $('#breadcrumb').textContent = authPath === '/register' ? 'Регистрация' : 'Вход';
    view = { section: 'auth' };
    auth.render(authPath.slice(1));
    return;
  }
  if (!auth.user) { auth.returnTo = location.hash.startsWith('#/') ? location.hash : '#/products'; navigate('#/login'); return; }
  const url = new URL(hash, location.origin), match = url.pathname.match(/^\/(products|points|deliveries)(?:\/([1-9]\d*))?\/?$/);
  const section = match?.[1] || (url.pathname === '/deliveries' ? 'deliveries' : 'products'), id = match?.[2];
  document.querySelectorAll('[data-nav]').forEach(link => { const active = link.dataset.nav === section; link.classList.toggle('active', active); if (active) link.setAttribute('aria-current', 'page'); else link.removeAttribute('aria-current'); });
  $('#breadcrumb').textContent = section === 'points' ? 'Склады и ПВЗ' : section === 'deliveries' ? 'Доставки' : 'Товары';
  document.title = `${$('#breadcrumb').textContent} — Логистический помощник`;
  if (section === 'products' && !id) { savedSearchRoute = location.hash.startsWith('#/products') ? location.hash : '#/products'; renderProducts(url.searchParams, signal); }
  else if (section === 'products') await renderProduct(id, url.searchParams, signal);
  else if (section === 'points' && !id) { savedPointRoute = location.hash || '#/points'; await renderPoints(url.searchParams, signal); }
  else if (section === 'points') await renderPoint(id, url.searchParams, signal);
  else if (id) await renderDelivery(id, url.searchParams, signal);
  else renderDeliveries(url.searchParams, signal);
}
function navigate(hash) { if (location.hash === hash) route(); else location.hash = hash; }
function updateMode(mode, point = false) {
  if (!Object.hasOwn(modes, mode)) return;
  const form = point ? $('#point-product-search') : $('#product-search'), value = $('input[name="query"]', form).value;
  view.mode = mode;
  if (point) form.outerHTML = pointSearchForm(mode, value); else form.innerHTML = productSearchForm(mode, value);
  $(`#${point ? 'point-product-query' : 'product-query'}`).focus();
}
function updatePointFilters() {
  if (!$('#points-content') || !Array.isArray(view.allPoints)) return;
  const params = new URLSearchParams();
  if (view.type) params.set('type', view.type);
  if (view.pointQuery) params.set('q', view.pointQuery);
  const hash = `#/points${params.size ? '?' + params : ''}`;
  history.replaceState(null, '', hash); savedPointRoute = hash; renderPointsList();
}
document.addEventListener('submit', event => {
  const form = event.target;
  if (form.id === 'auth-form') { event.preventDefault(); auth.submit(form); return; }
  if (!['product-search', 'point-search', 'point-product-search', 'delivery-lookup'].includes(form.id)) return;
  event.preventDefault();
  const query = $('input[name="query"]', form).value.trim();
  if (form.id === 'delivery-lookup') {
    const error = validate('id', query);
    showValidation(form, error ? 'Введите положительный номер рейса до 9223372036854775807.' : '');
    if (!error) navigate(`#/deliveries/${query}${view.deliveryTab === 'mine' ? '?tab=mine' : ''}`);
    return;
  }
  if (form.id === 'point-search') { view.pointQuery = query; updatePointFilters(); return; }
  const error = validate(view.mode, query); showValidation(form, error);
  if (!error) navigate(searchRoute(view.mode, query, form.id === 'point-product-search' ? `/points/${view.pointId}` : '/products'));
});
document.addEventListener('input', event => {
  if (event.target.id === 'cargo-query') { view.cargoQuery = event.target.value.trim(); renderCargoRows(); }
  else if (event.target.matches('#point-search input')) { view.pointQuery = event.target.value.trim(); updatePointFilters(); }
  else if (event.target.name === 'query') showValidation(event.target.closest('form'), '');
});
document.addEventListener('click', event => {
  const button = event.target.closest('[data-action]');
  if (!button) return;
  const action = button.dataset.action;
  if (action === 'refresh') route();
  else if (action === 'logout') {
    button.disabled = true;
    auth.logout().catch(error => { $('#account-error').textContent = error.message; }).finally(() => { button.disabled = false; });
  }
  else if (action === 'delivery-load-more') loadDeliveries(navigation.signal, true);
  else if (action === 'delivery-tab') navigate(button.dataset.tab === 'mine' ? '#/deliveries?tab=mine' : '#/deliveries');
  else if (action === 'assign-delivery') assignDelivery(button.dataset.deliveryId);
  else if (action === 'cargo-filter') { view.cargoFilter = button.dataset.filter; renderCargoRows(); }
  else if (action === 'search-mode') updateMode(button.dataset.mode);
  else if (action === 'point-product-mode') updateMode(button.dataset.mode, true);
  else if (action === 'clear-input') { const form = button.closest('form'), input = $('input[name="query"]', form); input.value = ''; showValidation(form, ''); input.focus(); }
  else if (action === 'clear-history') { try { localStorage.removeItem(historyKey); } catch {} renderHistory(); }
  else if (action === 'history') { const item = readHistory()[Number(button.dataset.index)]; if (item) navigate(searchRoute(item.mode, item.query)); }
  else if (action === 'load-more') loadProducts(navigation.signal, true);
  else if (action === 'point-filter') { view.type = button.dataset.type; updatePointFilters(); }
  else if (action === 'reset-points') { view.type = ''; view.pointQuery = ''; $('#point-search input').value = ''; updatePointFilters(); }
  else if (action === 'clear-point-query') { view.pointQuery = ''; $('#point-search input').value = ''; updatePointFilters(); $('#point-search input').focus(); }
  else if (action === 'point-load-more') loadPointProducts(navigation.signal, true);
  else if (action === 'retry-point-products') loadPointProducts(navigation.signal);
  else if (action === 'about') $('#about-dialog').showModal();
  else if (action === 'close-dialog') $('#about-dialog').close();
});
window.addEventListener('hashchange', () => { if (location.hash === '#main') { main.focus(); return; } window.scrollTo(0, 0); route(); });
window.addEventListener('online', () => { if (auth.user) api('/api/logistics-points', navigation?.signal).catch(() => {}); });
window.addEventListener('offline', () => setConnection('offline', 'Нет соединения'));
route();
