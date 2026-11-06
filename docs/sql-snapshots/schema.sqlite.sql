-- CreateTables：建表：注释、索引、外键一起建（外键排在全部建表之后）
CREATE TABLE IF NOT EXISTS "t_note" ( 
"id" INTEGER PRIMARY KEY AUTOINCREMENT,
"title" VARCHAR(255) NULL ,
"author_id" integer NOT NULL ,
"created_at" TEXT NOT NULL 
, FOREIGN KEY ("author_id") REFERENCES "t_user"("id") ON DELETE CASCADE 
);
CREATE INDEX t_note_author_id_created_at_idx ON "t_note" ("author_id" ASC,"created_at" DESC);
SELECT COUNT(*) FROM "t_note";

-- DbDefaults：库端默认值：建表时写上 DEFAULT；插入时为 null 的列不写，由库填上
CREATE TABLE IF NOT EXISTS "t_draft" ( 
"id" INTEGER PRIMARY KEY AUTOINCREMENT,
"title" VARCHAR(255) NULL ,
"status" VARCHAR(255) NULL  DEFAULT 'draft',
"priority" integer NULL  DEFAULT 0
);
INSERT INTO "t_draft" ("title") VALUES (?);
-- params: ['周报']
SELECT "id","title","status","priority" FROM "t_draft" LIMIT 1;

-- SyncTables：同步：先看要执行的语句，再执行；只增不删——改表名、改列名、加列、补索引
SELECT name AS table_name, NULL AS comment FROM sqlite_master WHERE type = 'table' AND name NOT LIKE 'sqlite_%' ORDER BY name;
SELECT m.name AS table_name, p.name AS column_name, p.type AS column_type, p."notnull" = 0 AND p.pk = 0 AS is_nullable, p.dflt_value AS column_default, NULL AS comment, p.pk = 1 AND m.sql LIKE '%AUTOINCREMENT%' AS is_generated, p.pk > 0 AS primary_key FROM sqlite_master m JOIN pragma_table_xinfo(m.name) p WHERE m.type = 'table' AND m.name NOT LIKE 'sqlite_%' AND p.hidden <> 1 ORDER BY m.name, p.cid;
SELECT m.name AS table_name, l.name AS index_name, l."unique" AS is_unique, i.name AS column_name FROM sqlite_master m JOIN pragma_index_list(m.name) l JOIN pragma_index_info(l.name) i WHERE m.type = 'table' AND m.name NOT LIKE 'sqlite_%' AND l.origin <> 'pk' ORDER BY m.name, l.name, i.seqno;
SELECT m.name AS table_name, 'fk_' || m.name || '_' || f.id AS constraint_name, f."from" AS column_name, f."table" AS referenced_table, f."to" AS referenced_column FROM sqlite_master m JOIN pragma_foreign_key_list(m.name) f WHERE m.type = 'table' AND m.name NOT LIKE 'sqlite_%' ORDER BY m.name, f.id, f.seq;
ALTER TABLE "t_tag" RENAME TO "t_label";
ALTER TABLE "t_label" RENAME COLUMN "name" TO "title";
ALTER TABLE "t_label" ADD COLUMN "color" VARCHAR(255) NULL;
CREATE UNIQUE INDEX t_label_title_uidx ON "t_label" ("title" ASC);

-- ReadTables：读库结构：列、主键、索引、外键
SELECT name AS table_name, NULL AS comment FROM sqlite_master WHERE type = 'table' AND name NOT LIKE 'sqlite_%' ORDER BY name;
SELECT m.name AS table_name, p.name AS column_name, p.type AS column_type, p."notnull" = 0 AND p.pk = 0 AS is_nullable, p.dflt_value AS column_default, NULL AS comment, p.pk = 1 AND m.sql LIKE '%AUTOINCREMENT%' AS is_generated, p.pk > 0 AS primary_key FROM sqlite_master m JOIN pragma_table_xinfo(m.name) p WHERE m.type = 'table' AND m.name NOT LIKE 'sqlite_%' AND p.hidden <> 1 ORDER BY m.name, p.cid;
SELECT m.name AS table_name, l.name AS index_name, l."unique" AS is_unique, i.name AS column_name FROM sqlite_master m JOIN pragma_index_list(m.name) l JOIN pragma_index_info(l.name) i WHERE m.type = 'table' AND m.name NOT LIKE 'sqlite_%' AND l.origin <> 'pk' ORDER BY m.name, l.name, i.seqno;
SELECT m.name AS table_name, 'fk_' || m.name || '_' || f.id AS constraint_name, f."from" AS column_name, f."table" AS referenced_table, f."to" AS referenced_column FROM sqlite_master m JOIN pragma_foreign_key_list(m.name) f WHERE m.type = 'table' AND m.name NOT LIKE 'sqlite_%' ORDER BY m.name, f.id, f.seq;

