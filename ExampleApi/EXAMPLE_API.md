# Example API

A simple e-commerce API used as test data for StepWise Management workflows. Runs on `http://localhost:5010`.

All request and response bodies use camelCase JSON. Non-2xx responses return `{ "error": "<message>" }`.

---

## Authentication

Most endpoints require a Bearer token obtained from `POST /auth/login`. Admin endpoints use a static `X-Admin-Key` header instead.

**Seeded users**

| Email | Password |
|-------|----------|
| alice@example.com | password |
| bob@example.com | password |

---

## Endpoints

### Auth

#### `POST /auth/login`
```json
{ "email": "alice@example.com", "password": "password" }
```
Response `200`: `{ "token": "...", "userId": "usr_alice", "name": "Alice Smith" }`

---

### Products

#### `GET /products`
Query params: `category` (string), `inStock` (bool)

Response `200`: array of `{ id, name, category, price, stock }`

**Seeded products** (10 items across `electronics`, `books`, `clothing`):

| id | name | category | price | stock |
|----|------|----------|-------|-------|
| prod_01 | Wireless Headphones | electronics | 79.99 | 50 |
| prod_02 | Mechanical Keyboard | electronics | 129.99 | 30 |
| prod_03 | USB-C Hub | electronics | 39.99 | 0 |
| prod_04 | The Pragmatic Programmer | books | 49.95 | 20 |
| prod_05 | Clean Code | books | 34.99 | 15 |
| prod_06 | Designing Data-Intensive Applications | books | 59.99 | 10 |
| prod_07 | Merino Wool T-Shirt | clothing | 44.99 | 100 |
| prod_08 | Waterproof Jacket | clothing | 89.99 | 25 |
| prod_09 | Running Shorts | clothing | 29.99 | 60 |
| prod_10 | Laptop Stand | electronics | 54.99 | 40 |

#### `GET /products/{id}`
Response `200`: `{ id, name, category, price, stock, description }`

---

### Cart

All cart endpoints require `Authorization: Bearer <token>`.

#### `GET /cart`
Response `200`: `{ "items": [{ productId, name, price, quantity, lineTotal }], "total": 0.00 }`

#### `POST /cart/items`
```json
{ "productId": "prod_01", "quantity": 1 }
```
Response `201`: `{ "productId": "...", "quantity": 1 }`

#### `PATCH /cart/items/{productId}`
```json
{ "quantity": 2 }
```
Response `200`: `{ "productId": "...", "quantity": 2 }`

#### `DELETE /cart/items/{productId}`
Response `204`

---

### Orders

All order endpoints require `Authorization: Bearer <token>`.

#### `POST /orders`
Places an order from the current cart. Optional body: `{ "voucherCode": "SAVE10" }`.

Response `201`: `{ id, status, subtotal, discount, total, voucherCode, createdAt }`

Order status transitions automatically: `pending` → `processing` (after 3s) → `shipped` (after 5s more).

#### `GET /orders`
Query param: `status` (string)

Response `200`: array of `{ id, status, total, createdAt }`

#### `GET /orders/{id}`
Response `200`: `{ id, status, subtotal, discount, total, voucherCode, createdAt, itemCount }`

#### `GET /orders/{id}/items`
Response `200`: array of `{ productId, name, price, quantity, lineTotal }`

#### `POST /orders/{id}/cancel`
Response `200`: `{ "id": "...", "status": "cancelled" }`. Fails if status is not `pending`.

---

### Vouchers

#### `POST /vouchers/validate`
```json
{ "code": "SAVE10" }
```
Response `200`: `{ "valid": true, "code": "SAVE10", "discountPct": 10 }`

**Seeded vouchers**: `SAVE10` (10% off), `HALF50` (50% off)

---

### Admin

All admin endpoints require `X-Admin-Key: admin-secret`.

#### `POST /admin/users`
```json
{ "email": "...", "password": "...", "name": "...", "id": "optional" }
```
Response `201`: `{ id, email, name }`

#### `POST /admin/products`
```json
{ "name": "...", "category": "...", "price": 9.99, "stock": 10, "description": "optional", "id": "optional" }
```
Response `201`: `{ id, name, category, price, stock }`

#### `PATCH /admin/products/{id}/stock`
```json
{ "stock": 25 }
```
Response `200`: `{ "id": "...", "stock": 25 }`

#### `DELETE /admin/products/{id}`
Response `204`

#### `POST /admin/vouchers`
```json
{ "code": "NEW20", "discountPct": 20 }
```
Response `201`: `{ code, discountPct }`

#### `DELETE /admin/vouchers/{code}`
Response `204`
