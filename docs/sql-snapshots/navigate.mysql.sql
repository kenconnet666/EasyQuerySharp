-- LeftJoin：显式 JOIN：后面的 lambda 依次收到每张表
SELECT t.`title`,t1.`name` AS `author` FROM `t_post` t LEFT JOIN `t_user` t1 ON t.`author_id` = t1.`id` WHERE t1.`age` >= ? ORDER BY t.`id` ASC;
-- params: [30]

-- JoinSubQuery：JOIN 一个子查询
SELECT t.`title`,t3.`count` AS `comments` FROM `t_post` t INNER JOIN (SELECT t1.`post_id` AS `post_id`,COUNT(*) AS `count` FROM `t_comment` t1 GROUP BY t1.`post_id`) t3 ON t.`id` = t3.`post_id` ORDER BY t.`id` ASC;

-- QuerySyntax：LINQ 查询语法：join … equals …
SELECT t2.`title`,t2.`name` AS `author` FROM (SELECT t.`id`,t.`author_id`,t.`category_id`,t.`title`,t.`status`,t.`views`,t.`published_at`,t1.`id` AS `id1`,t1.`name`,t1.`age`,t1.`company_id`,t1.`department_id`,t1.`manager_id`,t1.`level`,t1.`joined_at` FROM `t_post` t INNER JOIN `t_user` t1 ON t.`author_id` = t1.`id`) t2 WHERE t2.`views` >= ?;
-- params: [2000]

-- RequiredToOne：Required 的对一导航用 INNER JOIN
SELECT t1.`name`,t.`bio` FROM `t_profile` t INNER JOIN `t_user` t1 ON t1.`id` = t.`user_id` ORDER BY t.`user_id` ASC LIMIT 3;

-- SelfReference：自关联与多级导航：每级一个 LEFT JOIN
SELECT t.`name`,t1.`name` AS `manager`,t2.`name` AS `managers_manager` FROM `t_user` t LEFT JOIN `t_user` t1 ON t1.`id` = t.`manager_id` LEFT JOIN `t_user` t2 ON t2.`id` = t1.`manager_id` WHERE t.`manager_id` IS NOT NULL ORDER BY t.`id` ASC;

-- AnyNone：Any / !Any：EXISTS 与 NOT EXISTS
SELECT t.`name` FROM `t_user` t WHERE EXISTS(SELECT 1 FROM `t_post` t1 WHERE t1.`author_id` = t.`id` AND t1.`views` >= ? LIMIT 1) AND NOT (EXISTS(SELECT 1 FROM `t_comment` t2 WHERE t2.`author_id` = t.`id` LIMIT 1));
-- params: [2000]

-- CollectionAggregates：集合上的聚合：Count / Sum / Max 是标量子查询
SELECT t.`name`,(SELECT COUNT(*) FROM `t_post` t2 WHERE t2.`author_id` = t.`id`) AS `posts`,IFNULL((SELECT SUM(t3.`views`) FROM `t_post` t3 WHERE t3.`author_id` = t.`id`),0) AS `views`,(SELECT MAX(t4.`views`) FROM `t_post` t4 WHERE t4.`author_id` = t.`id`) AS `best` FROM `t_user` t WHERE EXISTS(SELECT 1 FROM `t_post` t1 WHERE t1.`author_id` = t.`id` LIMIT 1) ORDER BY t.`id` ASC;

-- ManyToMany：多对多：经中间表的 EXISTS
SELECT t.`title` FROM `t_post` t WHERE EXISTS(SELECT 1 FROM `t_tag` t1 WHERE EXISTS(SELECT 1 FROM `t_post_tag` t2 WHERE t2.`tag_id` = t1.`id` AND t2.`post_id` = t.`id` LIMIT 1) AND t1.`name` = ? LIMIT 1) ORDER BY t.`id` ASC;
-- params: ['架构']

-- ExtraFilter：导航上的固定条件：[Navigate(ExtraFilter = …)]
SELECT t.`name`,(SELECT COUNT(*) FROM `t_post` t2 WHERE (t2.`author_id` = t.`id` AND t2.`views` >= ?)) AS `hot` FROM `t_user` t WHERE EXISTS(SELECT 1 FROM `t_post` t1 WHERE (t1.`author_id` = t.`id` AND t1.`views` >= ?) LIMIT 1) ORDER BY t.`id` ASC;
-- params: [1000, 1000]

-- FilterNavigation：这条查询里给导航加条件：FilterNavigation
SELECT t.`name`,(SELECT COUNT(*) FROM `t_post` t2 WHERE t2.`author_id` = t.`id` AND t2.`status` = ?) AS `hot` FROM `t_user` t WHERE EXISTS(SELECT 1 FROM `t_post` t1 WHERE t1.`author_id` = t.`id` AND t1.`status` = ? LIMIT 1) ORDER BY t.`id` ASC;
-- params: ['published', 'published']

-- StringJoin：集合导航上的字符串聚合
SELECT CONCAT(t.`title`,?,(SELECT GROUP_CONCAT(t1.`name` ORDER BY t1.`id` ASC SEPARATOR ?) FROM `t_tag` t1 WHERE EXISTS(SELECT 1 FROM `t_post_tag` t2 WHERE t2.`tag_id` = t1.`id` AND t2.`post_id` = t.`id` LIMIT 1))) FROM `t_post` t WHERE t.`id` <= ? ORDER BY t.`id` ASC;
-- params: ['：', '、', 4]

-- NavigationLogicDelete：目标实体有逻辑删除：导航上关掉 DisableLogicDelete
SELECT t.`name`,(SELECT COUNT(*) FROM `t_doc` t1 WHERE t1.`created_by` = t.`name`) AS `hot` FROM `t_user` t WHERE t.`id` <= ? ORDER BY t.`id` ASC;
-- params: [2]

-- GroupJoin：同一导航上的几个聚合合并成一次分组 JOIN：GroupJoin
SELECT t.`name`,IFNULL(t2.`__count3__`,0) AS `posts`,IFNULL(t2.`__sum4__`,0) AS `views`,t2.`__max5__` AS `best` FROM `t_user` t LEFT JOIN (SELECT t1.`author_id` AS `__group_key1__`,(COUNT(*) > 0) AS `__any2__`,COUNT(*) AS `__count3__`,SUM(t1.`views`) AS `__sum4__`,MAX(t1.`views`) AS `__max5__` FROM `t_post` t1 GROUP BY t1.`author_id`) t2 ON t2.`__group_key1__` = t.`id` WHERE IFNULL(t2.`__any2__`,?) = ? ORDER BY t.`id` ASC;
-- params: [false, true]

-- GroupJoinAll：所有集合导航都合并：GroupJoinAll
SELECT t.`name`,IFNULL(t2.`__count2__`,0) AS `posts`,IFNULL(t4.`__count2__`,0) AS `comments` FROM `t_user` t LEFT JOIN (SELECT t1.`author_id` AS `__group_key1__`,COUNT(*) AS `__count2__` FROM `t_post` t1 GROUP BY t1.`author_id`) t2 ON t2.`__group_key1__` = t.`id` LEFT JOIN (SELECT t3.`author_id` AS `__group_key1__`,COUNT(*) AS `__count2__` FROM `t_comment` t3 GROUP BY t3.`author_id`) t4 ON t4.`__group_key1__` = t.`id` WHERE t.`id` <= ? ORDER BY t.`id` ASC;
-- params: [3]

-- Elements：集合里的第几个：First() / ElementAt(n)
SELECT t.`name`,t4.`title` AS `top`,t7.`title` AS `second` FROM `t_user` t LEFT JOIN (SELECT t2.`id`,t2.`author_id`,t2.`category_id`,t2.`title`,t2.`status`,t2.`views`,t2.`published_at`,(ROW_NUMBER() OVER (PARTITION BY t2.`author_id` ORDER BY t2.`views` DESC)) AS `__row__` FROM `t_post` t2) t4 ON (t4.`author_id` = t.`id` AND t4.`__row__` = ?) LEFT JOIN (SELECT t5.`id`,t5.`author_id`,t5.`category_id`,t5.`title`,t5.`status`,t5.`views`,t5.`published_at`,(ROW_NUMBER() OVER (PARTITION BY t5.`author_id` ORDER BY t5.`views` DESC)) AS `__row__` FROM `t_post` t5) t7 ON (t7.`author_id` = t.`id` AND t7.`__row__` = ?) WHERE EXISTS(SELECT 1 FROM `t_post` t1 WHERE t1.`author_id` = t.`id` LIMIT 1) ORDER BY t.`id` ASC;
-- params: [1, 2]

-- FirstInWhere：条件里用第一个元素
SELECT t.`name` FROM `t_user` t LEFT JOIN (SELECT t1.`id`,t1.`author_id`,t1.`category_id`,t1.`title`,t1.`status`,t1.`views`,t1.`published_at`,(ROW_NUMBER() OVER (PARTITION BY t1.`author_id` ORDER BY t1.`views` DESC)) AS `__row__` FROM `t_post` t1) t3 ON (t3.`author_id` = t.`id` AND t3.`__row__` = ?) WHERE t3.`views` >= ?;
-- params: [1, 2000]

-- RankedToOne：声明了条数的对一导航：按排序取那一行
SELECT t.`name`,t3.`title` AS `top`,t3.`views` FROM `t_user` t LEFT JOIN (SELECT t1.`id`,t1.`author_id`,t1.`category_id`,t1.`title`,t1.`status`,t1.`views`,t1.`published_at`,(ROW_NUMBER() OVER (PARTITION BY t1.`author_id` ORDER BY t1.`views` DESC)) AS `__row__` FROM `t_post` t1) t3 ON (t3.`author_id` = t.`id` AND t3.`__row__` = ?) WHERE t3.`views` >= ? ORDER BY t.`id` ASC;
-- params: [1, 1000]

-- DirectMapping：直达导航：[Navigate(DirectMapping = …)]
SELECT t.`title`,t2.`name` AS `company` FROM `t_post` t LEFT JOIN `t_user` t1 ON t1.`id` = t.`author_id` LEFT JOIN `t_company` t2 ON t2.`id` = t1.`company_id` WHERE t2.`city` = ? ORDER BY t.`id` ASC;
-- params: ['上海']

