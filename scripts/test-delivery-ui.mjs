import test from 'node:test';
import assert from 'node:assert/strict';
import { deliveryProducts, cargoState, loadBoxRequest, nextDeliveryStatus } from '../Logistics_Assistant/wwwroot/js/delivery-data.js';

test('loading preserves Int64 IDs and sends exactly one box selector', () => {
  assert.deepEqual(loadBoxRequest('9223372036854775806', 'id', ' 9223372036854775807 '), {
    orderItemId: '9223372036854775806', physicalBoxId: '9223372036854775807'
  });
  assert.deepEqual(loadBoxRequest('15', 'url', ' https://example.test/box/42 '), {
    orderItemId: '15', url: 'https://example.test/box/42'
  });
  for (const value of ['', '0', '-1', '1.5', '1e3', '9223372036854775808']) {
    assert.throws(() => loadBoxRequest('15', 'id', value));
  }
  assert.throws(() => loadBoxRequest('', 'id', '1'));
  assert.throws(() => loadBoxRequest('15', 'url', '  '));
});

test('workflow exposes only the next supported stage', () => {
  assert.equal(nextDeliveryStatus('new'), 'loading');
  assert.equal(nextDeliveryStatus('loading'), 'unloading');
  assert.equal(nextDeliveryStatus('unloading'), 'completed');
  assert.equal(nextDeliveryStatus('completed'), null);
  assert.equal(nextDeliveryStatus('cancelled'), null);
});

test('combines packaging and destinations into one product without losing quantities', () => {
  assert.deepEqual(deliveryProducts({ items: [
    { productId: '42', productName: 'Шкаф', boxQuantity: 5, inTruckBoxQuantity: 2 },
    { productId: '42', productName: 'Шкаф', boxQuantity: 3, inTruckBoxQuantity: 1 }
  ] }), [{ productId: '42', productName: 'Шкаф', requiredQuantity: 8, inTruckQuantity: 3 }]);
});

test('keeps unplanned cargo and zero actual quantities from the product response', () => {
  const result = deliveryProducts([
    { productId: '1', productName: 'Стол', requiredQuantity: 2, inTruckQuantity: 0 },
    { productId: '2', productName: 'Стул', requiredQuantity: 0, inTruckQuantity: 4 }
  ]);
  assert.equal(result.length, 2);
  assert.equal(cargoState(result.find(product => product.productId === '1')), 'shortage');
  assert.equal(cargoState(result.find(product => product.productId === '2')), 'unplanned');
});

test('distinguishes exact loading, shortages, and excess', () => {
  assert.equal(cargoState({ requiredQuantity: 5, inTruckQuantity: 5 }), 'complete');
  assert.equal(cargoState({ requiredQuantity: 5, inTruckQuantity: 3 }), 'shortage');
  assert.equal(cargoState({ requiredQuantity: 5, inTruckQuantity: 6 }), 'excess');
});

test('does not merge adjacent Int64 product IDs', () => {
  const result = deliveryProducts(['9223372036854775806', '9223372036854775807'].map(productId => ({
    productId, productName: 'Товар', requiredQuantity: 1, inTruckQuantity: 0
  })));
  assert.equal(result.length, 2);
  assert.notEqual(result[0].productId, result[1].productId);
});

test('accepts empty cargo and rejects invalid or missing counts instead of showing false zeros', () => {
  assert.deepEqual(deliveryProducts({ items: [] }), []);
  for (const item of [
    { productId: '1', requiredQuantity: 1 },
    { productId: '1', requiredQuantity: -1, inTruckQuantity: 0 },
    { productId: '1', requiredQuantity: 1, inTruckQuantity: '2' },
    { productId: 9223372036854775807, requiredQuantity: 1, inTruckQuantity: 0 }
  ]) assert.throws(() => deliveryProducts([item]));
});
