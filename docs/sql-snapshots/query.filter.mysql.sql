-- FilterBy：按条件对象筛选：没填的属性不加条件，导航路径写成隐式 JOIN
SELECT t.`title`,t.`status`,t.`views`,t1.`name` AS `author` FROM `t_post` t LEFT JOIN `t_user` t1 ON t1.`id` = t.`author_id` WHERE t1.`name` LIKE ? AND t.`views` >= ? ORDER BY t.`id` ASC;
-- params: ['%林%', 500]

-- TableRequest：列表页请求：表头筛选、关键字筛选、排序只认结果里的列
SELECT COUNT(*) FROM `t_post` t WHERE t.`status` IN (?) AND t.`title` LIKE ?;
-- params: ['published', '%的%']
SELECT t.`title`,t.`status`,t.`views`,t1.`name` AS `author` FROM `t_post` t LEFT JOIN `t_user` t1 ON t1.`id` = t.`author_id` WHERE t.`status` IN (?) AND t.`title` LIKE ? ORDER BY t.`views` DESC LIMIT 3;
-- params: ['published', '%的%']

