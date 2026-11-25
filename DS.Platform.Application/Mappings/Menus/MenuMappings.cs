using System.Linq.Expressions;
using DS.Platform.Application.DTOs.Menu.Response;
using DS.Platform.Application.Features.Menus.Commands.Create;
using DS.Platform.Application.Features.Menus.Commands.Update;
using DS.Platform.Domain.Entities.Menus;

namespace DS.Platform.Application.Mappings.Menus
{
    /// <summary>Mapeo explícito de menús (sin AutoMapper).</summary>
    public static class MenuMappings
    {
        public static readonly Expression<Func<Menu, MenuResponseDto>> ToResponse = m => new MenuResponseDto
        {
            Id = m.Id,
            Title = m.Title,
            Type = m.Type,
            Translate = m.Translate,
            Icon = m.Icon,
            Hidden = m.Hidden,
            Url = m.Url,
            Classes = m.Classes,
            ExactMatch = m.ExactMatch,
            External = m.External,
            Target = m.Target,
            Breadcrumbs = m.Breadcrumbs,
            BadgeTitle = m.BadgeTitle,
            BadgeType = m.BadgeType,
            Disabled = m.Disabled,
            Order = m.Order,
            MenuParentId = m.MenuParentId,
            IsActive = m.IsActive,
            StateName = m.IsActive ? "Activo" : "Inactivo"
        };

        private static readonly Func<Menu, MenuResponseDto> toResponse = ToResponse.Compile();

        public static MenuResponseDto ToResponseDto(this Menu menu) => toResponse(menu);

        public static Menu ToEntity(this CreateMenuCommand c) => new()
        {
            Title = c.Title,
            Type = c.Type,
            Translate = c.Translate,
            Icon = c.Icon,
            Hidden = c.Hidden,
            Url = c.Url,
            Classes = c.Classes,
            ExactMatch = c.ExactMatch,
            External = c.External,
            Target = c.Target,
            Breadcrumbs = c.Breadcrumbs,
            BadgeTitle = c.BadgeTitle,
            BadgeType = c.BadgeType,
            Disabled = c.Disabled,
            Order = c.Order,
            MenuParentId = c.MenuParentId
        };

        /// <summary>Copia los datos editables del menú (no toca id, estado ni auditoría).</summary>
        public static void ApplyTo(this UpdateMenuCommand c, Menu menu)
        {
            menu.Title = c.Title;
            menu.Type = c.Type;
            menu.Translate = c.Translate;
            menu.Icon = c.Icon;
            menu.Hidden = c.Hidden;
            menu.Url = c.Url;
            menu.Classes = c.Classes;
            menu.ExactMatch = c.ExactMatch;
            menu.External = c.External;
            menu.Target = c.Target;
            menu.Breadcrumbs = c.Breadcrumbs;
            menu.BadgeTitle = c.BadgeTitle;
            menu.BadgeType = c.BadgeType;
            menu.Disabled = c.Disabled;
            menu.Order = c.Order;
            menu.MenuParentId = c.MenuParentId;
        }
    }
}
