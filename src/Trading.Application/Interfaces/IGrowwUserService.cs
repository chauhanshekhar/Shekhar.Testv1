using Trading.Application.Models;

namespace Trading.Application.Interfaces;

public interface IGrowwUserService
{
    Task<UserDetailPayload?> GetUserDetailAsync();
}
