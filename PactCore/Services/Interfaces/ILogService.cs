namespace PACT2.PactCore.Services.Interfaces;

public interface ILogService
{
    public interface ILogService
    {
        void LogMessage(string message);
        Task LogToJournalAsync(string message);
        Task ClearLogsAsync();
    }
}