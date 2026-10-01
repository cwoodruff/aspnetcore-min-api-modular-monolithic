# 0004. Orders refers to Administration's Customer by id only

- Status: Accepted
- Date: 2026-09-30
- Phase: 2 (see `docs/upgrade-plan.md`)

## Context

`orders.Invoice.CustomerId` points at `administration.Customer`. Until phase 2
it was a foreign key with an `Invoice.Customer` navigation and a
`Customer.Invoices` inverse, and `GET /api/orders/invoices/{id}` embedded the
customer (with the support rep's name) in one joined query. `GET
/api/admin/customers/{id}` embedded the customer's invoices the same way.

## Decision

- Drop the navigation, its inverse and the foreign key
  `FK_Invoice_Customer_CustomerId`. Keep `CustomerId` as an indexed column.
- Invoice responses carry `CustomerId` only. Customer responses no longer list
  invoices; `GET /api/orders/invoices/customer/{id}` already serves that.
- If an invoice response needs customer details again, Orders asks
  Administration through a read contract in `Administration.Contracts`
  (phase 3), not a join.

## Consistency

Application-validated id, no database foreign key, eventual. Today "validated" means `InvoiceValidator` requires a non-null `CustomerId`;
nothing checks that the customer exists. Phase 3's read contract can add that
check at write time.

## Consequences

- The database accepts an invoice for a customer id that does not exist, and
  accepts deleting a customer that has invoices (there is no delete endpoint
  for customers today).
- If a customer row disappears, its invoices keep a dangling `CustomerId`.
  Nothing fails at read time; the invoice simply refers to nobody. Phase 6
  adds an orphan check that reports such rows.
- The invoice endpoint loses the embedded customer and support-rep name.

## How to reverse

Re-add the navigation and `HasOne(...).WithMany(...)` in a context that maps
both entities, which means undoing ADR-0003 for these two modules, and add a
migration that recreates the foreign key after removing any orphans.
