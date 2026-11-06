-- ModTables：按 id 取模分三张表：写入落到各自的表，按 id 查只查一张，IN 查命中的几张，全表在库内 UNION ALL；按条件更新逐张表执行
CREATE TABLE IF NOT EXISTS "t_order_log_0" ( 
"id" VARCHAR(255) NOT NULL ,
"user_id" integer NOT NULL ,
"amount" integer NOT NULL ,
"created_at" TEXT NOT NULL , 
 PRIMARY KEY ("id")
);
CREATE TABLE IF NOT EXISTS "t_order_log_1" ( 
"id" VARCHAR(255) NOT NULL ,
"user_id" integer NOT NULL ,
"amount" integer NOT NULL ,
"created_at" TEXT NOT NULL , 
 PRIMARY KEY ("id")
);
CREATE TABLE IF NOT EXISTS "t_order_log_2" ( 
"id" VARCHAR(255) NOT NULL ,
"user_id" integer NOT NULL ,
"amount" integer NOT NULL ,
"created_at" TEXT NOT NULL , 
 PRIMARY KEY ("id")
);
INSERT INTO "t_order_log_2" ("id","user_id","amount","created_at") VALUES (?,?,?,?);
-- params: ['a1', 1, 10, '2025-01-05 00:00:00']
INSERT INTO "t_order_log_0" ("id","user_id","amount","created_at") VALUES (?,?,?,?);
-- params: ['a2', 1, 20, '2025-02-05 00:00:00']
INSERT INTO "t_order_log_1" ("id","user_id","amount","created_at") VALUES (?,?,?,?);
-- params: ['a3', 2, 30, '2025-03-05 00:00:00']
INSERT INTO "t_order_log_2" ("id","user_id","amount","created_at") VALUES (?,?,?,?);
-- params: ['a4', 2, 40, '2025-03-06 00:00:00']
SELECT "id","user_id","amount","created_at" FROM "t_order_log_2" WHERE "id" = ?;
-- params: ['a1']
SELECT t."amount" FROM (SELECT * FROM "t_order_log_0" UNION ALL SELECT * FROM "t_order_log_1") t WHERE t."id" IN (?,?) ORDER BY t."id" ASC;
-- params: ['a2', 'a3']
SELECT t."id" FROM (SELECT * FROM "t_order_log_0" UNION ALL SELECT * FROM "t_order_log_1" UNION ALL SELECT * FROM "t_order_log_2") t ORDER BY t."amount" DESC LIMIT 2;
UPDATE "t_order_log_0" SET "amount" = ("amount" + ?) WHERE "user_id" = ?;
-- params: [1, 1]
UPDATE "t_order_log_1" SET "amount" = ("amount" + ?) WHERE "user_id" = ?;
-- params: [1, 1]
UPDATE "t_order_log_2" SET "amount" = ("amount" + ?) WHERE "user_id" = ?;
-- params: [1, 1]
SELECT COUNT(*) FROM (SELECT * FROM "t_order_log_0" UNION ALL SELECT * FROM "t_order_log_1" UNION ALL SELECT * FROM "t_order_log_2") t;
DROP TABLE IF EXISTS "t_order_log_0";
DROP TABLE IF EXISTS "t_order_log_1";
DROP TABLE IF EXISTS "t_order_log_2";

-- MonthTables：按月分表：时间条件只查命中的月份；没写排序、取前几行时按规则的默认排序（时间倒序）
CREATE TABLE IF NOT EXISTS "t_order_log_202501" ( 
"id" VARCHAR(255) NOT NULL ,
"user_id" integer NOT NULL ,
"amount" integer NOT NULL ,
"created_at" TEXT NOT NULL , 
 PRIMARY KEY ("id")
);
CREATE TABLE IF NOT EXISTS "t_order_log_202502" ( 
"id" VARCHAR(255) NOT NULL ,
"user_id" integer NOT NULL ,
"amount" integer NOT NULL ,
"created_at" TEXT NOT NULL , 
 PRIMARY KEY ("id")
);
CREATE TABLE IF NOT EXISTS "t_order_log_202503" ( 
"id" VARCHAR(255) NOT NULL ,
"user_id" integer NOT NULL ,
"amount" integer NOT NULL ,
"created_at" TEXT NOT NULL , 
 PRIMARY KEY ("id")
);
INSERT INTO "t_order_log_202501" ("id","user_id","amount","created_at") VALUES (?,?,?,?);
-- params: ['j1', 1, 10, '2025-01-05 00:00:00']
INSERT INTO "t_order_log_202502" ("id","user_id","amount","created_at") VALUES (?,?,?,?);
-- params: ['f1', 1, 20, '2025-02-05 00:00:00']
INSERT INTO "t_order_log_202503" ("id","user_id","amount","created_at") VALUES (?,?,?,?);
-- params: ['m1', 2, 30, '2025-03-05 00:00:00']
SELECT SUM(t."amount") FROM (SELECT * FROM "t_order_log_202502" UNION ALL SELECT * FROM "t_order_log_202503") t WHERE t."created_at" >= ?;
-- params: ['2025-02-01 00:00:00']
SELECT t."id" FROM (SELECT * FROM "t_order_log_202501" UNION ALL SELECT * FROM "t_order_log_202502" UNION ALL SELECT * FROM "t_order_log_202503") t ORDER BY t."created_at" DESC LIMIT 2;
DROP TABLE IF EXISTS "t_order_log_202501";
DROP TABLE IF EXISTS "t_order_log_202502";
DROP TABLE IF EXISTS "t_order_log_202503";

