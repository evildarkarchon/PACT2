using System;
using System.Threading.Tasks;

namespace AutoQAC.Extensions;

public static class TaskExtensions
{
    public static void FireAndForget(this Task task)
    {
        task.ContinueWith(t =>
        {
            if (t.IsFaulted && t.Exception != null)
            {
                Console.WriteLine($"Error in FireAndForget task: {t.Exception.GetBaseException()}");
            }
        }, TaskContinuationOptions.OnlyOnFaulted);
    }
}