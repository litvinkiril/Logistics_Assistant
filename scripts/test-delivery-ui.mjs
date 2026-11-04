import test from 'node:test';
import assert from 'node:assert/strict';
import { deliveryProducts, cargoState } from '../Logistics_Assistant/wwwroot/js/delivery-data.js';

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
