namespace EdgePeek.Services;

public static class TaskExtensions
{
    public static void Forget(this Task task, string context)
    {
        _ = ObserveAsync(task, context);
    }

    private static async Task ObserveAsync(Task task, string context)
    {
        try
        {
            await task;
        }
        catch (Exception ex)
        {
            AppLog.Write($"{context} failed.");
            AppLog.Write(ex);
        }
    }
}
