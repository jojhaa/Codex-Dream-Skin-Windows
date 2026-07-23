using Windows.ApplicationModel;

namespace CodexDreamSkin.Services;

public sealed class StartupTaskService
{
    public const string TaskId = "CodexDreamSkinMonitor";

    public async Task<StartupTaskState?> GetStateAsync()
    {
        try
        {
            return (await StartupTask.GetAsync(TaskId)).State;
        }
        catch
        {
            return null;
        }
    }

    public async Task<StartupTaskState?> SetEnabledAsync(bool enabled)
    {
        try
        {
            var task = await StartupTask.GetAsync(TaskId);
            if (!enabled)
            {
                task.Disable();
                return task.State;
            }

            return task.State == StartupTaskState.Enabled
                ? task.State
                : await task.RequestEnableAsync();
        }
        catch
        {
            return null;
        }
    }
}
