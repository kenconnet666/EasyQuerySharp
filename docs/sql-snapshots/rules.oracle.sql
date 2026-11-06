-- LogicDeleteQuery：逻辑删除：查询自动加上未删除的条件
SELECT t."title" FROM "t_doc" t WHERE t."deleted" = ? ORDER BY t."id" ASC;
-- params: [false]

-- LogicDeleteDisabled：查出已删除的行：DisableLogicDelete
SELECT COUNT(*) FROM "t_doc";

-- HardDelete：按条件删除也改写成 UPDATE；要真删时用 Hard()
UPDATE "t_doc" SET "deleted" = ? WHERE "deleted" = ? AND "title" = ?;
-- params: [true, false, '接入指南']
DELETE FROM "t_doc" WHERE "title" = ?;
-- params: ['接入指南']

-- VersionUpdate：乐观锁：按实体更新时带上版本号，版本号加一；影响 0 行时抛 ConcurrencyException
SELECT "id","title","deleted","version","created_by","updated_at" FROM "t_doc" WHERE "deleted" = ? AND "id" = ?;
-- params: [false, 1]
UPDATE "t_doc" SET "title" = ?,"updated_at" = ?,"version" = ? WHERE "deleted" = ? AND "version" = ? AND "id" = ?;
-- params: ['接入指南（第二版）', '2026-11-01 08:00:00', 2, false, 1, 1]

-- VersionCondition：按条件更新要说明版本：WithVersion 或 IgnoreVersion
UPDATE "t_doc" SET "title" = ?,"updated_at" = ?,"version" = ? WHERE "deleted" = ? AND "version" = ? AND "id" = ?;
-- params: ['改个标题', '2026-11-01 08:00:00', 2, false, 1, 1]
UPDATE "t_doc" SET "title" = ?,"updated_at" = ? WHERE "deleted" = ? AND "id" = ?;
-- params: ['管理端修正', '2026-11-01 08:00:00', false, 1]

-- TenantFilter：查询过滤：工单只看 1 号租户，更新、删除同样带上
DELETE FROM "t_ticket" WHERE "tenant_id" = ? AND "title" = ?;
-- params: [1, '登录不上']
SELECT t."title" FROM "t_ticket" t WHERE t."tenant_id" = ? ORDER BY t."id" ASC;
-- params: [1]

-- DisableInterceptor：按名字关掉某个拦截器
SELECT COUNT(*) FROM "t_ticket";

-- FillBeforeWrite：写入前填值：插入时补租户，按条件更新时追加 SET
INSERT INTO "t_ticket" ("tenant_id","title","contact") VALUES (?,?,?);
-- params: [1, '新工单', 'enc:90000000831']
UPDATE "t_ticket" SET "title" = ?,"touched_at" = ? WHERE "tenant_id" = ? AND "id" = ?;
-- params: ['发票抬头写错了（已处理）', '2026-11-01 08:00:00', 1, 1]

-- EncryptedColumn：加密列：写入加密、读出解密，等值条件按密文比较
SELECT t."title" FROM "t_ticket" t WHERE t."tenant_id" = ? AND t."contact" = ? ORDER BY t."id" ASC;
-- params: [1, 'enc:10000000831']

