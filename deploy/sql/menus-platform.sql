-- =====================================================================================
-- DS.Platform — Menú de la consola de plataforma (un solo script)                2026-11-23
-- Mismo mecanismo que menus-all.sql del ERP:
--   • Crea o acomoda TODOS los menús: grupos (título de sección), desplegables (collapse) e ítems (pantallas).
--     Un ítem se reconoce por su Url; un grupo / desplegable por su tipo + título + padre. Nunca borra menús.
--   • SuperAdmin recibe siempre todos. Auditoría (/private/admin/audit-trails) es solo de SuperAdmin.
--     Los demás roles del seeder reciben su lista por defecto SOLO si todavía no tienen ningún menú: después manda lo que
--     se configure en Roles → Menús.
--   • Idempotente: correrlo dos veces no duplica nada.
-- Necesita las migraciones y que la API haya arrancado una vez (crea los roles).
--
-- Orden de los grupos: Principal 1 · Comercial 2 · ERP 3 (C4) · API SUNAT 4 · Administración 9 · Soporte 10.
-- Una pantalla nueva = una línea PERFORM en su grupo con el siguiente número de orden (y su Url en los roles que la ven).
--
-- Ejecutar como dueño de la base:  psql -d <base> -f menus-platform.sql
-- =====================================================================================
BEGIN;

CREATE TEMP TABLE IF NOT EXISTS erp_menu_touched (menu_id int PRIMARY KEY, action text NOT NULL) ON COMMIT DROP;
TRUNCATE erp_menu_touched;

-- Menús por defecto de un rol: solo si el rol existe y aún no tiene ninguno. p_urls NULL = todos los de este script;
-- p_exclude: Urls que el rol no recibe aunque p_urls sea NULL (las pantallas solo de SuperAdmin).
CREATE OR REPLACE FUNCTION pg_temp.erp_role_menus(p_role text, p_urls text[], p_exclude text[] DEFAULT NULL)
RETURNS int LANGUAGE plpgsql AS $f$
DECLARE
    v_role text;
    v_count int := 0;
BEGIN
    SELECT "Id" INTO v_role FROM "Identity"."Roles" WHERE "Name" = p_role;
    IF v_role IS NULL
       OR EXISTS (SELECT 1 FROM "MenuRoles" WHERE "RoleId" = v_role AND NOT "IsDeleted") THEN
        RETURN 0;
    END IF;
    INSERT INTO "MenuRoles" ("MenuId","RoleId","IsActive","IsDeleted","CreatedBy","CreatedOn")
    SELECT t.menu_id, v_role, true, false, 'System', NOW()
      FROM erp_menu_touched t
      JOIN "Menus" m ON m."Id" = t.menu_id
     WHERE (p_urls IS NULL OR m."Url" = ANY (p_urls))
       AND (m."Url" IS NULL OR p_exclude IS NULL OR NOT (m."Url" = ANY (p_exclude)));
    GET DIAGNOSTICS v_count = ROW_COUNT;
    RAISE NOTICE 'menus-platform: rol % recibe % menús por defecto.', p_role, v_count;
    RETURN v_count;
END
$f$;

-- Crea o acomoda un menú y devuelve su Id.
CREATE OR REPLACE FUNCTION pg_temp.erp_menu(p_parent int, p_type text, p_title text, p_icon text, p_url text, p_order int)
RETURNS int LANGUAGE plpgsql AS $f$
DECLARE
    v_id int;
BEGIN
    IF p_url IS NOT NULL THEN
        SELECT "Id" INTO v_id FROM "Menus" WHERE "Url" = p_url AND NOT "IsDeleted" ORDER BY "Id" LIMIT 1;
    ELSE
        SELECT "Id" INTO v_id FROM "Menus"
         WHERE "Type" = p_type AND "Title" = p_title AND "MenuParentId" IS NOT DISTINCT FROM p_parent AND NOT "IsDeleted"
         ORDER BY "Id" LIMIT 1;
    END IF;

    IF v_id IS NULL THEN
        INSERT INTO "Menus" ("Title","Type","Icon","Url","Order","MenuParentId","Hidden","ExactMatch","External","Target",
                             "Breadcrumbs","Disabled","IsActive","IsDeleted","CreatedBy","CreatedOn")
        VALUES (p_title, p_type, p_icon, p_url, p_order, p_parent, false, false, false, false, false, false, true, false, 'System', NOW())
        RETURNING "Id" INTO v_id;
        INSERT INTO erp_menu_touched VALUES (v_id, 'creado');
    ELSE
        UPDATE "Menus"
           SET "Title" = p_title, "Type" = p_type, "Icon" = p_icon, "Order" = p_order, "MenuParentId" = p_parent,
               "Hidden" = false, "IsActive" = true, "LastModifiedBy" = 'System', "LastModifiedOn" = NOW()
         WHERE "Id" = v_id
           AND ("Title" IS DISTINCT FROM p_title OR "Type" IS DISTINCT FROM p_type OR "Icon" IS DISTINCT FROM p_icon
                OR "Order" IS DISTINCT FROM p_order OR "MenuParentId" IS DISTINCT FROM p_parent OR "Hidden" OR NOT "IsActive");
        INSERT INTO erp_menu_touched VALUES (v_id, CASE WHEN FOUND THEN 'actualizado' ELSE 'sin cambios' END)
        ON CONFLICT (menu_id) DO NOTHING;
    END IF;
    RETURN v_id;
END
$f$;

DO $$
DECLARE
    g int;  -- grupo
    c int;  -- desplegable
    v_super_role text;
    staff text[] := ARRAY['/private/home', '/private/help'];
    commercial text[] := ARRAY['/private/billing/customers', '/private/billing/subscriptions', '/private/billing/payments',
                               '/private/billing/plans', '/private/billing/notifications'];
    integrations text[] := ARRAY['/private/api-sunat/issuers', '/private/api-sunat/remote-actions', '/private/api-sunat/portal', '/private/erp/instances', '/private/erp/deployments', '/private/erp/api-credentials'];
BEGIN
    SELECT "Id" INTO v_super_role FROM "Identity"."Roles" WHERE "Name" = 'SuperAdmin';
    IF v_super_role IS NULL THEN
        RAISE EXCEPTION 'Rol SuperAdmin no encontrado (arranque la API una vez para crear los roles); no se aplicó nada.';
    END IF;

    -- ---------------------------------------------------------------- 1 Principal
    g := pg_temp.erp_menu(NULL, 'group', 'Principal', NULL, NULL, 1);
    PERFORM pg_temp.erp_menu(g, 'item', 'Inicio', 'feather icon-home', '/private/home', 1);

    -- ---------------------------------------------------------------- 2 Comercial (clientes, planes, cobros)
    g := pg_temp.erp_menu(NULL, 'group', 'Comercial', 'feather icon-briefcase', NULL, 2);
    PERFORM pg_temp.erp_menu(g, 'item', 'Clientes', 'feather icon-briefcase', '/private/billing/customers', 1);
    PERFORM pg_temp.erp_menu(g, 'item', 'Suscripciones', 'feather icon-repeat', '/private/billing/subscriptions', 2);
    PERFORM pg_temp.erp_menu(g, 'item', 'Pagos', 'feather icon-dollar-sign', '/private/billing/payments', 3);
    PERFORM pg_temp.erp_menu(g, 'item', 'Planes', 'feather icon-layers', '/private/billing/plans', 4);
    PERFORM pg_temp.erp_menu(g, 'item', 'Avisos', 'feather icon-mail', '/private/billing/notifications', 5);

    -- ---------------------------------------------------------------- 3 ERP (fase C4)
    g := pg_temp.erp_menu(NULL, 'group', 'ERP', 'feather icon-server', NULL, 3);
    PERFORM pg_temp.erp_menu(g, 'item', 'Instancias', 'feather icon-hard-drive', '/private/erp/instances', 1);
    PERFORM pg_temp.erp_menu(g, 'item', 'Despliegues', 'feather icon-upload-cloud', '/private/erp/deployments', 2);
    PERFORM pg_temp.erp_menu(g, 'item', 'Credenciales al ERP', 'feather icon-key', '/private/erp/api-credentials', 3);

    -- ---------------------------------------------------------------- 4 API SUNAT (fase C3)
    g := pg_temp.erp_menu(NULL, 'group', 'API SUNAT', 'feather icon-send', NULL, 4);
    PERFORM pg_temp.erp_menu(g, 'item', 'Emisores', 'feather icon-file-text', '/private/api-sunat/issuers', 1);
    PERFORM pg_temp.erp_menu(g, 'item', 'Acciones remotas', 'feather icon-activity', '/private/api-sunat/remote-actions', 2);
    PERFORM pg_temp.erp_menu(g, 'item', 'Portal', 'feather icon-users', '/private/api-sunat/portal', 3);

    -- ---------------------------------------------------------------- 9 Administración
    g := pg_temp.erp_menu(NULL, 'group', 'Administración', 'feather icon-settings', NULL, 9);
    c := pg_temp.erp_menu(g, 'collapse', 'Seguridad', 'feather icon-shield', NULL, 1);
    PERFORM pg_temp.erp_menu(c, 'item', 'Usuarios', 'feather icon-users', '/private/admin/users', 1);
    PERFORM pg_temp.erp_menu(c, 'item', 'Roles', 'feather icon-lock', '/private/admin/roles', 2);
    PERFORM pg_temp.erp_menu(c, 'item', 'Menús', 'feather icon-menu', '/private/admin/menus', 3);
    -- Solo SuperAdmin (lo recibe por la asignación de abajo; ningún otro rol lo tiene por defecto).
    PERFORM pg_temp.erp_menu(c, 'item', 'Auditoría', 'feather icon-eye', '/private/admin/audit-trails', 4);

    -- ---------------------------------------------------------------- 10 Soporte (siempre el último)
    g := pg_temp.erp_menu(NULL, 'group', 'Soporte', 'feather icon-life-buoy', NULL, 10);
    PERFORM pg_temp.erp_menu(g, 'item', 'Ayuda', 'feather icon-help-circle', '/private/help', 1);

    -- ---------------------------------------------------------------- SuperAdmin ve todo el menú (siempre)
    INSERT INTO "MenuRoles" ("MenuId","RoleId","IsActive","IsDeleted","CreatedBy","CreatedOn")
    SELECT t.menu_id, v_super_role, true, false, 'System', NOW()
      FROM erp_menu_touched t
     WHERE NOT EXISTS (SELECT 1 FROM "MenuRoles" mr WHERE mr."MenuId" = t.menu_id AND mr."RoleId" = v_super_role AND NOT mr."IsDeleted");

    -- ---------------------------------------------------------------- Menús por defecto de los demás roles (solo la primera vez)
    -- Coinciden con los permisos por defecto de SystemRoleCatalog (DS.Platform.Shared/Constants/Role).
    PERFORM pg_temp.erp_role_menus('Administrador', NULL, ARRAY['/private/admin/audit-trails']);
    PERFORM pg_temp.erp_role_menus('Cobranzas', staff || commercial || integrations);
    PERFORM pg_temp.erp_role_menus('Soporte', staff || commercial || integrations);
    PERFORM pg_temp.erp_role_menus('Lectura', staff || commercial || integrations || ARRAY['/private/admin/users']);
    PERFORM pg_temp.erp_role_menus('Básico', staff);

    RAISE NOTICE 'menus-platform: % menús (% creados, % actualizados, % sin cambios).',
        (SELECT count(*) FROM erp_menu_touched),
        (SELECT count(*) FROM erp_menu_touched WHERE action = 'creado'),
        (SELECT count(*) FROM erp_menu_touched WHERE action = 'actualizado'),
        (SELECT count(*) FROM erp_menu_touched WHERE action = 'sin cambios');
END $$;

-- Resumen (también para clientes SQL que no muestran los NOTICE).
SELECT action AS resultado, count(*) AS menus FROM erp_menu_touched GROUP BY action ORDER BY action;

COMMIT;
