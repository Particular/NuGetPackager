using System.Diagnostics;
using Microsoft.Build.Framework;

static class TaskItemExtensions
{
    [DebuggerStepThrough]
    public static string FullPath(this ITaskItem item)
    {
        return item.GetMetadata("FullPath");
    }
}