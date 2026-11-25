using dev_peru.Application.Specifications.Base;
using DS.Platform.Domain.Entities.Identity;

namespace DS.Platform.Infrastructure.Specifications
{
    public class UserFilterSpecification : IAdmSpecification<AdmUser>
    {
        public UserFilterSpecification(string searchString)
        {
            if (!string.IsNullOrEmpty(searchString))
            {
                Criteria = p => p.FirstName.Contains(searchString) || p.LastName.Contains(searchString) || p.Email!.Contains(searchString) || p.PhoneNumber!.Contains(searchString) || p.UserName!.Contains(searchString);
            }
            else
            {
                Criteria = p => true;
            }
        }
    }
}