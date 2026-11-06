-- GroupBy：分组聚合：Count / Sum
SELECT t1."order_id" AS "order_id",t1."lines" AS "lines",t1."quantity" AS "quantity",t1."amount" AS "amount" FROM (SELECT t."order_id" AS "order_id",COUNT(*) AS "lines",SUM(t."quantity") AS "quantity",SUM((t."price" * t."quantity")) AS "amount" FROM "t_order_item" t GROUP BY t."order_id") t1 ORDER BY t1."order_id" ASC;

-- Having：分组后的条件与排序：Having、按聚合排序
SELECT t."order_id" AS "order_id",COUNT(*) AS "lines",SUM(t."quantity") AS "quantity",SUM((t."price" * t."quantity")) AS "amount" FROM "t_order_item" t GROUP BY t."order_id" HAVING COUNT(*) >= ? ORDER BY SUM((t."price" * t."quantity")) DESC;
-- params: [2]

-- MultipleKeys：多列分组：键是匿名对象
SELECT t1."author_id" AS "author_id",t1."status" AS "status",t1."posts" AS "posts" FROM (SELECT t."author_id" AS "author_id",t."status" AS "status",COUNT(*) AS "posts" FROM "t_post" t WHERE t."author_id" <= ? GROUP BY t."author_id",t."status") t1 ORDER BY t1."author_id" ASC;
-- params: [3]

-- ByNavigation：按导航分组、按条件计数、字符串聚合
SELECT t3."company" AS "company",t3."posts" AS "posts",t3."drafts" AS "drafts",t3."best" AS "best",t3."titles" AS "titles" FROM (SELECT t2."name" AS "company",COUNT(*) AS "posts",COUNT((CASE WHEN t."status" = ? THEN ? ELSE NULL END)) AS "drafts",MAX(t."views") AS "best",LISTAGG(TO_CHAR(t."title"), ?) AS "titles" FROM "t_post" t LEFT JOIN "t_user" t1 ON t1."id" = t."author_id" LEFT JOIN "t_company" t2 ON t2."id" = t1."company_id" WHERE t1."company_id" IS NOT NULL GROUP BY t2."name") t3 ORDER BY t3."company" ASC;
-- params: ['draft', 1, '、']

-- CountDistinct：去重计数：g.Distinct()
SELECT t2."city" AS "city",t2."companies" AS "companies",t2."users" AS "users" FROM (SELECT t1."city" AS "city",COUNT(DISTINCT t."company_id") AS "companies",COUNT(*) AS "users" FROM "t_user" t LEFT JOIN "t_company" t1 ON t1."id" = t."company_id" WHERE t."company_id" IS NOT NULL GROUP BY t1."city") t2 ORDER BY t2."city" ASC;

-- Keys：只要分组键：Keys()
SELECT "post_id" FROM "t_comment" GROUP BY "post_id" HAVING SUM("likes") >= ? ORDER BY "post_id" ASC;
-- params: [20]

-- Aggregates：整表聚合：SumAsync / MaxAsync / AverageAsync / CountAsync
SELECT SUM(t."views") FROM "t_post" t WHERE t."status" = ?;
-- params: ['published']
SELECT MAX(t."views") FROM "t_post" t WHERE t."status" = ?;
-- params: ['published']
SELECT AVG(t."views") FROM "t_post" t WHERE t."status" = ?;
-- params: ['published']
SELECT COUNT(*) FROM "t_post" WHERE "status" = ?;
-- params: ['published']

