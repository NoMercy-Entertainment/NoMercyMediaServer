using NoMercy.Database.Models.Users;

namespace NoMercy.Setup.Auth;

public interface IUserProvisioningService
{
    Task ProvisionOwner(User user);
}
