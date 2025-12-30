using HotelApp.Models;

namespace HotelApp.Services;

public class CurrentUserService
{
    private User? _user;
    public User? User
    {
        get => _user;
        set
        {
            _user = value;
            OnChanged?.Invoke(_user);
        }
    }

    public bool IsAuthenticated => User != null;

    public event Action<User?>? OnChanged;
}