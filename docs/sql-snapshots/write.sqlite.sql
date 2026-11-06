-- InsertOne：插入一行
INSERT INTO "t_tag" ("name") VALUES (?);
-- params: ['性能']

-- InsertMany：插入多行：每行一条语句
INSERT INTO "t_tag" ("name") VALUES (?);
-- params: ['测试']
INSERT INTO "t_tag" ("name") VALUES (?);
-- params: ['运维']

-- InsertBatch：批量写入：同一 SQL 的多行一次发出，整批在一个事务里
INSERT INTO "t_tag" ("name") VALUES (?);
-- params: ['前端']
-- params: ['后端']
-- params: ['数据']

-- UpdateWithExpression：按条件更新：值可以引用原列
UPDATE "t_post" SET "views" = ("views" + ?),"title" = CONCAT("title",?) WHERE "id" = ?;
-- params: [100, '（修订）', 1]

-- UpdateThroughNavigation：条件里用集合导航
UPDATE "t_user" AS t SET "level" = ? WHERE EXISTS(SELECT 1 FROM "t_post" t1 WHERE t1."author_id" = t."id" AND t1."views" >= ? LIMIT 1);
-- params: ['senior', 2000]

-- UpdateEntity：按实体更新：按主键写全部列
SELECT "id","name","price","stock" FROM "t_product" WHERE "id" = ?;
-- params: [6]
UPDATE "t_product" SET "name" = ?,"price" = ?,"stock" = ? WHERE "id" = ?;
-- params: ['贴纸礼包', 25.00, 200, 6]

-- UpdateColumns：只写指定的列
SELECT "id","name","price","stock" FROM "t_product" WHERE "id" = ?;
-- params: [6]
UPDATE "t_product" SET "stock" = ? WHERE "id" = ?;
-- params: [199, 6]

-- UpdateReturning：更新并返回受影响的行：UPDATE … RETURNING（PostgreSQL、SQLite）
UPDATE "t_product" SET "stock" = ("stock" - ?) WHERE "id" = ? RETURNING "stock";
-- params: [1, 5]

-- Delete：删除
DELETE FROM "t_comment" WHERE "likes" < ?;
-- params: [3]

-- LogicDelete：逻辑删除：改写成 UPDATE，版本号一起加一
SELECT "id","title","deleted","version","created_by","updated_at" FROM "t_doc" WHERE "deleted" = ? AND "id" = ?;
-- params: [false, 2]
UPDATE "t_doc" SET "deleted" = ?,"version" = ? WHERE "deleted" = ? AND "version" = ? AND "id" = ?;
-- params: [true, 2, false, 1, 2]

-- Transaction：事务：出错时整体回滚
UPDATE "t_product" SET "stock" = ("stock" - ?) WHERE "id" = ?;
-- params: [1, 5]
SELECT "id","name","price","stock" FROM "t_product" WHERE "id" = ?;
-- params: [5]

