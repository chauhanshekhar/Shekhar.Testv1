namespace Trading.Application.Services;

public interface IJwtService
{
    string GenerateToken(string email);
}
