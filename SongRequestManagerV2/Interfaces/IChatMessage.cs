using CatCore.Models.Shared;

namespace SongRequestManagerV2.Interfaces
{
    public interface IChatMessage
    {
        string Id { get; }
        bool IsSystemMessage { get; }
        bool IsActionMessage { get; }
        bool IsMentioned { get; }
        string Message { get; }
        IChatUser Sender { get; }
    }
}
