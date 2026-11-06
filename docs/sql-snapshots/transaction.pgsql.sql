-- BeginTransaction：显式事务：同一异步流程里这个库的语句自动加入
UPDATE "t_product" SET "stock" = ("stock" - ?) WHERE "id" = ?;
-- params: [1, 3]
INSERT INTO "t_order" ("user_id","status","created_at") VALUES (?,?,?);
-- params: [2, 'pending', '2025-09-01 00:00:00']
SELECT "id","name","price","stock" FROM "t_product" WHERE "id" = ?;
-- params: [3]

-- DisposeRollsBack：没有提交就释放：回滚
UPDATE "t_product" SET "stock" = ? WHERE "id" = ?;
-- params: [0, 3]
SELECT "id","name","price","stock" FROM "t_product" WHERE "id" = ?;
-- params: [3]

-- Nested：已经在事务里再开事务：抛异常

