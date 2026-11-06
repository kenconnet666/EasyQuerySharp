-- CreateTables：建表：注释、索引、外键一起建（外键排在全部建表之后）
CREATE TABLE IF NOT EXISTS `t_note` ( 
`id` BIGINT(20) NOT NULL  AUTO_INCREMENT,
`title` VARCHAR(255) NULL  COMMENT '标题',
`author_id` BIGINT(20) NOT NULL ,
`created_at` DATETIME(3) NOT NULL , 
 PRIMARY KEY (`id`)
) Engine=InnoDB COMMENT='笔记';
CREATE INDEX t_note_author_id_created_at_idx ON `t_note` (`author_id` ASC,`created_at` DESC);
ALTER TABLE `t_note` ADD CONSTRAINT t_note_t_user_id_fk FOREIGN KEY (`author_id`) REFERENCES `t_user` (`id`) ON DELETE CASCADE ;
SELECT COUNT(*) FROM `t_note`;

-- DbDefaults：库端默认值：建表时写上 DEFAULT；插入时为 null 的列不写，由库填上
CREATE TABLE IF NOT EXISTS `t_draft` ( 
`id` BIGINT(20) NOT NULL  AUTO_INCREMENT,
`title` VARCHAR(255) NULL ,
`status` VARCHAR(255) NULL  DEFAULT 'draft',
`priority` INT(11) NULL  DEFAULT 0, 
 PRIMARY KEY (`id`)
) Engine=InnoDB;
INSERT INTO `t_draft` (`title`) VALUES (?);
-- params: ['周报']
SELECT `id`,`title`,`status`,`priority` FROM `t_draft` LIMIT 1;

-- SyncTables：同步：先看要执行的语句，再执行；只增不删——改表名、改列名、加列、补索引
SELECT TABLE_NAME AS table_name, TABLE_COMMENT AS comment FROM information_schema.TABLES WHERE TABLE_SCHEMA = DATABASE() AND TABLE_TYPE = 'BASE TABLE' ORDER BY TABLE_NAME;
SELECT TABLE_NAME AS table_name, COLUMN_NAME AS column_name, COLUMN_TYPE AS column_type, IS_NULLABLE AS is_nullable, COLUMN_DEFAULT AS column_default, COLUMN_COMMENT AS comment, EXTRA LIKE '%auto_increment%' AS is_generated, COLUMN_KEY = 'PRI' AS primary_key FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = DATABASE() ORDER BY TABLE_NAME, ORDINAL_POSITION;
SELECT TABLE_NAME AS table_name, INDEX_NAME AS index_name, NON_UNIQUE = 0 AS is_unique, COLUMN_NAME AS column_name FROM information_schema.STATISTICS WHERE TABLE_SCHEMA = DATABASE() AND INDEX_NAME <> 'PRIMARY' ORDER BY TABLE_NAME, INDEX_NAME, SEQ_IN_INDEX;
SELECT TABLE_NAME AS table_name, CONSTRAINT_NAME AS constraint_name, COLUMN_NAME AS column_name, REFERENCED_TABLE_NAME AS referenced_table, REFERENCED_COLUMN_NAME AS referenced_column FROM information_schema.KEY_COLUMN_USAGE WHERE TABLE_SCHEMA = DATABASE() AND REFERENCED_TABLE_NAME IS NOT NULL ORDER BY TABLE_NAME, CONSTRAINT_NAME, ORDINAL_POSITION;
ALTER TABLE `t_tag` RENAME TO `t_label`;
ALTER TABLE `t_label` CHANGE `name` `title` VARCHAR(30) NULL;
ALTER TABLE `t_label` ADD `color` VARCHAR(255) NULL COMMENT '颜色';
CREATE UNIQUE INDEX t_label_title_uidx ON `t_label` (`title` ASC);

-- ReadTables：读库结构：列、主键、索引、外键
SELECT TABLE_NAME AS table_name, TABLE_COMMENT AS comment FROM information_schema.TABLES WHERE TABLE_SCHEMA = DATABASE() AND TABLE_TYPE = 'BASE TABLE' ORDER BY TABLE_NAME;
SELECT TABLE_NAME AS table_name, COLUMN_NAME AS column_name, COLUMN_TYPE AS column_type, IS_NULLABLE AS is_nullable, COLUMN_DEFAULT AS column_default, COLUMN_COMMENT AS comment, EXTRA LIKE '%auto_increment%' AS is_generated, COLUMN_KEY = 'PRI' AS primary_key FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = DATABASE() ORDER BY TABLE_NAME, ORDINAL_POSITION;
SELECT TABLE_NAME AS table_name, INDEX_NAME AS index_name, NON_UNIQUE = 0 AS is_unique, COLUMN_NAME AS column_name FROM information_schema.STATISTICS WHERE TABLE_SCHEMA = DATABASE() AND INDEX_NAME <> 'PRIMARY' ORDER BY TABLE_NAME, INDEX_NAME, SEQ_IN_INDEX;
SELECT TABLE_NAME AS table_name, CONSTRAINT_NAME AS constraint_name, COLUMN_NAME AS column_name, REFERENCED_TABLE_NAME AS referenced_table, REFERENCED_COLUMN_NAME AS referenced_column FROM information_schema.KEY_COLUMN_USAGE WHERE TABLE_SCHEMA = DATABASE() AND REFERENCED_TABLE_NAME IS NOT NULL ORDER BY TABLE_NAME, CONSTRAINT_NAME, ORDINAL_POSITION;

