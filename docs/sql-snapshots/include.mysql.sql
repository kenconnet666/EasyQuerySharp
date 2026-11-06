-- IncludeMany：带出对多：每层一次 IN 查询，可以加条件和排序
SELECT `id`,`name`,`age`,`company_id`,`department_id`,`manager_id`,`level`,`joined_at` FROM `t_user` WHERE `company_id` = ? ORDER BY `id` ASC;
-- params: [1]
SELECT t.`id`,t.`author_id`,t.`category_id`,t.`title`,t.`status`,t.`views`,t.`published_at` FROM `t_post` t WHERE t.`status` = ? AND t.`author_id` IN (?,?,?,?) ORDER BY t.`views` DESC;
-- params: ['published', 1, 2, 7, 8]

-- IncludeOneAndManyToMany：带出对一与多对多
SELECT `id`,`author_id`,`category_id`,`title`,`status`,`views`,`published_at` FROM `t_post` WHERE `views` >= ? ORDER BY `id` ASC;
-- params: [2000]
SELECT t.`id`,t.`name`,t.`age`,t.`company_id`,t.`department_id`,t.`manager_id`,t.`level`,t.`joined_at` FROM `t_user` t WHERE t.`id` IN (?,?,?);
-- params: [2, 3, 9]
SELECT `post_id`,`tag_id` FROM `t_post_tag` WHERE `post_id` IN (?,?,?);
-- params: [3, 4, 9]
SELECT t.`id`,t.`name` FROM `t_tag` t WHERE t.`id` IN (?,?);
-- params: [3, 2]

-- IncludeNested：嵌套带出：配置里再 Include
SELECT `id`,`user_id`,`status`,`created_at` FROM `t_order` WHERE `user_id` = ? ORDER BY `id` ASC;
-- params: [1]
SELECT t.`order_id`,t.`product_id`,t.`quantity`,t.`price` FROM `t_order_item` t WHERE t.`order_id` IN (?,?);
-- params: [1, 2]
SELECT t.`id`,t.`name`,t.`price`,t.`stock` FROM `t_product` t WHERE t.`id` IN (?,?,?);
-- params: [1, 6, 3]

-- IncludeTake：每个父对象只带出前几条：按父对象分区编行号
SELECT t.`id`,t.`name`,t.`age`,t.`company_id`,t.`department_id`,t.`manager_id`,t.`level`,t.`joined_at` FROM `t_user` t WHERE EXISTS(SELECT 1 FROM `t_post` t1 WHERE t1.`author_id` = t.`id` LIMIT 1) ORDER BY t.`id` ASC;
SELECT t2.`id` AS `id`,t2.`author_id` AS `author_id`,t2.`category_id` AS `category_id`,t2.`title` AS `title`,t2.`status` AS `status`,t2.`views` AS `views`,t2.`published_at` AS `published_at` FROM (SELECT t1.`id` AS `id`,t1.`author_id` AS `author_id`,t1.`category_id` AS `category_id`,t1.`title` AS `title`,t1.`status` AS `status`,t1.`views` AS `views`,t1.`published_at` AS `published_at` FROM (SELECT t.`id`,t.`author_id`,t.`category_id`,t.`title`,t.`status`,t.`views`,t.`published_at`,(ROW_NUMBER() OVER (PARTITION BY t.`author_id` ORDER BY t.`views` DESC)) AS `__row__` FROM `t_post` t WHERE t.`author_id` IN (?,?,?,?,?,?,?,?,?)) t1 WHERE t1.`__row__` >= ? AND t1.`__row__` <= ?) t2;
-- params: [1, 2, 3, 4, 5, 7, 8, 9, 10, 1, 1]

-- IncludeGroupSize：父键多时分批：IncludeGroupSize
SELECT `id`,`name`,`city` FROM `t_company` ORDER BY `id` ASC;
SELECT t.`id`,t.`name`,t.`age`,t.`company_id`,t.`department_id`,t.`manager_id`,t.`level`,t.`joined_at` FROM `t_user` t WHERE t.`company_id` IN (?,?);
-- params: [1, 2]
SELECT t.`id`,t.`name`,t.`age`,t.`company_id`,t.`department_id`,t.`manager_id`,t.`level`,t.`joined_at` FROM `t_user` t WHERE t.`company_id` IN (?,?);
-- params: [3, 4]

-- LoadInclude：已经查出来的实体再带出：LoadIncludeAsync
SELECT `id`,`author_id`,`category_id`,`title`,`status`,`views`,`published_at` FROM `t_post` WHERE `author_id` = ?;
-- params: [1]
SELECT `id`,`post_id`,`author_id`,`parent_id`,`body`,`likes` FROM `t_comment` WHERE `likes` >= ? AND `post_id` IN (?,?);
-- params: [5, 1, 2]

-- NestedProjection：投影里的嵌套集合：对一走 JOIN，对多每层一次查询
SELECT t.`name`,t2.`name` AS `company`,t.`id` AS `__relation__id` FROM `t_user` t LEFT JOIN `t_company` t2 ON t2.`id` = t.`company_id` WHERE EXISTS(SELECT 1 FROM `t_post` t1 WHERE t1.`author_id` = t.`id` LIMIT 1) ORDER BY t.`id` ASC LIMIT 3;
SELECT t.`title`,t.`views`,t.`author_id` AS `__relation__authorId` FROM `t_post` t WHERE t.`status` = ? AND t.`author_id` IN (?,?,?) ORDER BY t.`views` DESC;
-- params: ['published', 1, 2, 3]

-- AutoInclude：按 DTO 结构自动带出：Select<TDto>()
SELECT t.`name`,t.`company_id` AS `__relation__companyId`,t.`id` AS `__relation__id` FROM `t_user` t WHERE t.`company_id` = ? ORDER BY t.`id` ASC;
-- params: [1]
SELECT t.`name`,t.`city`,t.`id` AS `__relation__id` FROM `t_company` t WHERE t.`id` IN (?);
-- params: [1]
SELECT t2.`title` AS `title`,t2.`views` AS `views`,t2.`author_id` AS `__relation__authorId` FROM (SELECT t1.`id` AS `id`,t1.`author_id` AS `author_id`,t1.`category_id` AS `category_id`,t1.`title` AS `title`,t1.`status` AS `status`,t1.`views` AS `views`,t1.`published_at` AS `published_at` FROM (SELECT t.`id`,t.`author_id`,t.`category_id`,t.`title`,t.`status`,t.`views`,t.`published_at`,(ROW_NUMBER() OVER (PARTITION BY t.`author_id` ORDER BY t.`views` DESC)) AS `__row__` FROM `t_post` t WHERE t.`status` = ? AND t.`author_id` IN (?,?,?,?)) t1 WHERE t1.`__row__` >= ? AND t1.`__row__` <= ?) t2;
-- params: ['published', 1, 2, 7, 8, 1, 2]

-- DtoNavigateLimit：DTO 属性上的 [Navigate(OrderBy, Limit)]
SELECT t.`title`,t.`id` AS `__relation__id` FROM `t_post` t WHERE EXISTS(SELECT 1 FROM `t_comment` t1 WHERE t1.`post_id` = t.`id` LIMIT 1) ORDER BY t.`id` ASC;
SELECT t2.`body` AS `body`,t2.`likes` AS `likes`,t2.`post_id` AS `__relation__postId` FROM (SELECT t1.`id` AS `id`,t1.`post_id` AS `post_id`,t1.`author_id` AS `author_id`,t1.`parent_id` AS `parent_id`,t1.`body` AS `body`,t1.`likes` AS `likes` FROM (SELECT t.`id`,t.`post_id`,t.`author_id`,t.`parent_id`,t.`body`,t.`likes`,(ROW_NUMBER() OVER (PARTITION BY t.`post_id` ORDER BY t.`likes` DESC)) AS `__row__` FROM `t_comment` t WHERE t.`post_id` IN (?,?,?,?,?,?,?)) t1 WHERE t1.`__row__` >= ? AND t1.`__row__` <= ?) t2;
-- params: [1, 3, 4, 6, 7, 9, 11, 1, 2]

-- FillMany：按键填充：FillMany
SELECT t.`id`,t.`status` FROM `t_order` t WHERE t.`user_id` = ? ORDER BY t.`id` ASC;
-- params: [1]
SELECT `order_id`,`product_id`,`quantity`,`price` FROM `t_order_item` WHERE `order_id` IN (?,?);
-- params: [1, 2]

-- TreeCte：递归 CTE：某个节点和它的直接下级（LimitDeep = 1）
WITH RECURSIVE `as_tree_cte` AS ( (SELECT 0 AS `cte_deep`,t1.`id`,t1.`company_id`,t1.`parent_id`,t1.`name` FROM `t_department` t1 WHERE t1.`id` = ?)  UNION ALL  (SELECT t2.`cte_deep` + 1 AS `cte_deep`,t3.`id`,t3.`company_id`,t3.`parent_id`,t3.`name` FROM `as_tree_cte` t2 INNER JOIN `t_department` t3 ON t3.`parent_id` = t2.`id`) ) SELECT t.`name`,t.`parent_id` FROM `as_tree_cte` t WHERE t.`cte_deep` <= ? ORDER BY t.`id` ASC;
-- params: [1, 1]

-- TreeUp：往上找全部上级：Up = true
WITH RECURSIVE `as_tree_cte` AS ( (SELECT 0 AS `cte_deep`,t1.`id`,t1.`parent_id`,t1.`name` FROM `t_category` t1 WHERE t1.`name` = ?)  UNION ALL  (SELECT t2.`cte_deep` + 1 AS `cte_deep`,t3.`id`,t3.`parent_id`,t3.`name` FROM `as_tree_cte` t2 INNER JOIN `t_category` t3 ON t3.`id` = t2.`parent_id`) ) SELECT t.`name` FROM `as_tree_cte` t ORDER BY t.`id` ASC;
-- params: ['PostgreSQL']

-- ToTree：查出整张表再拼成树：ToTreeAsync
SELECT `id`,`parent_id`,`name` FROM `t_category`;

