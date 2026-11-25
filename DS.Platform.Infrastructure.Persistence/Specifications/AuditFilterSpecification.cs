using dev_peru.Application.Specifications.Base;
using DS.Platform.Domain.Entities.Audit;

namespace DS.Platform.Infrastructure.Specifications
{
    public class AuditFilterSpecification : IAdmSpecification<Audit>
    {
        public AuditFilterSpecification(string userId, string searchString, bool searchInOldValues, bool searchInNewValues)
        {
            if (!string.IsNullOrEmpty(searchString))
            {
                Criteria = p => (p.TableName!.Contains(searchString) || searchInOldValues && p.OldValues!.Contains(searchString) || searchInNewValues && p.NewValues!.Contains(searchString)) && p.UserId == userId;
            }
            else
            {
                Criteria = p => p.UserId == userId;
            }
        }
    }
}