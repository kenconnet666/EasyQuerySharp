-- TrackedUpdate：追踪作用域里查出的实体，更新时只写改过的列
SELECT "id","name","price","stock" FROM "t_product" WHERE "id" = ?;
-- params: [5]
UPDATE "t_product" SET "stock" = ? WHERE "id" = ?;
-- params: [7, 5]

-- TrackedChanges：查看改了哪些属性：GetTrackedChanges
SELECT "id","name","age","company_id","department_id","manager_id","level","joined_at" FROM "t_user" WHERE "id" = ?;
-- params: [1]

-- NoTracking：作用域里不追踪某条查询：AsNoTracking
SELECT "id","name","age","company_id","department_id","manager_id","level","joined_at" FROM "t_user" WHERE "id" = ?;
-- params: [1]

-- SaveAggregate：保存聚合：改数量、删一行、加一行
SELECT "id","user_id","status","created_at" FROM "t_order" WHERE "id" = ?;
-- params: [1]
SELECT t."order_id",t."product_id",t."quantity",t."price" FROM "t_order_item" t WHERE t."order_id" IN (?) ORDER BY t."product_id" ASC;
-- params: [1]
DELETE FROM "t_order_item" WHERE "order_id" = ? AND "product_id" = ?;
-- params: [1, 6]
INSERT INTO "t_order_item" ("order_id","product_id","quantity","price") VALUES (?,?,?,?);
-- params: [1, 3, 1, 199.00]
UPDATE "t_order_item" SET "quantity" = ? WHERE "order_id" = ? AND "product_id" = ?;
-- params: [2, 1, 1]
SELECT t1."name" || ? || t."quantity" FROM "t_order_item" t LEFT JOIN "t_product" t1 ON t1."id" = t."product_id" WHERE t."order_id" = ? ORDER BY t."product_id" ASC;
-- params: [' × ', 1]

-- RemoveRoot：删除聚合根：RemoveRoot，拥有的子对象一起删
SELECT "id","user_id","status","created_at" FROM "t_order" WHERE "id" = ?;
-- params: [5]
SELECT t."order_id",t."product_id",t."quantity",t."price" FROM "t_order_item" t WHERE t."order_id" IN (?);
-- params: [5]
DELETE FROM "t_order_item" WHERE "order_id" = ? AND "product_id" = ?;
-- params: [5, 2]
DELETE FROM "t_order" WHERE "id" = ?;
-- params: [5]
SELECT COUNT(*) FROM "t_order_item" WHERE "order_id" = ?;
-- params: [5]

