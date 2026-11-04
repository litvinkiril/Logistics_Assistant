// Both quantities use the API's unit: physical boxes, grouped by product.
export function deliveryProducts(payload) {
  const items = Array.isArray(payload) ? payload : payload?.items;
  if (!Array.isArray(items)) throw new Error('Некорректный список груза.');

  const products = new Map();
  for (const item of items) {
    const id = item.productId;
    if ((typeof id !== 'string' && !Number.isSafeInteger(id)) || !/^[1-9]\d*$/.test(String(id))) {
      throw new Error('Некорректный ID товара.');
    }
    const required = item.requiredQuantity ?? item.boxQuantity;
    const inTruck = item.inTruckQuantity ?? item.inTruckBoxQuantity;
    if (![required, inTruck].every(value => Number.isSafeInteger(value) && value >= 0)) {
      throw new Error('Некорректное количество груза.');
    }
    const key = String(id);
    const product = products.get(key) ?? {
      productId: key,
      productName: item.productName || `Товар №${key}`,
      requiredQuantity: 0,
      inTruckQuantity: 0
    };
    product.requiredQuantity += required;
    product.inTruckQuantity += inTruck;
    if (!Number.isSafeInteger(product.requiredQuantity) || !Number.isSafeInteger(product.inTruckQuantity)) {
      throw new Error('Слишком большое количество груза.');
    }
    products.set(key, product);
  }
  return [...products.values()].sort((a, b) =>
    a.productName.localeCompare(b.productName, 'ru') || a.productId.localeCompare(b.productId, 'ru', { numeric: true }));
}

export function cargoState(product) {
  if (product.requiredQuantity === 0 && product.inTruckQuantity > 0) return 'unplanned';
  if (product.inTruckQuantity > product.requiredQuantity) return 'excess';
  if (product.inTruckQuantity < product.requiredQuantity) return 'shortage';
  return 'complete';
}

// Send Int64 IDs as decimal strings: ASP.NET Core accepts them without JS rounding.
export function loadBoxRequest(itemId, mode, value) {
  const positiveId = value => /^[1-9]\d{0,18}$/.test(String(value)) && BigInt(value) <= 9223372036854775807n;
  if (!positiveId(itemId)) throw new Error('Выберите позицию плана.');
  const query = value.trim();
  if (mode === 'id') {
    if (!positiveId(query)) throw new Error('Введите положительный ID коробки до 9223372036854775807.');
    return { orderItemId: String(itemId), physicalBoxId: query };
  }
  if (mode !== 'url' || !query) throw new Error('Вставьте ссылку с этикетки коробки.');
  return { orderItemId: String(itemId), url: query };
}

export function nextDeliveryStatus(status) {
  return { new: 'loading', loading: 'unloading', unloading: 'completed' }[status] ?? null;
}
