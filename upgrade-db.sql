-- =============================================================================
-- Aquarius 数据库升级脚本
-- 自 Commit 7786094 (style: 楼中楼回复的管理员标识复选框单独一行) 以来的所有变更
-- 适用于 SQLite，执行前请备份数据库
-- =============================================================================

-- 1. 新建 SiteSettings 表（站点设置）
-- -----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS "SiteSettings" (
    "Id"         INTEGER NOT NULL CONSTRAINT "PK_SiteSettings" PRIMARY KEY AUTOINCREMENT,
    "SiteName"   TEXT    NOT NULL DEFAULT 'Aquarius',
    "Copyright"  TEXT    NOT NULL DEFAULT ''
);

-- 插入默认设置（仅当表为空时）
INSERT INTO "SiteSettings" ("Id", "SiteName", "Copyright")
SELECT 1, 'Aquarius', ''
WHERE NOT EXISTS (SELECT 1 FROM "SiteSettings");


-- 2. Comment 表：添加瓶主标识列
-- -----------------------------------------------------------------------------
ALTER TABLE "Comments" ADD COLUMN "IsBottleOwnerBadge" INTEGER NOT NULL DEFAULT 0;


-- 3. User 表：添加角色、封禁相关列
-- -----------------------------------------------------------------------------
ALTER TABLE "Users" ADD COLUMN "Role"          TEXT    NOT NULL DEFAULT 'user';
ALTER TABLE "Users" ADD COLUMN "IsBanned"      INTEGER NOT NULL DEFAULT 0;
ALTER TABLE "Users" ADD COLUMN "BanReason"     TEXT    NULL;
ALTER TABLE "Users" ADD COLUMN "BannedUntil"   TEXT    NULL;

-- 回填现有管理员：IsAdmin=1 的用户设置 Role='admin'
UPDATE "Users" SET "Role" = 'admin' WHERE "IsAdmin" = 1;


-- 4. Bottle 表：添加举报目标列
-- -----------------------------------------------------------------------------
ALTER TABLE "Bottles" ADD COLUMN "ReportedBottleId" INTEGER NULL;


-- =============================================================================
-- 升级完成。验证：
--   SELECT * FROM SiteSettings;
--   PRAGMA table_info(Comments);   -- 应有 IsBottleOwnerBadge
--   PRAGMA table_info(Users);      -- 应有 Role, IsBanned, BanReason, BannedUntil
--   PRAGMA table_info(Bottles);    -- 应有 ReportedBottleId
-- =============================================================================
