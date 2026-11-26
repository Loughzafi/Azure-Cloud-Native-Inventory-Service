CREATE TABLE inventory_items (
    id uuid PRIMARY KEY,
    sku text NOT NULL,
    name text NOT NULL,
    quantity integer NOT NULL CHECK (quantity >= 0),
    updated_at timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT ck_inventory_items_sku_length CHECK (char_length(sku) BETWEEN 1 AND 64),
    CONSTRAINT ck_inventory_items_name_length CHECK (char_length(name) BETWEEN 1 AND 200)
);

CREATE UNIQUE INDEX ux_inventory_items_sku_normalized
    ON inventory_items (lower(sku));
