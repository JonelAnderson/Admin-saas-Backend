using DS.Platform.Domain.Entities.Identity;
using DS.Platform.Infrastructure.Persistence.Contexts;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DS.Platform.Infrastructure.Persistence.Extensions
{
    public static class UserExtensions
    {
        public static async Task<List<AdmUser>> LoadRecursively(this IQueryable<AdmUser> query, ApplicationDbContext context)
        {
            var result = await query.ToListAsync();

            foreach (var user in result)
            {
                await context.Entry(user).Collection(u => u.Subordinates).LoadAsync();
                var subordinates = await LoadRecursively(context.Users.Where(u => u.UserId == user.Id), context);
                user.Subordinates = subordinates;
            }

            return result;
        }
    }
}
