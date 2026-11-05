-- InsertOne：插入一行
INSERT INTO "t_tag" ("name") VALUES (?);
-- params: ['性能']

-- InsertMany：批量插入
INSERT INTO "t_tag" ("name") VALUES (?);
-- params: ['测试']
INSERT INTO "t_tag" ("name") VALUES (?);
-- params: ['运维']

-- UpdateWithExpression：按条件更新：值可以引用原列
UPDATE "t_post" SET "views" = ("views" + ?),"title" = "title" || ? WHERE "id" = ?;
-- params: [100, '（修订）', 1]

-- UpdateThroughNavigation：条件里用集合导航
UPDATE "t_user" t SET t."level" = ? WHERE EXISTS(SELECT 1 FROM "t_post" t1 WHERE t1."author_id" = t."id" AND t1."views" >= ? AND ROWNUM < 2);
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

