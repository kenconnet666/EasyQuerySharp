-- FirstQuery：条件、隐式 JOIN、排序、投影
SELECT t."name",t."age",t1."name" AS "company" FROM "t_user" t LEFT JOIN "t_company" t1 ON t1."id" = t."company_id" WHERE t."age" >= ? AND t1."city" = ? ORDER BY t."name" ASC;
-- params: [30, '杭州']

-- CollectionNavigation：隐式子查询与对象树：集合导航上的 Any / Count / Select
SELECT t."name",(SELECT COUNT(*) FROM "t_user" t2 WHERE t2."company_id" = t."id") AS "headcount",t."id" AS "__relation__id" FROM "t_company" t WHERE EXISTS(SELECT 1 FROM "t_user" t1 WHERE t1."company_id" = t."id" AND t1."age" < ? LIMIT 1) ORDER BY t."id" ASC;
-- params: [30]
SELECT t."name",t."company_id" AS "__relation__companyId" FROM "t_user" t WHERE t."company_id" IN (?,?,?) ORDER BY t."id" ASC;
-- params: [1, 2, 3]

-- FirstPage：分页
SELECT COUNT(*) FROM "t_user";
SELECT t."name" FROM "t_user" t ORDER BY t."id" ASC LIMIT 3;

-- Writes：插入、更新、删除
INSERT INTO "t_company" ("name","city") VALUES (?,?);
-- params: ['东海数据', '宁波']
UPDATE "t_company" SET "city" = ? WHERE "name" = ?;
-- params: ['舟山', '东海数据']
DELETE FROM "t_company" WHERE "name" = ?;
-- params: ['东海数据']

