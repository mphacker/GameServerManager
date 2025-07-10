namespace GameServerManager
{
    public interface INotificationProvider
    {
        void Notify(string subject, string message);
    }
}
