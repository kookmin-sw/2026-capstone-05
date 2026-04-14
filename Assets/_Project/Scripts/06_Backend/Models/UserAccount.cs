using System;

[Serializable]
public sealed class UserAccount
{
    public string Id = string.Empty;
    public string Username = string.Empty;
    public string Password = string.Empty;
    public DateTime CreatedAt;
}
