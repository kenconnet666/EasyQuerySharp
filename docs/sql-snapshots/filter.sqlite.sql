-- FilterOperators：多属性 OR、区间、集合导航上的条件（EXISTS）
SELECT t."title" FROM "t_post" t LEFT JOIN "t_user" t1 ON t1."id" = t."author_id" WHERE (t."title" LIKE ? OR t1."name" LIKE ?) AND (t."published_at" >= ? AND t."published_at" <= ?) AND EXISTS(SELECT 1 FROM "t_tag" t2 WHERE EXISTS(SELECT 1 FROM "t_post_tag" t3 WHERE t3."tag_id" = t2."id" AND t3."post_id" = t."id" LIMIT 1) AND t2."name" = ? LIMIT 1) ORDER BY t."id" ASC;
-- params: ['%林%', '%林%', '2025-01-01', '2025-12-31', 'ORM']

-- SortBy：动态排序：SortBy，只认允许的属性
SELECT t."title" FROM "t_post" t ORDER BY t."views" DESC LIMIT 3;

-- ColumnByPath：按路径取列：Sql.Column
SELECT t."title" FROM "t_post" t LEFT JOIN "t_user" t1 ON t1."id" = t."author_id" WHERE t1."age" >= ? ORDER BY t."id" ASC;
-- params: [35]

