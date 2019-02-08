using Microsoft.EntityFrameworkCore;

namespace Recipe.NetCore.Base.Interface
{
    public interface IRequestInfo
    {
        long UserId { get; }

        string UserName { get; }

        string DeviceId { get; }

        string Role { get; }

        long? PracticeId { get; }

        bool AllPractices { get; }

        DbContext Context { get; }
    }
}
